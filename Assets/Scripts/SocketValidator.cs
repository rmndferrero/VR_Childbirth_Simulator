using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Attached to each XRSocketInteractor on Table 2.
/// Checks if the placed tool matches the current expected step.
///   - Correct: green flash, advance scoring.
///   - Wrong:   red flash, penalty, haptics, eject back to Table 1.
/// </summary>
public class SocketValidator : MonoBehaviour
{
    private XRSocketInteractor socket;
    private bool hasBeenCompleted = false;
    private bool isRejecting = false;

    [Header("Rejection Settings")]
    [Tooltip("How long the wrong tool stays red on Table 2 before returning to Table 1.")]
    public float stayRedDuration = 0.8f;

    private void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        if (socket != null)
            socket.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDestroy()
    {
        if (socket != null)
            socket.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isRejecting || hasBeenCompleted) return;

        // Find ToolItem anywhere in the interactable hierarchy
        var tool = args.interactableObject.transform.GetComponent<ToolItem>()
                ?? args.interactableObject.transform.GetComponentInParent<ToolItem>()
                ?? args.interactableObject.transform.GetComponentInChildren<ToolItem>();

        if (tool == null)
        {
            // Not a tool — kick it out
            if (socket != null && socket.interactionManager != null && args.interactableObject != null)
                socket.interactionManager.SelectExit(socket, args.interactableObject);
            return;
        }

        // Get expected vs placed IDs
        string expectedID = (VRDemoGameManager.Instance != null && VRDemoGameManager.Instance.currentStep != null)
            ? VRDemoGameManager.Instance.currentStep.expectedID.Trim()
            : "";
        string placedID = (tool.toolID != null) ? tool.toolID.Trim() : "";

        if (placedID.Equals(expectedID, System.StringComparison.OrdinalIgnoreCase))
        {
            // ── CORRECT ──
            hasBeenCompleted = true;
            tool.MarkCorrect(args.interactableObject.transform.position, args.interactableObject.transform.rotation);

            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.ReportCorrectAction(VRDemoGameManager.Instance.currentStep);
                VRDemoGameManager.Instance.AdvanceStep();
            }
        }
        else
        {
            // ── WRONG ORDER ──
            isRejecting = true;

            // Scoring: penalty + warning
            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordMistake(placedID);
                VRDemoGameManager.Instance.ShowWarning(placedID);
            }

            // Haptics
            var inputInteractor = args.interactorObject as XRBaseInputInteractor
                ?? args.interactorObject?.transform.GetComponent<XRBaseInputInteractor>();
            if (inputInteractor != null)
            {
                inputInteractor.SendHapticImpulse(0.5f, 0.2f);
            }
            else
            {
                var ctrl = args.interactorObject?.transform.GetComponent<XRBaseController>();
                if (ctrl != null) ctrl.SendHapticImpulse(0.5f, 0.2f);
            }

            // Red flash → eject → warp to Table 1 (handled inside ToolItem)
            tool.HandleWrongPlacement(socket, stayRedDuration);

            Invoke(nameof(ResetReject), stayRedDuration + 0.5f);
        }
    }

    private void ResetReject() { isRejecting = false; }
}