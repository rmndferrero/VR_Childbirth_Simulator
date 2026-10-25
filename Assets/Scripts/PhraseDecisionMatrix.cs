using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Master controller for the 3-Column Parallel Phrase Board.
/// Features:
/// - 3 parallel columns (Greeting, Patient ID, Procedure) with top drop slots and choice banks below.
/// - Clean "Choice 1", "Choice 2", "Choice 3" pills with zero font-shrinking or VR clutter.
/// - Additive Live Preview: Hovering over any choice temporarily adds its spoken text onto the slotted statement.
/// - 1-click tap-to-slot and tap-to-eject, plus drag-to-slot and drag-to-swap.
/// - Strict 2D planar locking (Z = 0) with zero 3D mid-air floating glitches.
/// - Sequential Confirmation Flow: Built Dialogue focus -> Color Feedback -> Progression to Step 2.
/// - Multi-Criteria Weighted Sum Model (WSM) scoring aligned with OSCE clinical rubrics.
/// </summary>
public class PhraseDecisionMatrix : MonoBehaviour
{
    public static PhraseDecisionMatrix Instance { get; private set; }

    [Header("Scenario Data")]
    public PhraseFormulationScenarioSO scenarioData;

    [Header("3 Target Drop Slots")]
    public PhraseDropSlot introSlot;
    public PhraseDropSlot identSlot;
    public PhraseDropSlot explainSlot;

    [Header("3 Column Choice Banks")]
    public Transform column1ChoicesContainer;
    public Transform column2ChoicesContainer;
    public Transform column3ChoicesContainer;

    [Header("Currently Slotted Chips (1 per category)")]
    public DraggablePhraseChip selectedIntroChip => introSlot != null ? introSlot.currentSlottedChip : null;
    public DraggablePhraseChip selectedIdentChip => identSlot != null ? identSlot.currentSlottedChip : null;
    public DraggablePhraseChip selectedExplainChip => explainSlot != null ? explainSlot.currentSlottedChip : null;

    [Header("Currently Hovered Chip (for additive preview)")]
    private DraggablePhraseChip hoveredChip = null;

    [Header("Spawned Chips Pool")]
    public GameObject chipPrefab;
    public List<DraggablePhraseChip> spawnedChips = new List<DraggablePhraseChip>();

    [Header("Formulated Statement Preview Card")]
    public TMP_Text liveStatementText;
    public Outline liveStatementCardOutline;
    public Image liveStatementCardBg;
    [TextArea(1, 2)] public string defaultEmptyPrompt = "<color=#64748B><i>Hover over a choice to preview, or drag into matching slot above to create your dialogue...</i></color>";

    [Header("Feedback Banner & Confirmation")]
    public TMP_Text feedbackBannerText;
    public Button confirmButton;

    [Header("Legacy Compatibility Fields")]
    [HideInInspector] public Transform stage1ChoicesContainer;
    [HideInInspector] public Transform stage2ChoicesContainer;
    [HideInInspector] public Transform stage3ChoicesContainer;
    [HideInInspector] public Transform chipPoolContainer;
    [HideInInspector] public Image evaluationGaugeFill;
    [HideInInspector] public TMP_Text scorePercentText;

    [Header("UbiSim Medical Colors")]
    public Color greenColor = new Color(0.20f, 0.83f, 0.60f, 1f);  // #34D399 Vibrant Emerald
    public Color orangeColor = new Color(0.96f, 0.62f, 0.04f, 1f); // #F59E0B Warning Amber
    public Color redColor = new Color(0.94f, 0.27f, 0.27f, 1f);    // #EF4444 Failure Red
    public Color neutralFocusColor = new Color(0.22f, 0.74f, 0.97f, 0.9f); // #38BDF8 Cyan Focus

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip submitClickClip;
    public AudioClip optimalSuccessClip;
    public AudioClip warningClip;

    private bool isEvaluationLocked = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);
        }

        WireSlotEvents();
    }

    private void Start()
    {
        WireSlotEvents();
        PopulatePoolFromScenario();
        UpdateLivePreview();
    }

    private void OnEnable()
    {
        WireSlotEvents();
        if (spawnedChips == null || spawnedChips.Count == 0)
        {
            PopulatePoolFromScenario();
        }
        UpdateLivePreview();
    }

    private void WireSlotEvents()
    {
        if (introSlot != null)
        {
            introSlot.OnChipSlotted -= HandleChipSlotted;
            introSlot.OnChipSlotted += HandleChipSlotted;
            introSlot.OnChipRemoved -= HandleChipRemoved;
            introSlot.OnChipRemoved += HandleChipRemoved;
        }
        if (identSlot != null)
        {
            identSlot.OnChipSlotted -= HandleChipSlotted;
            identSlot.OnChipSlotted += HandleChipSlotted;
            identSlot.OnChipRemoved -= HandleChipRemoved;
            identSlot.OnChipRemoved += HandleChipRemoved;
        }
        if (explainSlot != null)
        {
            explainSlot.OnChipSlotted -= HandleChipSlotted;
            explainSlot.OnChipSlotted += HandleChipSlotted;
            explainSlot.OnChipRemoved -= HandleChipRemoved;
            explainSlot.OnChipRemoved += HandleChipRemoved;
        }
    }

    private void HandleChipSlotted(PhraseDropSlot slot, DraggablePhraseChip chip)
    {
        UpdateLivePreview();
    }

    private void HandleChipRemoved(PhraseDropSlot slot)
    {
        UpdateLivePreview();
    }

    public void PopulatePoolFromScenario()
    {
        if (scenarioData == null || scenarioData.availableTokens == null) return;

        // Clean existing spawned chips
        foreach (var c in spawnedChips)
        {
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }
        }
        spawnedChips.Clear();

        if (introSlot != null) introSlot.ClearSlot();
        if (identSlot != null) identSlot.ClearSlot();
        if (explainSlot != null) explainSlot.ClearSlot();

        int introCount = 0;
        int identCount = 0;
        int explainCount = 0;

        foreach (var token in scenarioData.availableTokens)
        {
            if (token == null) continue;

            Transform targetBank = null;
            int choiceNum = 1;

            if (token.category == PhraseSlotCategory.Introduction)
            {
                targetBank = column1ChoicesContainer != null ? column1ChoicesContainer : stage1ChoicesContainer;
                introCount++;
                choiceNum = introCount;
            }
            else if (token.category == PhraseSlotCategory.PatientIdentification)
            {
                targetBank = column2ChoicesContainer != null ? column2ChoicesContainer : stage2ChoicesContainer;
                identCount++;
                choiceNum = identCount;
            }
            else if (token.category == PhraseSlotCategory.ProcedureExplanation)
            {
                targetBank = column3ChoicesContainer != null ? column3ChoicesContainer : stage3ChoicesContainer;
                explainCount++;
                choiceNum = explainCount;
            }

            if (targetBank == null) targetBank = transform;

            GameObject chipObj = null;
            if (chipPrefab != null)
            {
                chipObj = Instantiate(chipPrefab, targetBank, false);
            }
            else
            {
                var existing = targetBank.GetComponentsInChildren<DraggablePhraseChip>(true);
                foreach (var ex in existing)
                {
                    if (ex.tokenData == token) { chipObj = ex.gameObject; break; }
                }
            }

            if (chipObj != null)
            {
                var chipComp = chipObj.GetComponent<DraggablePhraseChip>();
                if (chipComp != null)
                {
                    chipComp.Initialize(token, choiceNum, targetBank);
                    spawnedChips.Add(chipComp);
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // VR RAYCAST HOVER PREVIEW
    // ══════════════════════════════════════════════════════════════════

    public void SetHoveredChip(DraggablePhraseChip chip)
    {
        if (isEvaluationLocked) return;
        hoveredChip = chip;
        UpdateLivePreview();
    }

    public void ClearHoveredChip(DraggablePhraseChip chip)
    {
        if (isEvaluationLocked) return;
        if (hoveredChip == chip)
        {
            hoveredChip = null;
            UpdateLivePreview();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // TAP & DRAG SELECTION / SWAP
    // ══════════════════════════════════════════════════════════════════

    public void HandlePillClicked(DraggablePhraseChip chip)
    {
        if (isEvaluationLocked || chip == null || chip.tokenData == null) return;

        // If chip is currently slotted, clicking it ejects it back to bank
        if (chip.currentSlot != null)
        {
            chip.ReturnToPool();
            UpdateLivePreview();
            return;
        }

        // Otherwise, snap into its matching category slot
        PhraseDropSlot targetSlot = GetSlotForCategory(chip.tokenData.category);
        if (targetSlot != null)
        {
            targetSlot.AcceptChip(chip);
        }

        UpdateLivePreview();
    }

    public void HandleChipSwap(DraggablePhraseChip draggedChip, DraggablePhraseChip targetChip)
    {
        if (isEvaluationLocked || draggedChip == null || targetChip == null || draggedChip == targetChip) return;
        if (draggedChip.tokenData == null || targetChip.tokenData == null) return;
        if (draggedChip.tokenData.category != targetChip.tokenData.category) return;

        PhraseDropSlot slot = GetSlotForCategory(draggedChip.tokenData.category);
        if (slot == null) return;

        // Case 1: Target chip is currently in the slot -> dragged chip takes the slot
        if (targetChip.currentSlot == slot)
        {
            slot.AcceptChip(draggedChip);
        }
        // Case 2: Dragged chip is currently in the slot -> target chip takes the slot
        else if (draggedChip.currentSlot == slot)
        {
            slot.AcceptChip(targetChip);
        }
        // Case 3: Both are in the bank -> swap sibling index
        else
        {
            int indexA = draggedChip.transform.GetSiblingIndex();
            int indexB = targetChip.transform.GetSiblingIndex();
            draggedChip.transform.SetSiblingIndex(indexB);
            targetChip.transform.SetSiblingIndex(indexA);
        }

        UpdateLivePreview();
    }

    public PhraseDropSlot GetSlotForCategory(PhraseSlotCategory category)
    {
        switch (category)
        {
            case PhraseSlotCategory.Introduction: return introSlot;
            case PhraseSlotCategory.PatientIdentification: return identSlot;
            case PhraseSlotCategory.ProcedureExplanation: return explainSlot;
            default: return null;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // LIVE FORMULATED STATEMENT (ADDITIVE PREVIEW)
    // ══════════════════════════════════════════════════════════════════

    public void UpdateLivePreview()
    {
        DraggablePhraseChip introChip = selectedIntroChip;
        DraggablePhraseChip identChip = selectedIdentChip;
        DraggablePhraseChip explainChip = selectedExplainChip;

        // Check if hovered chip belongs to each category
        bool isIntroHovered = (hoveredChip != null && hoveredChip.tokenData != null && hoveredChip.tokenData.category == PhraseSlotCategory.Introduction);
        bool isIdentHovered = (hoveredChip != null && hoveredChip.tokenData != null && hoveredChip.tokenData.category == PhraseSlotCategory.PatientIdentification);
        bool isExplainHovered = (hoveredChip != null && hoveredChip.tokenData != null && hoveredChip.tokenData.category == PhraseSlotCategory.ProcedureExplanation);

        DraggablePhraseChip effectiveIntro = isIntroHovered ? hoveredChip : introChip;
        DraggablePhraseChip effectiveIdent = isIdentHovered ? hoveredChip : identChip;
        DraggablePhraseChip effectiveExplain = isExplainHovered ? hoveredChip : explainChip;

        bool allThreeSlotted = (introChip != null && identChip != null && explainChip != null);

        if (confirmButton != null)
        {
            confirmButton.interactable = allThreeSlotted && !isEvaluationLocked;
        }

        if (feedbackBannerText != null && !isEvaluationLocked)
        {
            int filledCount = (introChip != null ? 1 : 0) + (identChip != null ? 1 : 0) + (explainChip != null ? 1 : 0);
            if (allThreeSlotted)
            {
                feedbackBannerText.text = "<color=#38BDF8><b>[OK] Dialogue complete. Tap 'Speak to Mother' below.</b></color>";
            }
            else
            {
                feedbackBannerText.text = $"<color=#94A3B8>Fill all 3 slots to speak ({filledCount}/3 slots slotted).</color>";
            }
        }

        if (liveStatementText != null)
        {
            if (effectiveIntro == null && effectiveIdent == null && effectiveExplain == null)
            {
                liveStatementText.text = defaultEmptyPrompt;
                return;
            }

            string segIntro = FormatSegment(effectiveIntro, introChip, "[ 1. Greeting ]");
            string segIdent = FormatSegment(effectiveIdent, identChip, "[ 2. Patient ID ]");
            string segExplain = FormatSegment(effectiveExplain, explainChip, "[ 3. Procedure ]");

            liveStatementText.text = $"\"{segIntro} {segIdent} {segExplain}\"";
        }
    }

    private string FormatSegment(DraggablePhraseChip effective, DraggablePhraseChip slotted, string placeholder)
    {
        if (effective == null || effective.tokenData == null)
        {
            return $"<color=#64748B><i>{placeholder}</i></color>";
        }

        string text = effective.tokenData.fullPhraseText;

        // If hovered and not already slotted: show in glowing medical cyan italic (additive preview)
        if (effective == hoveredChip && effective != slotted)
        {
            return $"<color=#38BDF8><i>{text}</i></color>";
        }
        else
        {
            // Slotted in bold white
            return $"<b><color=#FFFFFF>{text}</color></b>";
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // SEQUENTIAL CONFIRMATION & EVALUATION
    // ══════════════════════════════════════════════════════════════════

    public void OnConfirmButtonClicked()
    {
        if (isEvaluationLocked) return;
        if (selectedIntroChip == null || selectedIdentChip == null || selectedExplainChip == null) return;

        PlaySound(submitClickClip);

        var introToken = selectedIntroChip.tokenData;
        var identToken = selectedIdentChip.tokenData;
        var explainToken = selectedExplainChip.tokenData;

        float wIntro = scenarioData != null ? scenarioData.weightIntroduction : 0.25f;
        float wIdent = scenarioData != null ? scenarioData.weightIdentification : 0.35f;
        float wExplain = scenarioData != null ? scenarioData.weightExplanation : 0.40f;

        // 1. Raw WSM Dot Product Utility (1.0 to 5.0 scale)
        float rawUtility = (introToken.RawScore * wIntro) +
                           (identToken.RawScore * wIdent) +
                           (explainToken.RawScore * wExplain);

        // 2. Linear Min-Max Normalization to 0% - 100%
        float normalizedScore = Mathf.Clamp(((rawUtility - 1.0f) / 4.0f) * 100f, 0f, 100f);

        // 3. Independent dimension scores for debriefing
        float pRaw = (introToken.professionalism * wIntro) + (identToken.professionalism * wIdent) + (explainToken.professionalism * wExplain);
        float eRaw = (introToken.empathy * wIntro) + (identToken.empathy * wIdent) + (explainToken.empathy * wExplain);
        float cRaw = (introToken.clarity * wIntro) + (identToken.clarity * wIdent) + (explainToken.clarity * wExplain);

        float pPercent = Mathf.Clamp(((pRaw - 1.0f) / 4.0f) * 100f, 0f, 100f);
        float ePercent = Mathf.Clamp(((eRaw - 1.0f) / 4.0f) * 100f, 0f, 100f);
        float cPercent = Mathf.Clamp(((cRaw - 1.0f) / 4.0f) * 100f, 0f, 100f);

        // 4. Cache Debrief Profile for Step 8 Summary Screen
        var debriefData = CommunicationDebriefData.CreateFromEvaluation(
            pPercent, ePercent, cPercent, normalizedScore, introToken, identToken, explainToken);

        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.communicationDebrief = debriefData;
        }

        // 5. Evaluate against Thresholds & Trigger Sequential Flow
        float greenThreshold = scenarioData != null ? scenarioData.greenThreshold : 80f;
        float orangeThreshold = scenarioData != null ? scenarioData.orangeThreshold : 50f;

        StartCoroutine(ProcessEvaluationOutcome(normalizedScore, greenThreshold, orangeThreshold));
    }

    private IEnumerator ProcessEvaluationOutcome(float score, float greenThreshold, float orangeThreshold)
    {
        isEvaluationLocked = true;
        if (confirmButton != null) confirmButton.interactable = false;

        // ════════════════════════════════════════════════════════════════
        // STAGE 1: SHOW THE BUILT DIALOGUE FIRST (Neutral Focus)
        // ════════════════════════════════════════════════════════════════
        if (liveStatementCardOutline != null)
        {
            liveStatementCardOutline.effectColor = neutralFocusColor;
        }
        if (feedbackBannerText != null)
        {
            feedbackBannerText.text = "<color=#94A3B8><i>Speaking to mother...</i></color>";
        }

        // Pause so student reads their completed statement
        yield return new WaitForSeconds(1.0f);

        // ════════════════════════════════════════════════════════════════
        // STAGE 2: THEN SHOWS COLOR & FEEDBACK
        // ════════════════════════════════════════════════════════════════
        bool isPassing = false;

        if (score >= greenThreshold)
        {
            isPassing = true;
            if (liveStatementCardOutline != null) liveStatementCardOutline.effectColor = greenColor;
            if (liveStatementCardBg != null) liveStatementCardBg.color = new Color(0.04f, 0.20f, 0.13f, 0.95f);

            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = $"<color=#34D399><b>[OK] Clinical Standard Met ({Mathf.RoundToInt(score)}%)</b></color>\n" +
                    (scenarioData != null ? scenarioData.optimalPatientResponse : "\"Thank you for explaining, nurse. I understand and am ready.\"");
            }

            PlaySound(optimalSuccessClip);
            Debug.Log($"[Decision Matrix] Green Tier ({score}%): Dialogue accepted. Proceeding to Step 2.");
        }
        else if (score >= orangeThreshold)
        {
            isPassing = true;
            if (liveStatementCardOutline != null) liveStatementCardOutline.effectColor = orangeColor;
            if (liveStatementCardBg != null) liveStatementCardBg.color = new Color(0.24f, 0.16f, 0.04f, 0.95f);

            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = $"<color=#F59E0B><b>[!] Suboptimal Communication ({Mathf.RoundToInt(score)}%, -5 pts)</b></color>\n" +
                    (scenarioData != null ? scenarioData.suboptimalPatientResponse : "\"Okay nurse... please be gentle.\"");
            }

            PlaySound(warningClip);

            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordViolation(
                    ClinicalViolationType.Suboptimal_Dialogue,
                    "Suboptimal communication: Lacked complete professional protocol or sensory explanation.");
            }
        }
        else
        {
            isPassing = false;
            if (liveStatementCardOutline != null) liveStatementCardOutline.effectColor = redColor;
            if (liveStatementCardBg != null) liveStatementCardBg.color = new Color(0.25f, 0.08f, 0.08f, 0.95f);

            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = $"<color=#EF4444><b>[X] Protocol Inadequate ({Mathf.RoundToInt(score)}%): Consent Denied</b></color>\n" +
                    "<i>Pitcher Wash remains locked. Please select appropriate phrases and try again.</i>";
            }

            PlaySound(warningClip);

            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordViolation(
                    ClinicalViolationType.Inadequate_Dialogue,
                    "Inadequate patient communication: Failed to obtain valid informed consent.");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // STAGE 3: THEN PROCEEDS
        // ════════════════════════════════════════════════════════════════
        yield return new WaitForSeconds(2.0f);

        if (isPassing)
        {
            // Unlock Pitcher and advance to Step 2
            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.OnMotherInformedConsentGiven();
            }

            yield return new WaitForSeconds(0.5f);
            gameObject.SetActive(false);
        }
        else
        {
            // Allow student to change choices and try again
            isEvaluationLocked = false;
            if (confirmButton != null) confirmButton.interactable = true;
        }
    }

    public void LoadOptimalPreset()
    {
        if (isEvaluationLocked) return;
        ClearFormulation();
        foreach (var chip in spawnedChips)
        {
            if (chip == null || chip.tokenData == null) continue;
            if (chip.tokenData.RawScore > 4.5f)
            {
                HandlePillClicked(chip);
            }
        }
        UpdateLivePreview();
    }

    public void LoadSuboptimalPreset()
    {
        if (isEvaluationLocked) return;
        ClearFormulation();
        foreach (var chip in spawnedChips)
        {
            if (chip == null || chip.tokenData == null) continue;
            if (chip.tokenData.RawScore >= 3.0f && chip.tokenData.RawScore <= 3.5f)
            {
                HandlePillClicked(chip);
            }
        }
        UpdateLivePreview();
    }

    public void ClearFormulation()
    {
        if (isEvaluationLocked) return;
        if (introSlot != null) introSlot.ClearSlot();
        if (identSlot != null) identSlot.ClearSlot();
        if (explainSlot != null) explainSlot.ClearSlot();
        foreach (var c in spawnedChips)
        {
            if (c != null) c.ReturnToPool();
        }
        UpdateLivePreview();
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
