using UnityEngine;

public enum SliderVisualState
{
    Active,
    Upcoming,
    Completed,
    Mistake
}

/// <summary>
/// Attached to each of the 9 3D on-skin stroke slider GameObjects in the scene.
/// Allows the user to select, move, and adjust the Start and End points directly in the Scene View.
/// Handles track line rendering, fill progression, direction arrows, mistake color transitions, and antiseptic puddle activation.
/// </summary>
[ExecuteInEditMode]
public class StrokeSliderTrack : MonoBehaviour
{
    [Header("Stroke Identification")]
    [Tooltip("Index from 0 to 8 corresponding to Strokes 1 through 9.")]
    public int strokeIndex = 0;
    public string strokeName = "Stroke 1";
    [Tooltip("If true, this landmark is a direct tap target (e.g. Stroke 9 Anus).")]
    public bool isTapOnly = false;

    [Header("Endpoints (Move these in Scene View to adjust the path)")]
    public Transform startPointTransform;
    public Transform endPointTransform;

    [Header("Visual Components")]
    public LineRenderer trackLine;
    public LineRenderer fillLine;
    public GameObject startNode;
    public GameObject endNode;
    public GameObject directionArrow;
    public GameObject handleBead;

    [Header("Antiseptic Puddle Layer")]
    public WashableIodinePuddle puddleLayer;

    [Header("Materials")]
    public Material activeMaterial;
    public Material upcomingMaterial;
    public Material completedMaterial;
    public Material mistakeMaterial;

    [Header("Editor Display Settings")]
    [Tooltip("When true in Edit Mode, displays the slider gizmos and lines in Scene View for easy positioning.")]
    public bool showInEditor = true;
    public Color gizmoColor = new Color(0.15f, 0.75f, 1f, 0.9f);

    private void OnValidate()
    {
        UpdateVisualPositions();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && showInEditor)
        {
            UpdateVisualPositions();
        }
#endif
    }

    public Vector3 GetStartWorldPosition()
    {
        if (startPointTransform != null) return startPointTransform.position;
        return transform.position;
    }

    public Vector3 GetEndWorldPosition()
    {
        if (endPointTransform != null) return endPointTransform.position;
        return transform.position + transform.forward * 0.05f;
    }

    /// <summary>
    /// Synchronizes the line renderers, nodes, and arrow positions with the Start/End transforms.
    /// </summary>
    public void UpdateVisualPositions()
    {
        Vector3 startPos = GetStartWorldPosition();
        Vector3 endPos = GetEndWorldPosition();

        if (startNode != null) startNode.transform.position = startPos;
        if (endNode != null) endNode.transform.position = endPos;

        if (isTapOnly)
        {
            if (trackLine != null) trackLine.enabled = false;
            if (fillLine != null) fillLine.enabled = false;
            if (endNode != null) endNode.SetActive(false);
            if (directionArrow != null) directionArrow.SetActive(false);
            if (handleBead != null) handleBead.SetActive(false);
            return;
        }

        // Update Track Line
        if (trackLine != null)
        {
            trackLine.enabled = true;
            trackLine.useWorldSpace = true;
            trackLine.positionCount = 2;
            trackLine.SetPosition(0, startPos);
            trackLine.SetPosition(1, endPos);
        }

        // Update Direction Arrow (midpoint pointing from start to end)
        if (directionArrow != null)
        {
            directionArrow.SetActive(true);
            directionArrow.transform.position = Vector3.Lerp(startPos, endPos, 0.5f);
            Vector3 dir = (endPos - startPos).normalized;
            if (dir != Vector3.zero)
            {
                directionArrow.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }

    public SliderVisualState CurrentVisualState { get; private set; } = (SliderVisualState)(-1);

    /// <summary>
    /// Sets the visual presentation according to the slider's state:
    /// - Active: Glowing Bright Sky Blue with live handle and fill line (Hint mode).
    /// - Upcoming: Subtle translucent blue roadmap line (Default no-hint mode).
    /// - Completed: Calm subtle emerald green line.
    /// - Mistake: Flashing Glowing Neon Red.
    /// </summary>
    public void SetVisualState(SliderVisualState state, bool force = false)
    {
        if (!force && CurrentVisualState == state) return;
        CurrentVisualState = state;

        Material targetMat = activeMaterial;

        switch (state)
        {
            case SliderVisualState.Active:
                targetMat = activeMaterial;
                if (directionArrow != null) directionArrow.SetActive(!isTapOnly);
                if (handleBead != null) handleBead.SetActive(true);
                if (trackLine != null)
                {
                    trackLine.startWidth = 0.015f;
                    trackLine.endWidth = 0.015f;
                }
                break;

            case SliderVisualState.Upcoming:
                targetMat = upcomingMaterial != null ? upcomingMaterial : activeMaterial;
                if (fillLine != null) fillLine.enabled = false;
                if (handleBead != null) handleBead.SetActive(false);
                if (directionArrow != null) directionArrow.SetActive(!isTapOnly);
                if (trackLine != null)
                {
                    trackLine.startWidth = 0.009f;
                    trackLine.endWidth = 0.009f;
                }
                break;

            case SliderVisualState.Completed:
                targetMat = completedMaterial != null ? completedMaterial : activeMaterial;
                if (fillLine != null) fillLine.enabled = false;
                if (handleBead != null) handleBead.SetActive(false);
                if (directionArrow != null) directionArrow.SetActive(false);
                if (trackLine != null)
                {
                    trackLine.startWidth = 0.007f;
                    trackLine.endWidth = 0.007f;
                }
                break;

            case SliderVisualState.Mistake:
                targetMat = mistakeMaterial != null ? mistakeMaterial : activeMaterial;
                if (startNode != null) startNode.SetActive(true);
                if (endNode != null) endNode.SetActive(!isTapOnly);
                if (directionArrow != null) directionArrow.SetActive(!isTapOnly);
                if (handleBead != null) handleBead.SetActive(true);
                if (trackLine != null)
                {
                    trackLine.enabled = !isTapOnly;
                    trackLine.startWidth = 0.016f;
                    trackLine.endWidth = 0.016f;
                }
                break;
        }

        ApplyMaterial(targetMat);
    }

    /// <summary>
    /// Enables or disables all 3D line renderers, nodes, arrows, and beads on this slider track.
    /// </summary>
    public void SetVisualElementsVisible(bool visible)
    {
        if (trackLine != null) trackLine.enabled = visible && !isTapOnly;
        if (fillLine != null) fillLine.enabled = visible && !isTapOnly;
        if (startNode != null) startNode.SetActive(visible);
        if (endNode != null) endNode.SetActive(visible && !isTapOnly);
        if (directionArrow != null) directionArrow.SetActive(visible && !isTapOnly);
        if (handleBead != null) handleBead.SetActive(visible && !isTapOnly);
    }

    /// <summary>
    /// Updates real-time wipe progress (0% to 100%) as the cotton slides down.
    /// </summary>
    public void UpdateProgress(float progress, Vector3 handlePos)
    {
        Vector3 startPos = GetStartWorldPosition();

        if (fillLine != null)
        {
            fillLine.enabled = (progress > 0.01f);
            fillLine.useWorldSpace = true;
            fillLine.positionCount = 2;
            fillLine.SetPosition(0, startPos);
            fillLine.SetPosition(1, handlePos);
        }

        if (handleBead != null)
        {
            handleBead.SetActive(true);
            handleBead.transform.position = handlePos;
        }
    }

    public void ResetProgress()
    {
        Vector3 startPos = GetStartWorldPosition();

        if (fillLine != null)
        {
            fillLine.enabled = false;
            fillLine.SetPosition(0, startPos);
            fillLine.SetPosition(1, startPos);
        }

        if (handleBead != null)
        {
            handleBead.transform.position = startPos;
        }
    }

    public void ApplyMaterial(Material mat)
    {
        if (mat == null) return;

        if (trackLine != null) trackLine.sharedMaterial = mat;
        if (fillLine != null) fillLine.sharedMaterial = mat;

        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mr in renderers)
        {
            // Don't overwrite puddle materials
            if (mr != null && !mr.name.StartsWith("Puddle") && !mr.name.StartsWith("Coating"))
            {
                mr.sharedMaterial = mat;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showInEditor) return;

        Vector3 startPos = GetStartWorldPosition();
        Vector3 endPos = GetEndWorldPosition();

        Gizmos.color = gizmoColor;

        if (isTapOnly)
        {
            Gizmos.DrawWireSphere(startPos, 0.025f);
            Gizmos.DrawSphere(startPos, 0.012f);
        }
        else
        {
            Gizmos.DrawLine(startPos, endPos);
            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.9f);
            Gizmos.DrawSphere(startPos, 0.010f);
            Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.9f);
            Gizmos.DrawSphere(endPos, 0.010f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * 0.015f, $"[{strokeIndex + 1}] {strokeName}");
#endif
    }
}
