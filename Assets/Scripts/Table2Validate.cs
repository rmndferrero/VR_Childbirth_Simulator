using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public class Table2SocketValidator : MonoBehaviour
{
    private XRSocketInteractor socket;

    private void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
    }

    private void OnEnable()
    {
        socket.selectEntered.AddListener(OnToolEnteredSocket);
    }

    private void OnDisable()
    {
        socket.selectEntered.RemoveListener(OnToolEnteredSocket);
    }

    private void OnToolEnteredSocket(SelectEnterEventArgs args)
    {
        var tool = args.interactableObject.transform.GetComponent<ToolSnapback>();
        if (tool == null) return;

        bool isCorrectOrder = CheckOrderWithYourScoringSystem(tool);

        if (isCorrectOrder)
        {
            // Update Table 2 as its new home and cancel snapback timer
            tool.SaveOrigin(transform.position, transform.rotation);
        }
        else
        {
            // Wrong order: Force drop and snap back to Table 1
            socket.interactionManager.SelectExit(socket, args.interactableObject);
            tool.SnapBack();
        }
    }

    private bool CheckOrderWithYourScoringSystem(ToolSnapback tool)
    {
        // ⚠️ IMPORTANT: Replace 'true' or 'false' with your actual scoring check!
        // Example:
        // return YourScoringManager.Instance.ValidateStep(tool.gameObject.name);

        return true; // Set to true for testing if correct
    }
}