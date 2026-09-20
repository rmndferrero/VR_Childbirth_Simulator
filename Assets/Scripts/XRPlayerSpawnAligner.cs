using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// 1. Automatically aligns the XR player's head camera to the desired starting position
///    and rotation in the scene on game start.
/// 2. Grounds the XR Origin base firmly at floor level (Y = 0) so the player never falls through the floor on PC or VR.
/// 3. Configures the Player CharacterController to pass through everything (tables, carts,
///    mother, tools, props) while retaining solid collision with walls and floor.
/// </summary>
[RequireComponent(typeof(XROrigin))]
public class XRPlayerSpawnAligner : MonoBehaviour
{
    public static XRPlayerSpawnAligner Instance { get; private set; }

    [Header("Target Starting Point")]
    [Tooltip("Target world position where the player's head/eyes should start. If null, uses defaultHeadWorldPosition.")]
    public Transform customSpawnPoint;

    [Tooltip("Default eye-level world position in front of the delivery table.")]
    public Vector3 defaultHeadWorldPosition = new Vector3(0.0f, 1.42f, 1.18f);

    [Tooltip("Default forward look direction.")]
    public Vector3 defaultForwardDirection = new Vector3(-0.95f, -0.15f, 0.25f);

    [Header("Collision Filtering")]
    [Tooltip("If true, the player passes through all tables, carts, mother, and props, colliding ONLY with walls and floor.")]
    public bool environmentOnlyCollisions = true;

    private XROrigin xrOrigin;
    private CharacterController characterController;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        xrOrigin = GetComponent<XROrigin>();
        characterController = GetComponent<CharacterController>();

        AlignPlayerToSpawn();
        if (environmentOnlyCollisions)
        {
            SetupEnvironmentOnlyCollisions();
        }
    }

    private void Start()
    {
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

    private IEnumerator MultiPassAlignmentRoutine()
    {
        AlignPlayerToSpawn();
        if (environmentOnlyCollisions) SetupEnvironmentOnlyCollisions();
        yield return new WaitForSeconds(0.05f);
        AlignPlayerToSpawn();
        if (environmentOnlyCollisions) SetupEnvironmentOnlyCollisions();
        yield return new WaitForSeconds(0.2f);
        AlignPlayerToSpawn();
    }

    private void Update()
    {
        // Safety floor lock: keep XR Origin base on the floor
        if (transform.position.y < -0.1f)
        {
            Vector3 pos = transform.position;
            pos.y = 0.0f;
            transform.position = pos;
        }

        // Press 'R' on keyboard in Editor to instantly recenter/re-align to spawn
        if (Application.isEditor)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
            {
                AlignPlayerToSpawn();
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
    /// Repositions the XR Origin so the player's base sits firmly on the floor at Y = 0,
    /// aligned horizontally and facing the delivery bed.
    /// </summary>
    public void AlignPlayerToSpawn()
    {
        if (xrOrigin == null) xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin == null || xrOrigin.Camera == null) return;

        Vector3 targetPos = (customSpawnPoint != null) ? customSpawnPoint.position : defaultHeadWorldPosition;
        Vector3 targetForward = (customSpawnPoint != null) ? customSpawnPoint.forward : defaultForwardDirection;

        // 1. Firmly place the XR Origin base on the floor at Y = 0
        transform.position = new Vector3(targetPos.x, 0.0f, targetPos.z);

        // 2. Align forward direction towards the patient
        targetForward.y = 0; // Keep horizon level
        if (targetForward.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(targetForward.normalized, Vector3.up);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 targetPos = (customSpawnPoint != null) ? customSpawnPoint.position : defaultHeadWorldPosition;
        Vector3 targetForward = (customSpawnPoint != null) ? customSpawnPoint.forward : defaultForwardDirection;
        targetForward.y = 0;
        if (targetForward.sqrMagnitude > 0.001f) targetForward.Normalize();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPos, 0.15f);
        Gizmos.DrawRay(targetPos, targetForward * 0.6f);
    }
}
