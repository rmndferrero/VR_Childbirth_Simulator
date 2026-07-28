using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs; // Added for ActionBasedController haptics

public class SocketValidator : MonoBehaviour
{
    public SimulationStep requiredStep;
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

        // Check if it matches the current expected step ID
        if (tool.toolID == requiredStep.expectedID)
        {
            hasBeenCompleted = true;

            tool.MarkCorrect();
            VRDemoGameManager.Instance.ReportCorrectAction(requiredStep);
            VRDemoGameManager.Instance.AdvanceStep();
        }
        else
        {
            isRejecting = true;

            // Trigger scoring penalty using the safe 'tool.toolID'
            VRDemoGameManager.Instance.RecordMistake(tool.toolID);

            // Trigger Haptics
            var controller = args.interactorObject.transform.GetComponent<ActionBasedController>();
            if (controller != null)
            {
                controller.SendHapticImpulse(0.5f, 0.2f);
            }

            // Reject item from socket
            socket.interactionManager.SelectExit(socket, args.interactableObject);

            // Show warning
            VRDemoGameManager.Instance.ShowWarning(requiredStep.outOfSequenceWarning);

            // Small delay before allowing validation again
            Invoke(nameof(ResetRejectState), 0.5f);
        }
    }

    private void ResetRejectState()
    {
        isRejecting = false;
    }
}