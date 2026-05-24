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

        // WE HAVE REMOVED THE GRAB LISTENER. 
        // The tool no longer triggers any validation when picked up.
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