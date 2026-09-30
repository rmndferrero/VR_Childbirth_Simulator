using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// 1. Spawns the VR player's HEAD/EYES exactly where the 'Player' GameObject and Blue Ball are in the Scene,
///    compensating for room-scale VR guardian offsets and physical standing/sitting differences.
/// 2. Continuously enforces target eye height in LateUpdate so XROrigin cannot overwrite Camera Offset.
/// 3. Keeps the CharacterController base firmly on the floor (Y = 0) to eliminate locomotion pop.
/// 4. Configures the Player CharacterController to pass through tables/props while colliding with walls and floor.
/// </summary>
[RequireComponent(typeof(XROrigin))]
public class XRPlayerSpawnAligner : MonoBehaviour
{
    public static XRPlayerSpawnAligner Instance { get; private set; }

    [Header("Spawn Position Mode")]
    [Tooltip("If TRUE (Default): The VR player spawns at the EXACT position and rotation where you place the 'Player' in the Scene!")]
    public bool spawnAtCurrentScenePosition = true;

    [Tooltip("Optional custom spawn point transform. Used only when spawnAtCurrentScenePosition is false.")]
    public Transform customSpawnPoint;

    [Header("Player Height / Eye-Level Adjustment")]
    [Tooltip("Target eye height in meters. Default: 1.42m (optimal height for patient delivery table). Standing adult is ~1.65m.")]
    [Range(0.8f, 2.2f)]
    public float eyeHeight = 1.42f;

    [Tooltip("Additional vertical height offset in meters (+ raises player higher, - lowers player).")]
    public float heightOffset = 0.0f;

    [Tooltip("If true, automatically sizes the CharacterController collider to match the player height.")]
    public bool autoAdjustColliderHeight = true;

    [Header("Collision Filtering")]
    [Tooltip("If true, the player passes through all tables, carts, mother, and props, colliding ONLY with walls and floor.")]
    public bool environmentOnlyCollisions = true;

    private XROrigin xrOrigin;
    private CharacterController characterController;

    private Vector3 initialScenePosition;
    private Quaternion initialSceneRotation;
    private bool hasCapturedSceneTransform = false;

    private float calibratedHeadOffsetY = 0.0f;
    private bool hasCalibratedHeight = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        xrOrigin = GetComponent<XROrigin>();
        characterController = GetComponent<CharacterController>();

        CaptureSceneTransform();
        ApplyHeightSettings();
        AlignPlayerToSpawn();

        if (environmentOnlyCollisions)
        {
            SetupEnvironmentOnlyCollisions();
        }
    }

    private void CaptureSceneTransform()
    {
        if (!hasCapturedSceneTransform)
        {
            initialScenePosition = transform.position;
            initialSceneRotation = transform.rotation;
            hasCapturedSceneTransform = true;
        }
    }

    private void Start()
    {
        ApplyHeightSettings();
        AlignPlayerToSpawn();

        if (environmentOnlyCollisions)
        {
            SetupEnvironmentOnlyCollisions();
        }
    }

    private void OnEnable()
    {
        StartCoroutine(MultiPassAlignmentRoutine());
    }

    private void OnValidate()
    {
        if (xrOrigin == null) xrOrigin = GetComponent<XROrigin>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        hasCalibratedHeight = false;
        ApplyHeightSettings();
        RecalibrateHeightOffset();
    }

    /// <summary>
    /// Applies eyeHeight and heightOffset to XROrigin and CharacterController.
    /// </summary>
    public void ApplyHeightSettings()
    {
        if (xrOrigin == null) xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin == null) return;

        float desiredHeight = Mathf.Max(0.5f, eyeHeight + heightOffset);

        // 1. Update XROrigin CameraYOffset (used in Device mode or editor simulation)
        xrOrigin.CameraYOffset = desiredHeight;

        // 2. Update CharacterController collider height & center so base sits at Y = 0
        if (autoAdjustColliderHeight && characterController != null)
        {
            characterController.height = desiredHeight + 0.1f;
            characterController.center = new Vector3(0, (desiredHeight + 0.1f) * 0.5f, 0);
        }
    }

    public void RecalibrateHeightOffset()
    {
        if (xrOrigin == null) xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin == null || xrOrigin.Camera == null) return;

        float desiredHeight = Mathf.Max(0.5f, eyeHeight + heightOffset);
        float physicalHeadY = xrOrigin.Camera.transform.localPosition.y;

        // In VR Floor mode, physicalHeadY is your real physical height above ground (e.g. 1.75m).
        if (physicalHeadY > 0.1f)
        {
            calibratedHeadOffsetY = desiredHeight - physicalHeadY;
        }
        else
        {
            calibratedHeadOffsetY = 0.0f;
            xrOrigin.CameraYOffset = desiredHeight;
        }

        hasCalibratedHeight = true;
        ApplyCameraFloorOffset();
    }

    /// <summary>
    /// LateUpdate runs AFTER XROrigin.Update(), ensuring XROrigin cannot overwrite CameraFloorOffset.
    /// </summary>
    private void LateUpdate()
    {
        ApplyCameraFloorOffset();
    }

    private void ApplyCameraFloorOffset()
    {
        if (xrOrigin == null || xrOrigin.CameraFloorOffsetObject == null) return;

        float desiredHeight = Mathf.Max(0.5f, eyeHeight + heightOffset);

        if (xrOrigin.RequestedTrackingOriginMode == XROrigin.TrackingOriginMode.Device)
        {
            xrOrigin.CameraYOffset = desiredHeight;
            Vector3 pos = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
            pos.y = desiredHeight;
            xrOrigin.CameraFloorOffsetObject.transform.localPosition = pos;
        }
        else
        {
            if (!hasCalibratedHeight)
            {
                RecalibrateHeightOffset();
            }

            Vector3 pos = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
            pos.y = calibratedHeadOffsetY;
            xrOrigin.CameraFloorOffsetObject.transform.localPosition = pos;
        }
    }

    private IEnumerator MultiPassAlignmentRoutine()
    {
        // Wait for OpenXR / VR runtime tracking frames to initialize
        yield return null;
        RecalibrateHeightOffset();
        AlignPlayerToSpawn();
        ApplyHeightSettings();
        if (environmentOnlyCollisions) SetupEnvironmentOnlyCollisions();

        yield return new WaitForSeconds(0.05f);
        RecalibrateHeightOffset();
        AlignPlayerToSpawn();
        ApplyHeightSettings();
        if (environmentOnlyCollisions) SetupEnvironmentOnlyCollisions();

        yield return new WaitForSeconds(0.2f);
        RecalibrateHeightOffset();
        AlignPlayerToSpawn();
        ApplyHeightSettings();
    }

    private void Update()
    {
        // Abyss safety: only reset if player falls far through the map below -10m
        if (transform.position.y < -10.0f)
        {
            AlignPlayerToSpawn();
        }

        // Press 'R' on keyboard in Editor to instantly recenter/re-align to spawn and recalibrate height
        if (Application.isEditor)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
            {
                hasCalibratedHeight = false;
                RecalibrateHeightOffset();
                AlignPlayerToSpawn();
                ApplyHeightSettings();
            }
        }
    }

    /// <summary>
    /// Configures the CharacterController so it passes freely through all tables,
    /// carts, bed, mother, and props, while retaining solid collision with walls and floor.
    /// </summary>
    public void SetupEnvironmentOnlyCollisions()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (characterController == null) return;

        var allColliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var env = GameObject.Find("ENVIRONMENT");
        var envColliders = env != null ? new HashSet<Collider>(env.GetComponentsInChildren<Collider>(true)) : new HashSet<Collider>();

        foreach (var col in allColliders)
        {
            if (col == null || col == characterController) continue;

            string colName = col.gameObject.name.ToLower();
            bool isFloorOrWall = envColliders.Contains(col) || colName.Contains("floor") || colName.Contains("plane") || colName.Contains("ground") || colName.Contains("wall");

            if (isFloorOrWall)
            {
                // Solid barrier: Floor, walls, and ceiling
                Physics.IgnoreCollision(characterController, col, false);
            }
            else
            {
                // Ghost passthrough: tables, carts, bed, mother, props, instruments
                Physics.IgnoreCollision(characterController, col, true);
            }
        }
    }

    public static void IgnoreCollisionWithPlayer(Collider col)
    {
        if (Instance != null && Instance.characterController != null && col != null)
        {
            Physics.IgnoreCollision(Instance.characterController, col, true);
        }
    }

    /// <summary>
    /// Repositions the XR Origin so the player's HEAD / EYES land exactly at the desired target world position (the Blue Ball)
    /// and look direction, while keeping the CharacterController firmly grounded on the floor (Y = 0) to eliminate locomotion pop.
    /// </summary>
    public void AlignPlayerToSpawn()
    {
        if (xrOrigin == null) xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin == null) return;

        Vector3 targetHeadPos = GetTargetHeadWorldPosition();
        Vector3 targetForward = GetTargetForwardDirection();

        targetForward.y = 0; // Keep horizon level
        if (targetForward.sqrMagnitude < 0.001f) targetForward = Vector3.forward;
        targetForward.Normalize();

        if (characterController != null) characterController.enabled = false;

        RecalibrateHeightOffset();

        if (xrOrigin.Camera != null)
        {
            // 1. Rotate the XR Origin rig so the player's view matches target forward
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, targetForward);

            // 2. Align horizontal (X/Z) position so camera lands at targetHeadPos.x/z while keeping rig base at Y = 0
            Vector3 camWorldPos = xrOrigin.Camera.transform.position;
            Vector3 deltaXZ = new Vector3(targetHeadPos.x - camWorldPos.x, 0, targetHeadPos.z - camWorldPos.z);
            transform.position = new Vector3(transform.position.x + deltaXZ.x, 0.0f, transform.position.z + deltaXZ.z);
        }
        else
        {
            transform.position = new Vector3(targetHeadPos.x, 0.0f, targetHeadPos.z);
            transform.rotation = Quaternion.LookRotation(targetForward, Vector3.up);
        }

        // 3. Ground CharacterController cleanly at Y = 0
        if (characterController != null)
        {
            float desiredHeight = eyeHeight + heightOffset;
            characterController.height = desiredHeight + 0.1f;
            characterController.center = new Vector3(0, (desiredHeight + 0.1f) * 0.5f, 0);
            characterController.enabled = true;
        }
    }

    public Vector3 GetTargetHeadWorldPosition()
    {
        Vector3 basePos;
        if (spawnAtCurrentScenePosition)
        {
            CaptureSceneTransform();
            basePos = initialScenePosition;
        }
        else if (customSpawnPoint != null)
        {
            return customSpawnPoint.position;
        }
        else
        {
            basePos = transform.position;
        }

        return new Vector3(basePos.x, eyeHeight + heightOffset, basePos.z);
    }

    public Vector3 GetTargetForwardDirection()
    {
        if (spawnAtCurrentScenePosition)
        {
            CaptureSceneTransform();
            return initialSceneRotation * Vector3.forward;
        }
        else if (customSpawnPoint != null)
        {
            return customSpawnPoint.forward;
        }
        else
        {
            return transform.forward;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Sync Spawn Point To Current Player Transform")]
    public void SyncSpawnPointToCurrentPlayer()
    {
        if (customSpawnPoint != null)
        {
            UnityEditor.Undo.RecordObject(customSpawnPoint, "Sync Spawn Point to Player");
            customSpawnPoint.position = GetTargetHeadWorldPosition();
            customSpawnPoint.rotation = Quaternion.LookRotation(GetTargetForwardDirection(), Vector3.up);
            UnityEditor.EditorUtility.SetDirty(customSpawnPoint.gameObject);
            Debug.Log($"[XRPlayerSpawnAligner] Synced customSpawnPoint ({customSpawnPoint.name}) to Player at {customSpawnPoint.position}, Facing {transform.eulerAngles.y:F1}°");
        }
    }

    [ContextMenu("Set Height to Delivery / Seated (1.42m)")]
    public void SetHeightDelivery()
    {
        eyeHeight = 1.42f;
        heightOffset = 0.0f;
        hasCalibratedHeight = false;
        ApplyHeightSettings();
        RecalibrateHeightOffset();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Set Height to Standing Adult (1.65m)")]
    public void SetHeightStanding()
    {
        eyeHeight = 1.65f;
        heightOffset = 0.0f;
        hasCalibratedHeight = false;
        ApplyHeightSettings();
        RecalibrateHeightOffset();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Vector3 headPos = GetTargetHeadWorldPosition();
        Vector3 spawnBase = new Vector3(headPos.x, 0, headPos.z);
        Vector3 fwd = GetTargetForwardDirection();
        fwd.y = 0;
        if (fwd.sqrMagnitude > 0.001f) fwd.Normalize();

        Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.85f);
        Gizmos.DrawWireSphere(headPos, 0.12f);
        Gizmos.DrawLine(spawnBase, headPos);
        Gizmos.DrawRay(headPos, fwd * 0.5f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(headPos + Vector3.up * 0.15f, $"[Player Head Target: {(eyeHeight + heightOffset):F2}m]");
#endif
    }
}
