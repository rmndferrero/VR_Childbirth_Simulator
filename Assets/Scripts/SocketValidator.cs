using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class SocketValidator : MonoBehaviour
{
    private XRSocketInteractor socket;
    private bool hasBeenCompleted = false;
    private bool isRejecting = false;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.selectEntered.AddListener(OnSelectEntered);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isRejecting || hasBeenCompleted)
            return;

        // Safely get the ToolItem component from the incoming object
        var tool = args.interactableObject.transform.GetComponent<ToolItem>();

        // If the object doesn't even have a ToolItem script, ignore it entirely
        if (tool == null)
        {
            Debug.LogWarning("An object without a ToolItem script was placed in the socket.");
            return;
        }

        // 1. Ask the Game Manager what tool we are currently supposed to be placing
        string currentlyExpectedID = VRDemoGameManager.Instance.currentStep.expectedID.Trim();
        string placedToolID = tool.toolID.Trim();

        // 2. Compare the placed tool to the expected linear step
        if (placedToolID == currentlyExpectedID)
        {
            // Right tool at the right time!
            hasBeenCompleted = true;

            tool.MarkCorrect();
            
            // Pass the current step to the manager for the success message
            VRDemoGameManager.Instance.ReportCorrectAction(VRDemoGameManager.Instance.currentStep);
            
            // Advance the linear sequence to the next tool!
            VRDemoGameManager.Instance.AdvanceStep();
        }
        else
        {
            // The tool fits the socket (layer mask), but it's the WRONG TIME in the sequence.
            isRejecting = true;

            // Trigger scoring penalty for out-of-sequence placement
            VRDemoGameManager.Instance.RecordMistake(placedToolID);

            // Trigger Haptics so the player feels the rejection
            var controller = args.interactorObject.transform.GetComponent<ActionBasedController>();
            if (controller != null)
            {
                controller.SendHapticImpulse(0.5f, 0.2f);
            }

            // Force the socket to drop the item back out
            socket.interactionManager.SelectExit(socket, args.interactableObject);

            // Show the specific out-of-sequence warning from the CURRENT step
            VRDemoGameManager.Instance.ShowWarning(VRDemoGameManager.Instance.currentStep.outOfSequenceWarning);

            // Small delay before allowing validation again
            Invoke(nameof(ResetRejectState), 0.5f);
        }
    }

    private void ResetRejectState()
    {
        isRejecting = false;
    }
}