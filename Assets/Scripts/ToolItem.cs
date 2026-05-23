using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class ToolItem : MonoBehaviour
{
    public string toolID; // Matches expectedID in ScriptableObject
    private Renderer rend;
    private Color originalColor;
    private XRGrabInteractable grab;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
            originalColor = rend.material.color;

        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrab);
    }

    private void OnDestroy()
    {
        if (grab != null)
            grab.selectEntered.RemoveListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        // Use the Singleton instance instead of a hardcoded reference
        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.CheckTool(this);
        }
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