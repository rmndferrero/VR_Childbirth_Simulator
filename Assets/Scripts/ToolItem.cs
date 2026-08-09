using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ToolItem : MonoBehaviour
{
    public string toolID; // Matches expectedID in the Socket's ScriptableObject
    private XRGrabInteractable grab;
    private Rigidbody rb;

    private Renderer[] allRenderers;
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

    private Vector3 table1Position;
    private Quaternion table1Rotation;
    private Vector3 currentOriginPosition;
    private Quaternion currentOriginRotation;
    private bool table1OriginSaved = false;

    void Awake()
    {
        // Store original colors for all renderers/materials in object hierarchy
        allRenderers = GetComponentsInChildren<Renderer>();
        foreach (var r in allRenderers)
        {
            if (r != null && r.materials != null)
            {
                Color[] colors = new Color[r.materials.Length];
                for (int i = 0; i < r.materials.Length; i++)
                {
                    var mat = r.materials[i];
                    if (mat == null) continue;

                    if (mat.HasProperty("_BaseColor"))
                        colors[i] = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color"))
                        colors[i] = mat.color;
                    else
                        colors[i] = Color.white;
                }
                originalColors[r] = colors;
            }
        }

        grab = GetComponent<XRGrabInteractable>() ?? GetComponentInParent<XRGrabInteractable>() ?? GetComponentInChildren<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>() ?? GetComponentInParent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();

        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
        }
    }

    void Start()
    {
        if (!table1OriginSaved)
        {
            SaveTable1Origin();
        }
    }

    public void SaveTable1Origin()
    {
        table1Position = transform.position;
        table1Rotation = transform.rotation;
        currentOriginPosition = table1Position;
        currentOriginRotation = table1Rotation;
        table1OriginSaved = true;

        var snapback = GetComponent<ToolSnapback>() ?? GetComponentInParent<ToolSnapback>() ?? GetComponentInChildren<ToolSnapback>();
        if (snapback != null)
        {
            snapback.ResetToTable1();
        }
    }

    public void SaveTable2Origin(Vector3 pos, Quaternion rot)
    {
        currentOriginPosition = pos;
        currentOriginRotation = rot;

        var snapback = GetComponent<ToolSnapback>() ?? GetComponentInParent<ToolSnapback>() ?? GetComponentInChildren<ToolSnapback>();
        if (snapback != null)
        {
            snapback.SaveTable2Origin(pos, rot);
        }
    }

    public void ResetOriginToTable1()
    {
        currentOriginPosition = table1Position;
        currentOriginRotation = table1Rotation;

        var snapback = GetComponent<ToolSnapback>() ?? GetComponentInParent<ToolSnapback>() ?? GetComponentInChildren<ToolSnapback>();
        if (snapback != null)
        {
            snapback.ResetToTable1();
        }
    }

    public void ReturnToOrigin(XRInteractionManager interactionManager = null)
    {
        ResetOriginToTable1();

        if (grab != null && interactionManager != null)
        {
            var interactors = new List<IXRSelectInteractor>(grab.interactorsSelecting);
            foreach (var interactor in interactors)
            {
                interactionManager.SelectExit(interactor, grab);
            }
        }

        StartCoroutine(SnapToOriginRoutine());
    }

    private IEnumerator SnapToOriginRoutine()
    {
        yield return new WaitForEndOfFrame();

        var snapback = GetComponent<ToolSnapback>() ?? GetComponentInParent<ToolSnapback>() ?? GetComponentInChildren<ToolSnapback>();
        if (snapback != null)
        {
            snapback.SnapBack();
        }

        transform.position = table1Position;
        transform.rotation = table1Rotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();

        Invoke(nameof(ResetTool), 0.6f);
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
        SetToolColor(new Color(0.3f, 1f, 0.3f, 1f)); // Vivid green across all renderers
    }

    public void MarkWrong()
    {
        SetToolColor(new Color(1f, 0.2f, 0.2f, 1f)); // Vivid red across all renderers
    }

    private void SetToolColor(Color targetColor)
    {
        if (allRenderers == null) return;

        foreach (var r in allRenderers)
        {
            if (r == null || r.materials == null) continue;
            foreach (var mat in r.materials)
            {
                if (mat == null) continue;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", targetColor);
                if (mat.HasProperty("_Color"))
                    mat.color = targetColor;
            }
        }
    }

    public void ResetTool()
    {
        if (allRenderers == null) return;

        foreach (var r in allRenderers)
        {
            if (r == null || r.materials == null) continue;
            if (originalColors.TryGetValue(r, out Color[] colors))
            {
                for (int i = 0; i < r.materials.Length && i < colors.Length; i++)
                {
                    var mat = r.materials[i];
                    if (mat == null) continue;
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", colors[i]);
                    if (mat.HasProperty("_Color"))
                        mat.color = colors[i];
                }
            }
        }
    }
}