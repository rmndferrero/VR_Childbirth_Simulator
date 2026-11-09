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
///   Step 3: Picking up an antiseptic cotton ball with Pickup Forceps, transferring it to Handling Forceps with Button A, and discarding it.
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

    [Header("Step 1: Grab Mechanic References")]
    [Tooltip("Target grabbable object on the table (e.g. Pitcher on Table 3).")]
    public XRGrabInteractable demoTool;

    [Header("Step 2: Sample UI Button Panel References")]
    [Tooltip("Floating sample panel used to teach VR ray pointing & trigger clicking.")]
    public GameObject sampleButtonPanel;
    public CanvasGroup sampleButtonCanvasGroup;
    public Button sampleActionButton;
    public TextMeshProUGUI sampleButtonLabel;

    [Header("Step 3: Cotton Ball & Dual-Forceps Transfer References")]
    public ForcepsController pickupForceps;
    public ForcepsController handlingForceps;
    public Transform antisepticJarTransform;
    public Transform wasteBasinTransform;
    public WasteBasin wasteBasin;

    [Header("Step 4: Completion & Navigation UI")]
    public GameObject completionCard;
    public Button proceedToMainSceneButton;
    public Button skipTutorialButton;

    [Header("Optional Legacy Guide References")]
    public TutorialStepGuide station1Guide;
    public TutorialStepGuide station2Guide;

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip stepCompleteClip;
    public AudioClip tutorialCompleteClip;

    private bool step1ToolGrabbed = false;
    private bool step1ToolReleased = false;
    private bool step2ButtonClicked = false;

    private int step3SubPhase = 0; // 0 = pick cotton from jar, 1 = transfer to handling forceps, 2 = release cotton
    private bool isTransitioningStep = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

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

    private void Start()
    {
        AutoFindMissingReferences();
        SetupStep1();
    }

    private void AutoFindMissingReferences()
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
            var trashGo = GameObject.Find("TrashCan");
            if (trashGo != null) wasteBasinTransform = trashGo.transform;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // STEP 1: GRAB MECHANIC (HOLD GRIP BUTTON TO PICK UP PITCHER)
    // ══════════════════════════════════════════════════════════════════

    private void SetupStep1()
    {
        currentState = TutorialState.Step1_GrabTool;
        step1ToolGrabbed = false;
        step1ToolReleased = false;
        isTransitioningStep = false;

        if (sampleButtonPanel != null) sampleButtonPanel.SetActive(false);
        if (completionCard != null) completionCard.SetActive(false);
        if (station1Guide != null) station1Guide.gameObject.SetActive(false);
        if (station2Guide != null) station2Guide.gameObject.SetActive(false);

        if (demoTool != null)
        {
            demoTool.selectEntered.RemoveListener(OnDemoToolGrabbed);
            demoTool.selectExited.RemoveListener(OnDemoToolReleased);
            demoTool.selectEntered.AddListener(OnDemoToolGrabbed);
            demoTool.selectExited.AddListener(OnDemoToolReleased);
        }

        if (holographicGuide != null)
        {
            Transform target = demoTool != null ? demoTool.transform : null;
            holographicGuide.ShowInstruction(
                HolographicButtonType.Grip,
                "HOLD [GRAB] BUTTON\nTO PICK UP",
                "HOLD [GRAB] BUTTON\nTO PICK UP",
                "GRIP",
                target,
                "GRAB PITCHER",
                new Vector3(0f, 0.08f, 0f),
                showLaserRay: false
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
            Transform target = demoTool != null ? demoTool.transform : null;
            holographicGuide.UpdateTargetAndCallout(
                HolographicButtonType.Grip,
                "<color=#34D399>✓ PITCHER GRABBED!</color>\nRELEASE [GRAB] TO SET DOWN",
                "RELEASE [GRAB] BUTTON\nTO PLACE ON TABLE",
                "GRIP",
                target,
                "RELEASE TO PLACE DOWN",
                new Vector3(0f, 0.08f, 0f)
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

        yield return new WaitForSeconds(0.15f);
        SetupStep2();
    }

    // ══════════════════════════════════════════════════════════════════
    // STEP 2: UI BUTTON INTERACTION WITH SAMPLE PANEL (TRIGGER BUTTON)
    // ══════════════════════════════════════════════════════════════════

    private void SetupStep2()
    {
        currentState = TutorialState.Step2_ButtonInteraction;
        step2ButtonClicked = false;
        isTransitioningStep = false;

        if (sampleButtonPanel != null)
        {
            sampleButtonPanel.SetActive(true);
            if (sampleButtonCanvasGroup != null) sampleButtonCanvasGroup.alpha = 1f;
        }

        if (sampleButtonLabel != null)
        {
            sampleButtonLabel.text = "PRESS [TRIGGER] TO CONFIRM";
        }

        if (holographicGuide != null)
        {
            Transform target = sampleActionButton != null ? sampleActionButton.transform : (sampleButtonPanel != null ? sampleButtonPanel.transform : null);
            holographicGuide.ShowInstruction(
                HolographicButtonType.Trigger,
                "POINT & PRESS [TRIGGER]\nBUTTON TO CLICK UI",
                "PRESS [TRIGGER] BUTTON\nON SAMPLE PANEL",
                "TRIG",
                target,
                "CLICK SAMPLE BUTTON",
                new Vector3(0f, 0.02f, 0f),
                showLaserRay: true
            );
        }
    }

    public void OnSampleButtonClicked()
    {
        if (currentState != TutorialState.Step2_ButtonInteraction || step2ButtonClicked || isTransitioningStep) return;
        step2ButtonClicked = true;

        if (sampleButtonLabel != null)
        {
            sampleButtonLabel.text = "<color=#34D399>✓ CONFIRMED!</color>";
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

        // Smoothly fade out the sample UI panel at the same time
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

    private void SetupStep3()
    {
        currentState = TutorialState.Step3_ForcepsTransfer;
        step3SubPhase = 0;
        isTransitioningStep = false;

        ShowStep3SubPhase0();
    }

    private void ShowStep3SubPhase0()
    {
        step3SubPhase = 0;
        if (holographicGuide != null)
        {
            Transform target = antisepticJarTransform != null
                ? antisepticJarTransform
                : (pickupForceps != null ? pickupForceps.transform : null);

            holographicGuide.ShowInstruction(
                HolographicButtonType.ButtonA,
                "1. GRAB PICKUP FORCEPS &\nDIP IN IODINE JAR (HOLD [A])",
                "HOLD [A] IN JAR TO\nPICK COTTON BALL",
                "BTN A",
                target,
                "DIP PICKUP FORCEPS & HOLD [A]",
                new Vector3(0f, 0.14f, 0f),
                showLaserRay: false
            );
        }
    }

    private void ShowStep3SubPhase1()
    {
        step3SubPhase = 1;
        PlaySound(stepCompleteClip);

        if (holographicGuide != null)
        {
            Transform target = handlingForceps != null ? handlingForceps.transform : null;
            holographicGuide.UpdateTargetAndCallout(
                HolographicButtonType.ButtonA,
                "2. GRAB HANDLING FORCEPS,\nBRING TIPS CLOSE & HOLD [A]",
                "PRESS & HOLD [A] TO\nTRANSFER COTTON BALL",
                "BTN A",
                target,
                "BRING FORCEPS CLOSE & HOLD [A]",
                new Vector3(0f, 0.12f, 0f)
            );
        }
    }

    private void ShowStep3SubPhase2()
    {
        step3SubPhase = 2;
        PlaySound(stepCompleteClip);

        if (holographicGuide != null)
        {
            Transform target = wasteBasinTransform != null
                ? wasteBasinTransform
                : (handlingForceps != null ? handlingForceps.transform : null);

            holographicGuide.UpdateTargetAndCallout(
                HolographicButtonType.ButtonA,
                "<color=#34D399>✓ COTTON TRANSFERRED!</color>\n3. RELEASE [A] OVER BASIN",
                "RELEASE [A] BUTTON\nTO DISCARD COTTON",
                "BTN A",
                target,
                "RELEASE [A] OVER TRASH BASIN",
                new Vector3(0f, 0.22f, 0f)
            );
        }
    }

    private void Update()
    {
        if (currentState != TutorialState.Step3_ForcepsTransfer || isTransitioningStep) return;

        if (step3SubPhase == 0)
        {
            // Sub-phase 0 -> 1: Player picked up a cotton ball from the jar with Pickup Forceps
            if (pickupForceps != null && pickupForceps.HasCotton)
            {
                ShowStep3SubPhase1();
            }
        }
        else if (step3SubPhase == 1)
        {
            // Sub-phase 1 -> 2: Player transferred the cotton ball to Handling Forceps using Button A
            if (handlingForceps != null && handlingForceps.HasCotton)
            {
                ShowStep3SubPhase2();
            }
            // If cotton was accidentally dropped from Pickup Forceps before transfer, guide them back to Sub-phase 0
            else if (pickupForceps != null && !pickupForceps.HasCotton)
            {
                ShowStep3SubPhase0();
            }
        }
        else if (step3SubPhase == 2)
        {
            // Sub-phase 2 -> Complete: Player released Button A to discard the cotton ball from Handling Forceps
            if (handlingForceps != null && !handlingForceps.HasCotton)
            {
                StartCoroutine(CompleteTutorialRoutine());
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // STEP 4: TUTORIAL COMPLETION & TRANSITION TO MAIN SCENE
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator CompleteTutorialRoutine()
    {
        isTransitioningStep = true;
        currentState = TutorialState.Step4_Completed;
        PlaySound(tutorialCompleteClip != null ? tutorialCompleteClip : stepCompleteClip);

        if (holographicGuide != null)
        {
            yield return holographicGuide.FadeOutInstruction(0.45f);
        }
        else
        {
            yield return new WaitForSeconds(0.45f);
        }

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
