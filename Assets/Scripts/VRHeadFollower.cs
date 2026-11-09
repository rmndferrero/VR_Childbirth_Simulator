using UnityEngine;

/// <summary>
/// Smoothly positions and rotates a floating VR UI canvas in front of the player's head.
/// Uses smooth damping and spherical interpolation (lazy billboarding) so the canvas stays
/// comfortably in view without the jitter or motion sickness caused by rigid camera attachment.
/// </summary>
public class VRHeadFollower : MonoBehaviour
{
    [Header("Distance & Height")]
    [Tooltip("Distance in meters from the player's eyes to the canvas.")]
    public float targetDistance = 1.35f;

    [Tooltip("Vertical offset relative to eye level.")]
    public float heightOffset = -0.05f;

    [Header("Follow Dynamics")]
    [Tooltip("How fast the UI catches up with player rotation and movement (higher = snappier, lower = smoother).")]
    public float followSpeed = 6.0f;

    [Tooltip("Minimum angular difference in degrees before the UI begins turning to face the user.")]
    public float angleDeadzone = 12f;

    [Tooltip("If true, UI will only rotate along the horizontal Y axis (keeps horizon level).")]
    public bool lockRollAndPitch = true;

    private Camera targetCamera;
    private Vector3 currentVelocity;

    private void Start()
    {
        targetCamera = Camera.main;
        SnapToTargetImmediate();
    }

    public void SnapToTargetImmediate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        Vector3 fwd = targetCamera.transform.forward;
        if (lockRollAndPitch)
        {
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
        }

        Vector3 targetPos = targetCamera.transform.position + fwd * targetDistance + Vector3.up * heightOffset;
        transform.position = targetPos;
        transform.rotation = Quaternion.LookRotation(fwd);
    }

    private void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        Vector3 camPos = targetCamera.transform.position;
        Vector3 camFwd = targetCamera.transform.forward;

        if (lockRollAndPitch)
        {
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 0.001f) camFwd = Vector3.forward;
            camFwd.Normalize();
        }

        Vector3 targetPos = camPos + camFwd * targetDistance + Vector3.up * heightOffset;

        // Smooth position follow
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, 1f / followSpeed);

        // Calculate rotation facing user
        Vector3 dirToCam = transform.position - camPos;
        if (lockRollAndPitch) dirToCam.y = 0f;

        if (dirToCam.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToCam);
            float angleDiff = Quaternion.Angle(transform.rotation, targetRot);

            if (angleDiff > angleDeadzone)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followSpeed);
            }
        }
    }
}
