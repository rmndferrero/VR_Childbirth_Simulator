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
        var tool = args.interactableObject.transform.GetComponent<ToolItem>();

        if (isRejecting || hasBeenCompleted)
            return;

        if (!hasBeenCompleted && tool != null && tool.toolID == requiredStep.expectedID)
        {
            hasBeenCompleted = true;

            tool.MarkCorrect();
            VRDemoGameManager.Instance.ReportCorrectAction(requiredStep);
            // Tell the GameManager to move to the next step or finish the phase
            VRDemoGameManager.Instance.AdvanceStep();
        }
        else
        {
            isRejecting = true;

            // Trigger scoring penalty
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