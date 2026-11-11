using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

public enum TutorialState
{
    Step1_GrabTool,
    Step2_ButtonInteraction,
    Step3_ForcepsTransfer,
    Step4_Completed
}

/// <summary>
/// Master sequence manager for TutorialScene.
/// Drives the 3D Holographic Controller Guide (HolographicTutorialGuide) through:
///   Step 1: Grabbing the Pitcher / instrument using the controller Grip button.
///   Step 2: Interacting with a floating sample UI panel button using the controller Ray & Index Trigger.
///   Step 3: Picking up an antiseptic cotton ball with Pickup Forceps, grabbing Handling Forceps,
///           transferring the cotton with Button A, and discarding it into the trash basin.
///   Step 4: Smooth fade transition into MainScene (Delivery Room).
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Current State")]
    public TutorialState currentState = TutorialState.Step1_GrabTool;

    [Header("Holographic 3D Guide System")]
    [Tooltip("Reference to the 3D Holographic Controller & Target Ring Guide.")]
    public HolographicTutorialGuide holographicGuide;

    [Header("Editable Step World-Space Guide UIs (Visible in Hierarchy & Scene View)")]
    [Tooltip("Step 1 Guide UI above the Pitcher.")]
    public GameObject step1PitcherGuideUI;

    [Tooltip("Step 2 Guide UI for the Sample Interactive Button.")]
    public GameObject step2SampleButtonGuideUI;

    [Tooltip("Step 3A Forceps Guide UI over the Pickup Forceps.")]
    public GameObject step3APickupForcepsGuideUI;

    [Tooltip("Step 3B Forceps Guide UI over the 7.5% Iodine Jar.")]
    public GameObject step3BIodineJarGuideUI;

    [Tooltip("Step 3C Forceps Guide UI over the Handling Forceps.")]
    public GameObject step3CHandlingForcepsGuideUI;

    [Tooltip("Step 3D Forceps Guide UI for Cotton Transfer between Forceps.")]
    public GameObject step3DTransferCottonGuideUI;

    [Tooltip("Step 3E Forceps Guide UI over the Trash Basin.")]
    public GameObject step3ETrashBasinGuideUI;

    [Header("Custom Target Ring Positions (Move these in Scene View!)")]
    [Tooltip("Move this in Scene View to set where the Pitcher guide label appears.")]
    public Transform ringPosition_Pitcher;

    [Tooltip("Move this in Scene View to set where the UI Button ring appears.")]
    public Transform ringPosition_UIButton;

    [Tooltip("Move this in Scene View to set where the Pickup Forceps ring appears.")]
    public Transform ringPosition_PickupForceps;

    [Tooltip("Move this in Scene View to set where the Iodine Jar ring appears.")]
    public Transform ringPosition_IodineJar;

    [Tooltip("Move this in Scene View to set where the Cotton Transfer ring appears.")]
    public Transform ringPosition_TransferCotton;

    [Tooltip("Move this in Scene View to set where the Trash Basin ring appears.")]
    public Transform ringPosition_TrashBasin;

    [Header("Step 1: Grab Mechanic References & Editable Texts")]
    [Tooltip("Target grabbable object on the table (e.g. Pitcher on Table 3).")]
    public XRGrabInteractable demoTool;
    public bool showPitcherRingCircles = false;
    [TextArea(2, 3)] public string step1GrabCalloutText = "HOLD [GRAB] BUTTON\nTO PICK UP";
    public string step1GrabRingLabel = "GRAB PITCHER";
    [TextArea(2, 3)] public string step1ReleaseCalloutText = "<color=#34D399>✓ PITCHER GRABBED!</color>\nRELEASE [GRAB] TO SET DOWN";
    public string step1ReleaseRingLabel = "RELEASE TO PLACE DOWN";

    [Header("Step 2: Sample UI Button Panel References & Editable Texts")]
    [Tooltip("Floating sample panel used to teach VR ray pointing & trigger clicking.")]
    public GameObject sampleButtonPanel;
    public CanvasGroup sampleButtonCanvasGroup;
    public Button sampleActionButton;
    public TextMeshProUGUI sampleButtonLabel;
    [TextArea(2, 3)] public string step2TriggerCalloutText = "POINT & PRESS [TRIGGER]\nBUTTON TO CLICK UI";
    public string step2TriggerRingLabel = "CLICK SAMPLE BUTTON";
    public string step2ButtonConfirmedText = "<color=#34D399>✓ CONFIRMED!</color>";

    [Header("Step 3: Cotton Ball & Dual-Forceps References & Editable Texts")]
    public ForcepsController pickupForceps;
    public ForcepsController handlingForceps;
    public Transform antisepticJarTransform;
    public Transform wasteBasinTransform;
    public WasteBasin wasteBasin;

    [TextArea(2, 3)] public string step3A_PickupForcepsCallout = "1. GRAB PICKUP FORCEPS\nUSING [GRIP] BUTTON";
    public string step3A_PickupForcepsRingLabel = "GRAB FORCEPS";

    [TextArea(2, 3)] public string step3B_DipJarCallout = "2. DIP IN IODINE JAR &\nHOLD [A] TO PICK COTTON";
    public string step3B_DipJarRingLabel = "DIP & HOLD [A]";

    [TextArea(2, 3)] public string step3C_HandlingForcepsCallout = "3. GRAB HANDLING FORCEPS\nUSING [GRIP] BUTTON";
    public string step3C_HandlingForcepsRingLabel = "GRAB HANDLING FORCEPS";

    [TextArea(2, 3)] public string step3D_TransferCottonCallout = "4. BRING FORCEPS CLOSE\n& HOLD [A] TO TRANSFER";
    public string step3D_TransferCottonRingLabel = "TRANSFER COTTON";

    [TextArea(2, 3)] public string step3E_TrashDisposeCallout = "<color=#34D399>✓ COTTON TRANSFERRED!</color>\n5. RELEASE [A] OVER TRASH BIN";
    public string step3E_TrashDisposeRingLabel = "DROP IN BIN";

    [Header("Step 4: Completion & Navigation UI")]
    public GameObject completionCard;
    public Button proceedToMainSceneButton;
    public Button skipTutorialButton;

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip stepCompleteClip;
    public AudioClip tutorialCompleteClip;

    private bool step1ToolGrabbed = false;
    private bool step1ToolReleased = false;
    private bool step2ButtonClicked = false;

    private int step3SubPhase = 0; // 0 = grab pickup, 1 = dip & pick cotton, 2 = grab handling forceps, 3 = transfer cotton, 4 = dispose in bin
    private float step3HandlingHoldTimer = 0f;
    private bool step3HandlingAlreadyHeldAtStart = false;
    private bool isTransitioningStep = false;
    private bool hasDisposedCottonInTrash = false;
    private float step3MissedTrashElapsed = 0f;
    private const float Step3MissedTrashFallGraceSeconds = 1.2f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (wasteBasin == null)
        {
            if (wasteBasinTransform != null)
            {
                wasteBasin = wasteBasinTransform.GetComponentInChildren<WasteBasin>(true);
            }
            if (wasteBasin == null)
            {
                wasteBasin = FindFirstObjectByType<WasteBasin>(FindObjectsInactive.Include);
            }
        }

        if (proceedToMainSceneButton != null)
        {
            proceedToMainSceneButton.onClick.RemoveAllListeners();
            proceedToMainSceneButton.onClick.AddListener(OnProceedToMainSceneClicked);
        }

        if (skipTutorialButton != null)
        {
            skipTutorialButton.onClick.RemoveAllListeners();
            skipTutorialButton.onClick.AddListener(OnProceedToMainSceneClicked);
        }

        if (sampleActionButton != null)
        {
            sampleActionButton.onClick.RemoveAllListeners();
            sampleActionButton.onClick.AddListener(OnSampleButtonClicked);
        }
    }

    private void OnEnable()
    {
        WasteBasin.OnCottonDisposed -= HandleWasteBasinCottonDisposed;
        WasteBasin.OnCottonDisposed += HandleWasteBasinCottonDisposed;
    }

    private void OnDisable()
    {
        WasteBasin.OnCottonDisposed -= HandleWasteBasinCottonDisposed;
    }

    private void HandleWasteBasinCottonDisposed(WasteBasin basin, CottonState cotton)
    {
        if (currentState != TutorialState.Step3_ForcepsTransfer || step3SubPhase != 4 || isTransitioningStep) return;
        if (wasteBasin != null && basin != null && basin != wasteBasin) return;

        hasDisposedCottonInTrash = true;
    }

    private void Start()
    {
        AutoFindMissingReferences();
        HideAllStepGuideUIsExcept(step1PitcherGuideUI);
        // UI styling is no longer overwritten at runtime so all Hierarchy/Scene UI edits stay intact!
        SetupStep1();
    }

    public void HideAllStepGuideUIsExcept(GameObject activeUI)
    {
        GameObject[] allStepUIs = new GameObject[]
        {
            step1PitcherGuideUI,
            step2SampleButtonGuideUI,
            step3APickupForcepsGuideUI,
            step3BIodineJarGuideUI,
            step3CHandlingForcepsGuideUI,
            step3DTransferCottonGuideUI,
            step3ETrashBasinGuideUI
        };

        foreach (var ui in allStepUIs)
        {
            if (ui != null && ui != activeUI)
            {
                ui.SetActive(false);
            }
        }

        if (activeUI != null)
        {
            activeUI.SetActive(true);
        }
    }

    public void SetAllEditableUIsVisibleInEditor(bool showAll)
    {
        if (step1PitcherGuideUI != null) step1PitcherGuideUI.SetActive(true);
        if (step2SampleButtonGuideUI != null) step2SampleButtonGuideUI.SetActive(showAll);
        if (sampleButtonPanel != null) sampleButtonPanel.SetActive(showAll);
        if (step3APickupForcepsGuideUI != null) step3APickupForcepsGuideUI.SetActive(showAll);
        if (step3BIodineJarGuideUI != null) step3BIodineJarGuideUI.SetActive(showAll);
        if (step3CHandlingForcepsGuideUI != null) step3CHandlingForcepsGuideUI.SetActive(showAll);
        if (step3DTransferCottonGuideUI != null) step3DTransferCottonGuideUI.SetActive(showAll);
        if (step3ETrashBasinGuideUI != null) step3ETrashBasinGuideUI.SetActive(showAll);
        if (completionCard != null) completionCard.SetActive(showAll);
    }

    public void AutoFindMissingReferences()
    {
        if (holographicGuide == null)
            holographicGuide = FindFirstObjectByType<HolographicTutorialGuide>();

        if (demoTool == null)
        {
            var pitcherGo = GameObject.Find("Pitcher") ?? GameObject.Find("Old Pitcher");
            if (pitcherGo != null) demoTool = pitcherGo.GetComponent<XRGrabInteractable>();
        }

        if (pickupForceps == null || handlingForceps == null)
        {
            foreach (var fc in FindObjectsByType<ForcepsController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fc.role == ForcepsRole.Pickup && pickupForceps == null) pickupForceps = fc;
                else if (fc.role == ForcepsRole.Handling && handlingForceps == null) handlingForceps = fc;
            }
        }

        if (antisepticJarTransform == null)
        {
            var jarGo = GameObject.Find("7.5_iodine") ?? GameObject.Find("10_iodine");
            if (jarGo != null) antisepticJarTransform = jarGo.transform;
        }

        if (wasteBasinTransform == null)
        {
            var trashGo = GameObject.Find("TrashCan_InteriorDisposalZone") ?? GameObject.Find("TrashCan");
            if (trashGo != null) wasteBasinTransform = trashGo.transform;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // STEP 1: GRAB MECHANIC (HOLD GRIP BUTTON TO PICK UP PITCHER)
    // ══════════════════════════════════════════════════════════════════

    public void SetupStep1()
    {
        currentState = TutorialState.Step1_GrabTool;
        step1ToolGrabbed = false;
        step1ToolReleased = false;
        isTransitioningStep = false;

        if (sampleButtonPanel != null) sampleButtonPanel.SetActive(false);
        if (completionCard != null) completionCard.SetActive(false);
        HideAllStepGuideUIsExcept(step1PitcherGuideUI);

        if (demoTool != null)
        {
            demoTool.enabled = true;
            demoTool.gameObject.layer = LayerMask.NameToLayer("Default");
            foreach (var col in demoTool.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.gameObject.layer = LayerMask.NameToLayer("Default");
            }
            demoTool.selectEntered.RemoveListener(OnDemoToolGrabbed);
            demoTool.selectExited.RemoveListener(OnDemoToolReleased);
            demoTool.selectEntered.AddListener(OnDemoToolGrabbed);
            demoTool.selectExited.AddListener(OnDemoToolReleased);
        }

        if (holographicGuide != null)
        {
            Transform target = demoTool != null ? demoTool.transform : null;
            Transform ringUI = step1PitcherGuideUI != null ? step1PitcherGuideUI.transform : ringPosition_Pitcher;
            holographicGuide.ShowInstruction(
                HolographicButtonType.Grip,
                step1GrabCalloutText,
                step1GrabCalloutText,
                "GRIP",
                target,
                step1GrabRingLabel,
                new Vector3(0f, 0.08f, 0f),
                showLaserRay: false,
                showTargetRing: true,
                ringAnchor: ringUI,
                showRingCircles: showPitcherRingCircles,
                preserveRingHierarchyText: false
            );
        }
    }

    private void OnDemoToolGrabbed(SelectEnterEventArgs args)
    {
        if (currentState != TutorialState.Step1_GrabTool || step1ToolGrabbed) return;
        step1ToolGrabbed = true;

        PlaySound(stepCompleteClip);

        if (holographicGuide != null)
        {
            Transform ringUI = step1PitcherGuideUI != null ? step1PitcherGuideUI.transform : ringPosition_Pitcher;
            holographicGuide.UpdateTargetAndCallout(
                HolographicButtonType.Grip,
                step1ReleaseCalloutText,
                step1ReleaseCalloutText,
                "GRIP",
                null,
                step1ReleaseRingLabel,
                new Vector3(0f, 0.08f, 0f),
                showLaserRay: false,
                showTargetRing: true,
                ringAnchor: ringUI,
                showRingCircles: showPitcherRingCircles,
                preserveRingHierarchyText: false
            );
        }
    }

    private void OnDemoToolReleased(SelectExitEventArgs args)
    {
        if (currentState != TutorialState.Step1_GrabTool || !step1ToolGrabbed || step1ToolReleased || isTransitioningStep) return;
        step1ToolReleased = true;

        if (demoTool != null)
        {
            demoTool.selectEntered.RemoveListener(OnDemoToolGrabbed);
            demoTool.selectExited.RemoveListener(OnDemoToolReleased);
            // Disable demoTool interactable & move to Ignore Raycast layer so it does not occlude forceps/buttons in steps 2 & 3
            demoTool.enabled = false;
            demoTool.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var col in demoTool.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            }
        }

        if (step1PitcherGuideUI != null)
        {
            var txt = step1PitcherGuideUI.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (txt != null) txt.text = step1GrabRingLabel;
            step1PitcherGuideUI.SetActive(false);
        }

        StartCoroutine(TransitionToStep2Routine());
    }

    private IEnumerator TransitionToStep2Routine()
    {
        isTransitioningStep = true;
        PlaySound(stepCompleteClip);

        if (holographicGuide != null)
        {
            yield return holographicGuide.FadeOutInstruction(0.42f);
        }
        else
        {
            yield return new WaitForSeconds(0.42f);
        }

        if (step1PitcherGuideUI != null) step1PitcherGuideUI.SetActive(false);

        yield return new WaitForSeconds(0.15f);
        SetupStep2();
    }

    // ══════════════════════════════════════════════════════════════════
    // STEP 2: UI BUTTON INTERACTION WITH SAMPLE PANEL (TRIGGER BUTTON)
    // ══════════════════════════════════════════════════════════════════

    public void SetupStep2()
    {
        currentState = TutorialState.Step2_ButtonInteraction;
        step2ButtonClicked = false;
        isTransitioningStep = false;

        // Ensure demoTool doesn't intercept UI pointer ray
        if (demoTool != null)
        {
            demoTool.enabled = false;
            demoTool.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var col in demoTool.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            }
        }

        HideAllStepGuideUIsExcept(step2SampleButtonGuideUI);
        if (step1PitcherGuideUI != null) step1PitcherGuideUI.SetActive(false);

        if (sampleButtonPanel != null)
        {
            sampleButtonPanel.SetActive(true);
            if (sampleButtonCanvasGroup != null) sampleButtonCanvasGroup.alpha = 1f;
        }

        if (holographicGuide != null)
        {
            Transform target = sampleActionButton != null ? sampleActionButton.transform : (sampleButtonPanel != null ? sampleButtonPanel.transform : null);
            bool hasSampleRing = step2SampleButtonGuideUI != null;
            Transform ringUI = hasSampleRing ? step2SampleButtonGuideUI.transform : ringPosition_UIButton;
            bool showRing = ringUI != null;

            holographicGuide.ShowInstruction(
                HolographicButtonType.Trigger,
                step2TriggerCalloutText,
                step2TriggerCalloutText,
                "TRIG",
                target,
                step2TriggerRingLabel,
                new Vector3(0f, 0.02f, 0f),
                showLaserRay: true,
                showTargetRing: showRing,
                ringAnchor: ringUI,
                showRingCircles: showRing,
                preserveRingHierarchyText: true
            );
        }
    }

    public void OnSampleButtonClicked()
    {
        if (currentState != TutorialState.Step2_ButtonInteraction || step2ButtonClicked || isTransitioningStep) return;
        step2ButtonClicked = true;

        if (sampleButtonLabel != null && !string.IsNullOrEmpty(step2ButtonConfirmedText))
        {
            sampleButtonLabel.text = step2ButtonConfirmedText;
        }

        StartCoroutine(TransitionToStep3Routine());
    }

    private IEnumerator TransitionToStep3Routine()
    {
        isTransitioningStep = true;
        PlaySound(stepCompleteClip);

        if (holographicGuide != null)
        {
            holographicGuide.FadeOutInstruction(0.42f);
        }

        if (sampleButtonCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < 0.42f)
            {
                elapsed += Time.deltaTime;
                sampleButtonCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / 0.42f));
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.42f);
        }

        if (sampleButtonPanel != null) sampleButtonPanel.SetActive(false);

        yield return new WaitForSeconds(0.15f);
        SetupStep3();
    }

    // ══════════════════════════════════════════════════════════════════
    // STEP 3: COTTON BALL & DUAL-FORCEPS TRANSFER (BUTTON A)
    // ══════════════════════════════════════════════════════════════════

    public void SetupStep3()
    {
        currentState = TutorialState.Step3_ForcepsTransfer;
        step3SubPhase = 0;
        isTransitioningStep = false;
        hasDisposedCottonInTrash = false;
        step3MissedTrashElapsed = 0f;

        // Ensure demoTool doesn't intercept rays aimed at forceps on Table 3
        if (demoTool != null)
        {
            demoTool.enabled = false;
            demoTool.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var col in demoTool.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            }
        }

        ShowStep3SubPhase0_GrabPickupForceps();
    }

    public void ShowStep3SubPhase0_GrabPickupForceps()
    {
        step3SubPhase = 0;
        HideAllStepGuideUIsExcept(step3APickupForcepsGuideUI);

        if (holographicGuide != null)
        {
            Transform target = pickupForceps != null ? pickupForceps.transform : null;
            Transform ringUI = step3APickupForcepsGuideUI != null ? step3APickupForcepsGuideUI.transform : ringPosition_PickupForceps;

            holographicGuide.ShowInstruction(
                HolographicButtonType.Grip,
                step3A_PickupForcepsCallout,
                step3A_PickupForcepsCallout,
                "GRIP",
                target,
                step3A_PickupForcepsRingLabel,
                new Vector3(0f, 0.08f, -0.02f),
                showLaserRay: false,
                showTargetRing: true,
                ringAnchor: ringUI,
                showRingCircles: true,
                preserveRingHierarchyText: true
            );
        }
    }

    public void ShowStep3SubPhase1_DipInIodineJar()
    {
        ShowStep3SubPhase1_DipInIodineJar(missedTrash: false);
    }

    private void ShowStep3SubPhase1_DipInIodineJar(bool missedTrash)
    {
        step3SubPhase = 1;
        hasDisposedCottonInTrash = false;
        step3MissedTrashElapsed = 0f;
        PlaySound(stepCompleteClip);
        HideAllStepGuideUIsExcept(step3BIodineJarGuideUI);

        if (holographicGuide != null)
        {
            Transform target = antisepticJarTransform != null ? antisepticJarTransform : null;
            Transform ringUI = step3BIodineJarGuideUI != null ? step3BIodineJarGuideUI.transform : ringPosition_IodineJar;
            string callout = missedTrash
                ? "<color=#EF4444>MISSED TRASH BIN!</color>\n" + step3B_DipJarCallout
                : step3B_DipJarCallout;

            holographicGuide.UpdateTargetAndCallout(
                HolographicButtonType.ButtonA,
                callout,
                callout,
                "BTN A",
                target,
                step3B_DipJarRingLabel,
                new Vector3(0f, 0.14f, 0f),
                showLaserRay: false,
                showTargetRing: true,
                ringAnchor: ringUI,
                showRingCircles: true,
                preserveRingHierarchyText: true
            );
        }
    }

    public void ShowStep3SubPhase2_GrabHandlingForceps()
    {
        step3SubPhase = 2;
        step3HandlingHoldTimer = 0f;
        step3HandlingAlreadyHeldAtStart = IsForcepsHeld(handlingForceps);
        PlaySound(stepCompleteClip);
        HideAllStepGuideUIsExcept(step3CHandlingForcepsGuideUI);

        if (holographicGuide != null)
        {
            Transform target = handlingForceps != null ? handlingForceps.transform : null;
            Transform ringUI = step3CHandlingForcepsGuideUI != null ? step3CHandlingForcepsGuideUI.transform : ringPosition_TransferCotton;

            holographicGuide.UpdateTargetAndCallout(
                HolographicButtonType.Grip,
                step3C_HandlingForcepsCallout,
                step3C_HandlingForcepsCallout,
                "GRIP",
                target,
                step3C_HandlingForcepsRingLabel,
                new Vector3(0f, 0.08f, -0.02f),
                showLaserRay: false,
                showTargetRing: true,
                ringAnchor: ringUI,
                showRingCircles: true,
                preserveRingHierarchyText: true
            );
        }
    }

    public void ShowStep3SubPhase3_TransferCotton()
    {
        step3SubPhase = 3;
        PlaySound(stepCompleteClip);
        HideAllStepGuideUIsExcept(step3DTransferCottonGuideUI);

        if (holographicGuide != null)
        {
            Transform target = handlingForceps != null ? handlingForceps.transform : null;
            Transform ringUI = step3DTransferCottonGuideUI != null ? step3DTransferCottonGuideUI.transform : ringPosition_TransferCotton;

            holographicGuide.UpdateTargetAndCallout(
                HolographicButtonType.ButtonA,
                step3D_TransferCottonCallout,
                step3D_TransferCottonCallout,
                "BTN A",
                target,
                step3D_TransferCottonRingLabel,
                new Vector3(0f, 0.10f, -0.02f),
                showLaserRay: false,
                showTargetRing: true,
                ringAnchor: ringUI,
                showRingCircles: true,
                preserveRingHierarchyText: true
            );
        }
    }

    public void ShowStep3SubPhase4_DisposeInTrashBin()
    {
        step3SubPhase = 4;
        hasDisposedCottonInTrash = false;
        step3MissedTrashElapsed = 0f;
        PlaySound(stepCompleteClip);
        HideAllStepGuideUIsExcept(step3ETrashBasinGuideUI);

        if (holographicGuide != null)
        {
            Transform target = wasteBasinTransform != null ? wasteBasinTransform : null;
            Transform ringUI = step3ETrashBasinGuideUI != null ? step3ETrashBasinGuideUI.transform : ringPosition_TrashBasin;

            holographicGuide.UpdateTargetAndCallout(
                HolographicButtonType.ButtonA,
                step3E_TrashDisposeCallout,
                step3E_TrashDisposeCallout,
                "BTN A",
                target,
                step3E_TrashDisposeRingLabel,
                new Vector3(0f, 0.22f, 0f),
                showLaserRay: false,
                showTargetRing: true,
                ringAnchor: ringUI,
                showRingCircles: true,
                preserveRingHierarchyText: true
            );
        }
    }

    private void Update()
    {
        if (currentState != TutorialState.Step3_ForcepsTransfer || isTransitioningStep) return;

        if (step3SubPhase == 0)
        {
            if (IsForcepsHeld(pickupForceps))
            {
                ShowStep3SubPhase1_DipInIodineJar();
            }
        }
        else if (step3SubPhase == 1)
        {
            if (pickupForceps != null && pickupForceps.HasCotton)
            {
                ShowStep3SubPhase2_GrabHandlingForceps();
            }
            else if (!IsForcepsHeld(pickupForceps))
            {
                ShowStep3SubPhase0_GrabPickupForceps();
            }
        }
        else if (step3SubPhase == 2)
        {
            if (handlingForceps != null && handlingForceps.HasCotton)
            {
                ShowStep3SubPhase4_DisposeInTrashBin();
            }
            else if (pickupForceps != null && !pickupForceps.HasCotton)
            {
                if (!IsForcepsHeld(pickupForceps))
                    ShowStep3SubPhase0_GrabPickupForceps();
                else
                    ShowStep3SubPhase1_DipInIodineJar();
            }
            else if (IsForcepsHeld(handlingForceps) && IsForcepsHeld(pickupForceps))
            {
                step3HandlingHoldTimer += Time.deltaTime;
                float requiredHoldTime = step3HandlingAlreadyHeldAtStart ? 0.65f : 0.15f;
                if (step3HandlingHoldTimer >= requiredHoldTime)
                {
                    ShowStep3SubPhase3_TransferCotton();
                }
            }
            else
            {
                step3HandlingHoldTimer = 0f;
            }
        }
        else if (step3SubPhase == 3)
        {
            if (handlingForceps != null && handlingForceps.HasCotton)
            {
                ShowStep3SubPhase4_DisposeInTrashBin();
            }
            else if (pickupForceps != null && !pickupForceps.HasCotton)
            {
                if (!IsForcepsHeld(pickupForceps))
                    ShowStep3SubPhase0_GrabPickupForceps();
                else
                    ShowStep3SubPhase1_DipInIodineJar();
            }
            else if (!IsForcepsHeld(handlingForceps))
            {
                ShowStep3SubPhase2_GrabHandlingForceps();
            }
        }
        else if (step3SubPhase == 4)
        {
            if (hasDisposedCottonInTrash)
            {
                StartCoroutine(CompleteTutorialRoutine());
            }
            else if (handlingForceps != null && !handlingForceps.HasCotton)
            {
                // Allow a brief fall grace window after releasing [A] above the trash rim before requiring re-dip
                step3MissedTrashElapsed += Time.deltaTime;
                if (step3MissedTrashElapsed >= Step3MissedTrashFallGraceSeconds)
                {
                    HandleStep3MissedTrashDisposal();
                }
            }
            else
            {
                step3MissedTrashElapsed = 0f;
            }
        }
    }

    private void HandleStep3MissedTrashDisposal()
    {
        hasDisposedCottonInTrash = false;
        step3MissedTrashElapsed = 0f;

        if (!IsForcepsHeld(pickupForceps))
        {
            ShowStep3SubPhase0_GrabPickupForceps();
        }
        else if (pickupForceps != null && pickupForceps.HasCotton)
        {
            ShowStep3SubPhase2_GrabHandlingForceps();
        }
        else
        {
            ShowStep3SubPhase1_DipInIodineJar(missedTrash: true);
        }
    }

    private bool IsForcepsHeld(ForcepsController fc)
    {
        if (fc == null) return false;
        if (fc.forcepsGrabInteractable != null) return fc.forcepsGrabInteractable.isSelected;
        var grab = fc.GetComponent<XRGrabInteractable>();
        return grab != null && grab.isSelected;
    }

    // ══════════════════════════════════════════════════════════════════
    // STEP 4: COMPLETION
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator CompleteTutorialRoutine()
    {
        isTransitioningStep = true;
        currentState = TutorialState.Step4_Completed;
        PlaySound(tutorialCompleteClip != null ? tutorialCompleteClip : stepCompleteClip);

        if (holographicGuide != null)
        {
            yield return holographicGuide.FadeOutInstruction(0.40f);
        }

        HideAllStepGuideUIsExcept(null);

        if (completionCard != null)
        {
            completionCard.SetActive(true);
        }
    }

    public void OnProceedToMainSceneClicked()
    {
        PlaySound(stepCompleteClip);
        Debug.Log("[TutorialManager] Loading MainScene (Delivery Room)...");
        VRSceneTransitionFader.FadeToScene("MainScene");
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
