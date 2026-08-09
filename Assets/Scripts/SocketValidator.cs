using System.Collections;
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

    [Header("Rejection Delay")]
    [Tooltip("Delay in seconds that the wrong item stays red on Table 2 before being dropped back to Table 1.")]
    public float rejectionDelay = 0.8f;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.selectEntered.AddListener(OnSelectEntered);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isRejecting || hasBeenCompleted) return;

        var tool = args.interactableObject.transform.GetComponent<ToolItem>() ??
                   args.interactableObject.transform.GetComponentInParent<ToolItem>() ??
                   args.interactableObject.transform.GetComponentInChildren<ToolItem>();

        if (tool == null)
        {
            if (socket != null && socket.interactionManager != null && args.interactableObject != null)
            {
                socket.interactionManager.SelectExit(socket, args.interactableObject);
            }
            return;
        }

        string currentlyExpectedID = VRDemoGameManager.Instance != null && VRDemoGameManager.Instance.currentStep != null
            ? VRDemoGameManager.Instance.currentStep.expectedID.Trim()
            : "";

        string placedToolID = tool.toolID != null ? tool.toolID.Trim() : "";

        if (placedToolID.Equals(currentlyExpectedID, System.StringComparison.OrdinalIgnoreCase))
        {
            hasBeenCompleted = true;
            tool.MarkCorrect();
            tool.SaveTable2Origin(args.interactableObject.transform.position, args.interactableObject.transform.rotation);

            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.ReportCorrectAction(VRDemoGameManager.Instance.currentStep);
                VRDemoGameManager.Instance.AdvanceStep();
            }
        }
        else
        {
            isRejecting = true;

            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordMistake(placedToolID);
                VRDemoGameManager.Instance.ShowWarning(placedToolID);
            }

            var inputInteractor = args.interactorObject as XRBaseInputInteractor ?? args.interactorObject?.transform.GetComponent<XRBaseInputInteractor>();
            if (inputInteractor != null)
            {
                inputInteractor.SendHapticImpulse(0.5f, 0.2f);
            }
            else
            {
                var controller = args.interactorObject?.transform.GetComponent<XRBaseController>();
                if (controller != null) controller.SendHapticImpulse(0.5f, 0.2f);
            }

            // Turn red immediately on Table 2
            tool.MarkWrong();

            // Wait for rejectionDelay (0.8s) so player sees red tool on Table 2, then kick it off back to Table 1
            StartCoroutine(RejectAndSnapBackRoutine(tool));
        }
    }

    private IEnumerator RejectAndSnapBackRoutine(ToolItem tool)
    {
        yield return new WaitForSeconds(rejectionDelay);

        if (tool != null)
        {
            tool.ReturnToOrigin(socket != null ? socket.interactionManager : null);
        }

        isRejecting = false;
    }
}