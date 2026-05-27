using UnityEngine;

public class LazyFollowHUD : MonoBehaviour
{
    [Header("Tracking Settings")]
    [Tooltip("Drag your Main Camera (XR head) here")]
    public Transform targetCamera;

    [Tooltip("How far away the UI floats (in meters)")]
    public float distance = 0.8f;

    [Tooltip("How fast the UI catches up to the head")]
    public float followSpeed = 5f;

    [Tooltip("Offset relative to the camera (e.g., slightly down)")]
    public Vector3 offset = new Vector3(0, -0.2f, 0);

    void LateUpdate()
    {
        if (targetCamera == null) return;

        // 1. Calculate where the UI SHOULD be
        Vector3 targetPosition = targetCamera.position + (targetCamera.forward * distance);

        // Apply local offset (so it floats slightly below eye level so it doesn't block vision)
        targetPosition += targetCamera.right * offset.x + targetCamera.up * offset.y;

        // 2. Smoothly glide to that position
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);

        // 3. Smoothly rotate to face exactly the same direction as the camera
        transform.rotation = Quaternion.Slerp(transform.rotation, targetCamera.rotation, Time.deltaTime * followSpeed);
    }
}