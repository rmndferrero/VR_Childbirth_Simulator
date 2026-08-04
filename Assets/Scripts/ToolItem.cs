using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class ToolItem : MonoBehaviour
{
    public string toolID; // Matches expectedID in the Socket's ScriptableObject
    private Renderer rend;
    private Color originalColor;
    private XRGrabInteractable grab;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
            originalColor = rend.material.color;

        grab = GetComponent<XRGrabInteractable>();

        // Grab detection: reports to the GameManager whenever this tool is picked up,
        // so held-tool hazards (e.g. scalpel/curette during Dialogue) can be evaluated
        // via the Global Hazard Matrix, independent of socket placement.
        grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnDestroy()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (VRDemoGameManager.Instance != null)
            VRDemoGameManager.Instance.CheckHeldToolHazard(toolID);
    }

    public void MarkCorrect()
    {
        if (rend != null)
            rend.material.color = new Color(0.4f, 1f, 0.4f); // green metallic
    }

    public void MarkWrong()
    {
        if (rend != null)
            rend.material.color = new Color(1f, 0.4f, 0.4f); // red metallic
    }

    public void ResetTool()
    {
        if (rend != null)
            rend.material.color = originalColor;
    }
}