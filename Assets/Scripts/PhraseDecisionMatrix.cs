using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Master controller for the 3-Slot Phrase Formulation Decision Matrix on the clinical clipboard.
/// Evaluates communication choices using a Multi-Criteria Weighted Sum Model (WSM) dot product,
/// enforces procedural tool gatekeeping (unlocking/locking the Pitcher Wash), and caches affective
/// sub-scores (Professionalism, Empathy, Clarity) for the Step 8 Final Debrief.
/// </summary>
public class PhraseDecisionMatrix : MonoBehaviour
{
    public static PhraseDecisionMatrix Instance { get; private set; }

    [Header("Scenario Data")]
    public PhraseFormulationScenarioSO scenarioData;

    [Header("The 3 Drop Slots")]
    public PhraseDropSlot introSlot;       // Weight: 25%
    public PhraseDropSlot identSlot;       // Weight: 35%
    public PhraseDropSlot explainSlot;     // Weight: 40%

    [Header("Chip Pool & Spawning")]
    public Transform chipPoolContainer;
    public GameObject chipPrefab;
    public List<DraggablePhraseChip> spawnedChips = new List<DraggablePhraseChip>();

    [Header("Live Statement Preview")]
    public TMP_Text liveStatementText;
    [TextArea(1, 2)] public string defaultEmptyPrompt = "<color=#64748B><i>Arrange phrases into the 3 slots above to formulate your statement to the mother...</i></color>";

    [Header("Decision Matrix Feedback Gauge & Controls")]
    public Image evaluationGaugeFill;
    public TMP_Text scorePercentText;
    public TMP_Text feedbackBannerText;
    public Button confirmButton;

    [Header("Colors")]
    public Color greenColor = new Color(0.20f, 0.83f, 0.60f, 1f);  // #34D399
    public Color orangeColor = new Color(0.96f, 0.62f, 0.04f, 1f); // #F59E0B
    public Color redColor = new Color(0.94f, 0.27f, 0.27f, 1f);    // #EF4444
    public Color defaultGaugeColor = new Color(0.30f, 0.40f, 0.50f, 0.6f);

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
    }

    private void Start()
    {
        SetupSlots();
        PopulatePoolFromScenario();
        UpdateLivePreview();
    }

    private void OnEnable()
    {
        SetupSlots();
        UpdateLivePreview();
    }

    private void SetupSlots()
    {
        if (introSlot != null)
        {
            introSlot.targetCategory = PhraseSlotCategory.Introduction;
            introSlot.stageWeight = scenarioData != null ? scenarioData.weightIntroduction : 0.25f;
            introSlot.OnChipSlotted += HandleSlotChanged;
            introSlot.OnChipRemoved += HandleSlotCleared;
        }

        if (identSlot != null)
        {
            identSlot.targetCategory = PhraseSlotCategory.PatientIdentification;
            identSlot.stageWeight = scenarioData != null ? scenarioData.weightIdentification : 0.35f;
            identSlot.OnChipSlotted += HandleSlotChanged;
            identSlot.OnChipRemoved += HandleSlotCleared;
        }

        if (explainSlot != null)
        {
            explainSlot.targetCategory = PhraseSlotCategory.ProcedureExplanation;
            explainSlot.stageWeight = scenarioData != null ? scenarioData.weightExplanation : 0.40f;
            explainSlot.OnChipSlotted += HandleSlotChanged;
            explainSlot.OnChipRemoved += HandleSlotCleared;
        }
    }

    public void PopulatePoolFromScenario()
    {
        if (scenarioData == null || scenarioData.availableTokens == null) return;
        if (chipPoolContainer == null) return;

        // Clean existing spawned chips
        foreach (var c in spawnedChips)
        {
            if (c != null) Destroy(c.gameObject);
        }
        spawnedChips.Clear();

        foreach (var token in scenarioData.availableTokens)
        {
            if (token == null) continue;

            GameObject chipObj = null;
            if (chipPrefab != null)
            {
                chipObj = Instantiate(chipPrefab, chipPoolContainer, false);
            }
            else
            {
                // Fallback: search existing chip children
                var existing = chipPoolContainer.GetComponentsInChildren<DraggablePhraseChip>(true);
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
                    chipComp.Initialize(token, chipPoolContainer);
                    chipComp.OnChipClicked += HandleChipClicked;
                    spawnedChips.Add(chipComp);
                }
            }
        }
    }

    private void HandleSlotChanged(PhraseDropSlot slot, DraggablePhraseChip chip)
    {
        UpdateLivePreview();
    }

    private void HandleSlotCleared(PhraseDropSlot slot)
    {
        UpdateLivePreview();
    }

    /// <summary>
    /// Tap-to-Slot / Tap-to-Return helper for VR comfort and accessibility.
    /// </summary>
    public void HandleChipClicked(DraggablePhraseChip chip)
    {
        if (isEvaluationLocked || chip == null) return;

        // If already in a slot, return to pool
        if (chip.currentSlot != null)
        {
            chip.ReturnToPool();
            UpdateLivePreview();
            return;
        }

        // Otherwise auto-slot into matching empty slot
        PhraseDropSlot targetSlot = GetSlotForCategory(chip.tokenData.category);
        if (targetSlot != null)
        {
            targetSlot.AcceptChip(chip);
            UpdateLivePreview();
        }
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

    public void UpdateLivePreview()
    {
        bool allSlotsFilled = (introSlot != null && introSlot.IsOccupied) &&
                              (identSlot != null && identSlot.IsOccupied) &&
                              (explainSlot != null && explainSlot.IsOccupied);

        if (confirmButton != null)
        {
            confirmButton.interactable = allSlotsFilled && !isEvaluationLocked;
        }

        if (liveStatementText != null)
        {
            if (!allSlotsFilled)
            {
                string assembled = "";
                if (introSlot != null && introSlot.IsOccupied) assembled += $"\"{introSlot.currentSlottedChip.tokenData.fullPhraseText}\" ";
                if (identSlot != null && identSlot.IsOccupied) assembled += $"\"{identSlot.currentSlottedChip.tokenData.fullPhraseText}\" ";
                if (explainSlot != null && explainSlot.IsOccupied) assembled += $"\"{explainSlot.currentSlottedChip.tokenData.fullPhraseText}\"";

                liveStatementText.text = string.IsNullOrEmpty(assembled)
                    ? defaultEmptyPrompt
                    : assembled.TrimEnd();
            }
            else
            {
                liveStatementText.text = $"\"{introSlot.currentSlottedChip.tokenData.fullPhraseText} " +
                                         $"{identSlot.currentSlottedChip.tokenData.fullPhraseText} " +
                                         $"{explainSlot.currentSlottedChip.tokenData.fullPhraseText}\"";
            }
        }
    }

    /// <summary>
    /// Executes the Multi-Criteria Decision Matrix (MCDM) Weighted Sum Model (WSM) evaluation.
    /// </summary>
    public void OnConfirmButtonClicked()
    {
        if (isEvaluationLocked) return;
        if (!introSlot.IsOccupied || !identSlot.IsOccupied || !explainSlot.IsOccupied) return;

        PlaySound(submitClickClip);

        var introToken = introSlot.currentSlottedChip.tokenData;
        var identToken = identSlot.currentSlottedChip.tokenData;
        var explainToken = explainSlot.currentSlottedChip.tokenData;

        float wIntro = scenarioData != null ? scenarioData.weightIntroduction : 0.25f;
        float wIdent = scenarioData != null ? scenarioData.weightIdentification : 0.35f;
        float wExplain = scenarioData != null ? scenarioData.weightExplanation : 0.40f;

        // 1. Raw WSM Dot Product Utility (1.0 to 5.0 scale)
        float rawUtility = (introToken.RawScore * wIntro) +
                           (identToken.RawScore * wIdent) +
                           (explainToken.RawScore * wExplain);

        // 2. Linear Min-Max Normalization to 0% - 100%
        float normalizedScore = Mathf.Clamp(((rawUtility - 1.0f) / 4.0f) * 100f, 0f, 100f);

        // 3. Compute independent dimension scores for debriefing
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

        // 5. Evaluate against Thresholds & Trigger Procedural Gatekeeping
        float greenThreshold = scenarioData != null ? scenarioData.greenThreshold : 80f;
        float orangeThreshold = scenarioData != null ? scenarioData.orangeThreshold : 50f;

        StartCoroutine(ProcessEvaluationOutcome(normalizedScore, greenThreshold, orangeThreshold));
    }

    private IEnumerator ProcessEvaluationOutcome(float score, float greenThreshold, float orangeThreshold)
    {
        isEvaluationLocked = true;
        if (confirmButton != null) confirmButton.interactable = false;

        // Visual gauge animation
        if (evaluationGaugeFill != null)
        {
            evaluationGaugeFill.fillAmount = score / 100f;
        }

        if (scorePercentText != null)
        {
            scorePercentText.text = $"{Mathf.RoundToInt(score)}%";
        }

        // TIER 1: OPTIMAL (Green >= 80%)
        if (score >= greenThreshold)
        {
            if (evaluationGaugeFill != null) evaluationGaugeFill.color = greenColor;
            if (scorePercentText != null) scorePercentText.color = greenColor;
            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = "<color=#34D399><b>✓ Clinical Standard Met (Optimal Consent)</b></color>\n" +
                    (scenarioData != null ? scenarioData.optimalPatientResponse : "\"Thank you for explaining, nurse. I am ready.\"");
            }

            PlaySound(optimalSuccessClip);
            Debug.Log($"[Decision Matrix] Green Tier ({score}%): Pitcher Wash Unlocked. Transitioning to Step 2.");

            yield return new WaitForSeconds(2.0f);

            // Unlock Pitcher and advance to Step 2
            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.OnMotherInformedConsentGiven();
            }

            yield return new WaitForSeconds(1.0f);
            gameObject.SetActive(false);
        }
        // TIER 2: SUBOPTIMAL (Orange 50% - 79%)
        else if (score >= orangeThreshold)
        {
            if (evaluationGaugeFill != null) evaluationGaugeFill.color = orangeColor;
            if (scorePercentText != null) scorePercentText.color = orangeColor;
            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = "<color=#F59E0B><b>▲ Suboptimal Communication (-5 pts)</b></color>\n" +
                    (scenarioData != null ? scenarioData.suboptimalPatientResponse : "\"Okay nurse... please be gentle.\"");
            }

            PlaySound(warningClip);

            // Record minor penalty into scoring engine
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordViolation(
                    ClinicalViolationType.Suboptimal_Dialogue,
                    "Suboptimal communication: Lacked complete professional protocol or sensory explanation.");
            }

            yield return new WaitForSeconds(2.5f);

            // Unlock Pitcher and advance to Step 2 with penalty logged
            if (PerinealCareManager.Instance != null)
            {
                PerinealCareManager.Instance.OnMotherInformedConsentGiven();
            }

            yield return new WaitForSeconds(1.0f);
            gameObject.SetActive(false);
        }
        // TIER 3: INADEQUATE / PROTOCOL FAILURE (Red < 50%)
        else
        {
            if (evaluationGaugeFill != null) evaluationGaugeFill.color = redColor;
            if (scorePercentText != null) scorePercentText.color = redColor;
            if (feedbackBannerText != null)
            {
                feedbackBannerText.text = "<color=#EF4444><b>✖ Protocol Inadequate: Informed Consent Denied</b></color>\n" +
                    "<i>Pitcher Wash remains locked. You must identify yourself and explain the procedure to proceed.</i>";
            }

            PlaySound(warningClip);

            // Record protocol failure violation
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordViolation(
                    ClinicalViolationType.Inadequate_Dialogue,
                    "Inadequate patient communication: Failed to obtain valid informed consent.");
            }

            yield return new WaitForSeconds(2.5f);

            // STRICT GATEKEEPING: Pitcher Wash remains LOCKED. Allow student to rearrange chips and try again
            isEvaluationLocked = false;
            if (confirmButton != null) confirmButton.interactable = true;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
