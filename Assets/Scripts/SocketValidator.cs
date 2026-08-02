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
        if (isRejecting || hasBeenCompleted) return;

        var tool = args.interactableObject.transform.GetComponent<ToolItem>();
        if (tool == null) return;

        string currentlyExpectedID = VRDemoGameManager.Instance.currentStep.expectedID.Trim();
        string placedToolID = tool.toolID.Trim();

        if (placedToolID == currentlyExpectedID)
        {
            hasBeenCompleted = true;
            tool.MarkCorrect();
            VRDemoGameManager.Instance.ReportCorrectAction(VRDemoGameManager.Instance.currentStep);
            VRDemoGameManager.Instance.AdvanceStep();
        }
        else
        {
            isRejecting = true;
            VRDemoGameManager.Instance.RecordMistake(placedToolID);

            var controller = args.interactorObject.transform.GetComponent<ActionBasedController>();
            if (controller != null) controller.SendHapticImpulse(0.5f, 0.2f);

            socket.interactionManager.SelectExit(socket, args.interactableObject);

            // Pass the specific wrong ID to the manager to fetch the tailored matrix warning
            VRDemoGameManager.Instance.ShowWarning(placedToolID);

            Invoke(nameof(ResetRejectState), 0.5f);
        }
    }

    private void ResetRejectState() { isRejecting = false; }
}