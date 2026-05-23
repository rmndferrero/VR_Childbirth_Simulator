using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs; // Added for ActionBasedController haptics

public class SocketValidator : MonoBehaviour
{
    public SimulationStep requiredStep;
    private XRSocketInteractor socket;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.selectEntered.AddListener(OnSelectEntered);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        var tool = args.interactableObject.transform.GetComponent<ToolItem>();

        if (tool != null && tool.toolID == requiredStep.expectedID)
        {
            tool.MarkCorrect();
            VRDemoGameManager.Instance.ReportCorrectAction(requiredStep);
        }
        else
        {
            // Trigger Haptics
            var controller = args.interactorObject.transform.GetComponent<ActionBasedController>();
            if (controller != null)
            {
                controller.SendHapticImpulse(0.5f, 0.2f);
            }

            // Reject the item from the socket so it drops
            socket.interactionManager.SelectExit(socket, args.interactableObject);

            // Show UI Warning
            VRDemoGameManager.Instance.ShowWarning(requiredStep.outOfSequenceWarning);
        }
    }
}