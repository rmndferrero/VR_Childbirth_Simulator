using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[System.Serializable]
public class DrapeSlot
{
    public string slotName = "Drape Zone";
    public Transform targetPoint;
    public GameObject ghostIndicatorObject;       // Mesh ghost hover indicator
    public GameObject placeholderGreenDrape;      // Placed green visual drape mesh (stays permanently)
    public GameObject final3DModelPrefabOrObject; // Swappable slot for future 3D drape models
    [HideInInspector] public bool isDraped = false;
    [HideInInspector] public Coroutine mistakeFlashCo = null;
}

/// <summary>
/// Controls the sterile draping sequence on the mother and Mayo Stand:
/// 1. Under Buttocks -> 2. Abdominal -> 3/4. Left or Right Leg (either order) -> 5. Mayo Stand.
/// Features:
/// - Uniform blue holographic guides across unplaced zones.
/// - Permanent placed 3D drapes (Adjusted_Drapes) on correct placement.
/// - Flashes glowing RED if placed on an out-of-order zone.
/// - Spawns fresh linen on Table 1 after every step from master cached template.
/// </summary>
public class MotherDrapingManager : MonoBehaviour
{
    public static MotherDrapingManager Instance { get; private set; }

    [Header("Draping Slots (4-Step Sequence)")]
    public List<DrapeSlot> drapeSlots = new List<DrapeSlot>();

    [Header("Visual Materials")]
    public Material ghostMaterial;         // Active target cyan guide
    public Material inactiveGuideMaterial; // Upcoming soft blue guide
    public Material mistakeRedMaterial;    // Mistake red flash

    [Header("Linen Spawning (1 Drape Per Zone)")]
    [Tooltip("Prefab or template used to spawn fresh dry linen on Table 1 for each subsequent draping step.")]
    public GameObject dryLinenTemplate;
    public Transform table1LinenParent;
    public Vector3 table1LinenLocalPosition = new Vector3(0.67f, 0.72f, 8.925f);
    public Quaternion table1LinenLocalRotation = Quaternion.Euler(-90f, 0f, 8.036f);
    public Vector3 table1LinenLocalScale = new Vector3(0.88247573f, 0.61450624f, 0.15847997f);

    [Header("State")]
    public int currentTargetIndex = 0;
    public bool isDrapingActive = false;
    public bool isDrapingComplete = false;

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip drapePlaceAudio;
    public AudioClip drapeMistakeAudio;

    public event Action<int, DrapeSlot> OnDrapePlaced;
    public event Action OnAllDrapesCompleted;

    private float lastMistakeWarningTime = 0f;
    private GameObject cachedLinenTemplate = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        LoadAssetsIfNull();
        ResolveAdjustedDrapeModelsIfNeeded();
        CacheInitialLinenTransform();
    }

    private void LoadAssetsIfNull()
    {
        if (ghostMaterial == null)
        {
#if UNITY_EDITOR
            ghostMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Active_Mat.mat")
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Drape_Ghost_Mat.mat");
#endif
        }

        if (inactiveGuideMaterial == null)
        {
#if UNITY_EDITOR
            inactiveGuideMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Inactive_Mat.mat")
                                 ?? ghostMaterial;
#endif
        }

        if (mistakeRedMaterial == null)
        {
#if UNITY_EDITOR
            mistakeRedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Red_Mistake_Mat.mat");
#endif
        }
    }

    private void CacheInitialLinenTransform()
    {
        if (dryLinenTemplate == null)
        {
            var linen = GameObject.Find("Dry_Linen");
            if (linen != null) dryLinenTemplate = linen;
        }

        if (dryLinenTemplate != null)
        {
            if (dryLinenTemplate.transform.parent != null)
            {
                table1LinenParent = dryLinenTemplate.transform.parent;
                table1LinenLocalPosition = dryLinenTemplate.transform.localPosition;
                table1LinenLocalRotation = dryLinenTemplate.transform.localRotation;
                table1LinenLocalScale = dryLinenTemplate.transform.localScale;
            }
            else
            {
                var table1 = GameObject.Find("Table 1") ?? GameObject.Find("Table1");
                if (table1 != null) table1LinenParent = table1.transform;
            }

            // Create an inactive master template clone that is never destroyed
            if (cachedLinenTemplate == null)
            {
                cachedLinenTemplate = Instantiate(dryLinenTemplate, table1LinenParent);
                cachedLinenTemplate.name = "Dry_Linen_Master_Template";
                cachedLinenTemplate.transform.localPosition = table1LinenLocalPosition;
                cachedLinenTemplate.transform.localRotation = table1LinenLocalRotation;
                cachedLinenTemplate.transform.localScale = table1LinenLocalScale;
                cachedLinenTemplate.SetActive(false);
            }
        }
    }

    [Header("Custom Transform Alignment")]
    [Tooltip("If true, resets zones to default hardcoded coordinates on start. Leave false to preserve your custom transforms in the Scene/Hierarchy.")]
    public bool autoAlignToDefaults = false;

    private void Start()
    {
        if (autoAlignToDefaults)
        {
            AlignAndFixDrapeSlotVisuals();
        }
        ResolveAdjustedDrapeModelsIfNeeded();
        HideAllDrapesAndGhosts();
    }

    /// <summary>
    /// Optional default alignment helper. Only executed if autoAlignToDefaults is true.
    /// </summary>
    public void AlignAndFixDrapeSlotVisuals()
    {
        Vector3[] worldPositions = new Vector3[]
        {
            new Vector3(-1.06f, 0.90f, 1.50f),  // 1. Under Buttocks
            new Vector3(-1.22f, 1.11f, 1.50f),  // 2. Abdomen
            new Vector3(-0.68f, 1.18f, 1.18f),  // 3. Left Thigh
            new Vector3(-0.72f, 1.18f, 1.82f)   // 4. Right Thigh
        };

        Quaternion[] worldRotations = new Quaternion[]
        {
            Quaternion.Euler(0f, 90f, 0f),       // 1. Flat under sacrum/buttocks
            Quaternion.Euler(15f, 90f, 0f),      // 2. Conforming to lower abdomen
            Quaternion.Euler(12f, 65f, -20f),    // 3. Conforming over left thigh
            Quaternion.Euler(-12f, 115f, 20f)    // 4. Conforming over right thigh
        };

        Vector3[] worldScales = new Vector3[]
        {
            new Vector3(0.48f, 0.008f, 0.36f),   // 1. Under Buttocks
            new Vector3(0.46f, 0.008f, 0.32f),   // 2. Abdomen
            new Vector3(0.34f, 0.008f, 0.44f),   // 3. Left Thigh
            new Vector3(0.34f, 0.008f, 0.44f)    // 4. Right Thigh
        };

        for (int i = 0; i < drapeSlots.Count && i < worldPositions.Length; i++)
        {
            var slot = drapeSlots[i];
            if (slot.targetPoint != null)
            {
                slot.targetPoint.position = worldPositions[i];
                slot.targetPoint.rotation = worldRotations[i];

                var trigger = slot.targetPoint.GetComponent<BoxCollider>();
                if (trigger != null)
                {
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(0.48f, 0.35f, 0.48f);
                }
            }

            if (slot.ghostIndicatorObject != null)
            {
                slot.ghostIndicatorObject.transform.position = worldPositions[i];
                slot.ghostIndicatorObject.transform.rotation = worldRotations[i];
                slot.ghostIndicatorObject.transform.localScale = worldScales[i];
            }

            if (slot.placeholderGreenDrape != null)
            {
                slot.placeholderGreenDrape.transform.position = worldPositions[i];
                slot.placeholderGreenDrape.transform.rotation = worldRotations[i];
                slot.placeholderGreenDrape.transform.localScale = worldScales[i];
            }
        }
    }

    /// <summary>
    /// Begins Phase 5: Sterile Draping of the Mother.
    /// </summary>
    public void BeginDrapingPhase()
    {
        isDrapingActive = true;
        isDrapingComplete = false;
        currentTargetIndex = 0;

        if (autoAlignToDefaults)
        {
            AlignAndFixDrapeSlotVisuals();
        }

        ResolveAdjustedDrapeModelsIfNeeded();

        foreach (var slot in drapeSlots)
        {
            slot.isDraped = false;
            if (slot.mistakeFlashCo != null)
            {
                StopCoroutine(slot.mistakeFlashCo);
                slot.mistakeFlashCo = null;
            }
            SetSlotPlacedVisual(slot, false);
        }

        // Show static blue guides on remaining zones
        UpdateAllZoneVisualGuides();

        if (MayoStandDrapeSocket.Instance != null)
        {
            MayoStandDrapeSocket.Instance.isDraped = false;
            MayoStandDrapeSocket.Instance.isSocketActive = false;
            if (MayoStandDrapeSocket.Instance.ghostDrapeObject != null)
                MayoStandDrapeSocket.Instance.ghostDrapeObject.SetActive(false);
            if (MayoStandDrapeSocket.Instance.actualDrapeObject != null)
                MayoStandDrapeSocket.Instance.actualDrapeObject.SetActive(false);
        }

        if (TablePrepManager.Instance != null)
        {
            TablePrepManager.Instance.isMayoDraped = false;
        }

        Debug.Log("[MotherDrapingManager] Sterile Draping Phase started: Target 1 (Under Buttocks). Use both hands to drape.");
    }

    /// <summary>
    /// Updates visual guide states for all zones.
    /// All unplaced zones show uniform static blue guides.
    /// Placed zones show the assigned 3D drape model (Adjusted_Drapes), keeping white placeholders disabled.
    /// </summary>
    public void UpdateAllZoneVisualGuides()
    {
        for (int i = 0; i < drapeSlots.Count; i++)
        {
            var slot = drapeSlots[i];

            if (slot.isDraped)
            {
                // Placed -> Show permanent drape (preferring Adjusted_Drapes over white cube placeholder), hide ghost guide
                if (slot.ghostIndicatorObject != null) slot.ghostIndicatorObject.SetActive(false);
                SetSlotPlacedVisual(slot, true);
            }
            else if (isDrapingActive && !isDrapingComplete)
            {
                // Unplaced Zone -> Uniform, static soft blue guide across all remaining zones
                SetSlotPlacedVisual(slot, false);
                if (slot.ghostIndicatorObject != null)
                {
                    SetGhostMaterial(slot.ghostIndicatorObject, inactiveGuideMaterial != null ? inactiveGuideMaterial : ghostMaterial);
                    slot.ghostIndicatorObject.SetActive(true);
                }
            }
            else
            {
                if (slot.ghostIndicatorObject != null) slot.ghostIndicatorObject.SetActive(false);
                SetSlotPlacedVisual(slot, false);
            }
        }
    }

    /// <summary>
    /// Returns true if placing a drape on slotIndex obeys:
    /// 1. Under Buttocks -> 2. Abdominal -> 3/4. Left or Right Leg (either order) -> 5. Mayo Stand.
    /// </summary>
    public bool IsSlotValidNext(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= drapeSlots.Count) return false;
        if (drapeSlots[slotIndex] == null || drapeSlots[slotIndex].isDraped) return false;

        if (slotIndex == 0) return true;
        if (slotIndex == 1) return drapeSlots[0].isDraped;

        // Legs (indices 2 and 3) can be placed in either order once Buttocks (0) and Abdominal (1) are complete.
        if (slotIndex == 2 || slotIndex == 3)
        {
            return drapeSlots.Count > 1 && drapeSlots[0].isDraped && drapeSlots[1].isDraped;
        }

        // Subsequent slots (e.g., index 4 = Mayo Stand) require all mother draping zones (0..3) and earlier slots.
        int motherCount = Mathf.Min(4, drapeSlots.Count);
        for (int i = 0; i < motherCount; i++)
        {
            if (!drapeSlots[i].isDraped) return false;
        }
        for (int i = 4; i < slotIndex; i++)
        {
            if (!drapeSlots[i].isDraped) return false;
        }
        return true;
    }

    private int ComputeNextCanonicalTargetIndex()
    {
        for (int i = 0; i < drapeSlots.Count; i++)
        {
            if (drapeSlots[i] != null && !drapeSlots[i].isDraped)
            {
                return i;
            }
        }
        return drapeSlots.Count;
    }

    public string GetExpectedStepDescription()
    {
        if (drapeSlots.Count > 0 && !drapeSlots[0].isDraped) return "1. Under Buttocks";
        if (drapeSlots.Count > 1 && !drapeSlots[1].isDraped) return "2. Abdomen";

        bool leftPending = drapeSlots.Count > 2 && !drapeSlots[2].isDraped;
        bool rightPending = drapeSlots.Count > 3 && !drapeSlots[3].isDraped;
        if (leftPending && rightPending) return "Left or Right Leg (either order)";
        if (leftPending) return "Left Leg";
        if (rightPending) return "Right Leg";

        int nextIdx = ComputeNextCanonicalTargetIndex();
        if (nextIdx < drapeSlots.Count) return drapeSlots[nextIdx].slotName;
        return "Complete";
    }

    /// <summary>
    /// Returns the number of currently completed / draped zones (0 to 4).
    /// </summary>
    public int GetDrapedCount()
    {
        int count = 0;
        for (int i = 0; i < drapeSlots.Count; i++)
        {
            if (drapeSlots[i].isDraped) count++;
        }
        return count;
    }

    /// <summary>
    /// Attempts to place a drape into a specific slot when linen is brought/dropped in the zone.
    /// Flashes RED and rejects if out of sequence; leaves green drape and advances if correct.
    /// </summary>
    public bool TryPlaceDrape(int slotIndex, GameObject linenObj)
    {
        if (!isDrapingActive || isDrapingComplete) return false;
        if (slotIndex < 0 || slotIndex >= drapeSlots.Count) return false;

        var targetSlot = drapeSlots[slotIndex];
        if (targetSlot.isDraped) return false; // Already placed

        // 1. Two-Handed Grab Requirement
        if (linenObj != null)
        {
            var twoHand = linenObj.GetComponent<TwoHandedLinenCloth>()
                       ?? linenObj.GetComponentInParent<TwoHandedLinenCloth>()
                       ?? linenObj.GetComponentInChildren<TwoHandedLinenCloth>();

            if (twoHand != null && !twoHand.IsHeldWithTwoHands())
            {
                if (Time.time - lastMistakeWarningTime > 1.5f)
                {
                    lastMistakeWarningTime = Time.time;
                    if (VRDemoGameManager.Instance != null)
                    {
                        VRDemoGameManager.Instance.RecordViolation(ClinicalViolationType.One_Handed_Drape_Violation, "Linen unfolded with 1 hand");
                        VRDemoGameManager.Instance.ShowWarning("Sterile Technique: You must hold and unfold the drape with BOTH hands!");
                    }
                }
                return false;
            }
        }

        // 2. Sequence Check: 1. Under Buttocks -> 2. Abdomen -> Left or Right Leg (either order) -> Mayo Stand
        if (!IsSlotValidNext(slotIndex))
        {
            FlashMistakeRed(slotIndex);

            // Reject placement & immediately return linen to Table 1
            if (linenObj != null)
            {
                var twoHand = linenObj.GetComponent<TwoHandedLinenCloth>()
                           ?? linenObj.GetComponentInParent<TwoHandedLinenCloth>()
                           ?? linenObj.GetComponentInChildren<TwoHandedLinenCloth>();
                if (twoHand != null)
                {
                    twoHand.ResetToTable1(false);
                }
            }
            return false;
        }

        // 3. Successfully Place Drape on this valid zone
        targetSlot.isDraped = true;

        if (targetSlot.ghostIndicatorObject != null)
        {
            targetSlot.ghostIndicatorObject.SetActive(false);
        }

        // Show permanent placed drape (preferring Adjusted_Drapes over white cube placeholder)
        SetSlotPlacedVisual(targetSlot, true);

        // Unlock Mayo Table if this was the Mayo Stand Drape
        if (targetSlot.slotName.Contains("Mayo") || slotIndex == 4)
        {
            if (MayoStandDrapeSocket.Instance != null)
            {
                MayoStandDrapeSocket.Instance.isDraped = true;
                MayoStandDrapeSocket.Instance.isSocketActive = false;
                if (MayoStandDrapeSocket.Instance.ghostDrapeObject != null)
                    MayoStandDrapeSocket.Instance.ghostDrapeObject.SetActive(false);
                if (MayoStandDrapeSocket.Instance.actualDrapeObject != null)
                    MayoStandDrapeSocket.Instance.actualDrapeObject.SetActive(true);
            }
            if (TablePrepManager.Instance != null)
            {
                TablePrepManager.Instance.isMayoDraped = true;
            }
        }

        // Play audio chime
        if (audioSource != null && drapePlaceAudio != null)
        {
            audioSource.PlayOneShot(drapePlaceAudio);
        }

        int completedStep = GetDrapedCount();
        Debug.Log($"[MotherDrapingManager] Placed Drape: {targetSlot.slotName} ({completedStep}/{drapeSlots.Count})");
        EvaluationLogger.Instance.LogCorrect($"Drape {completedStep} placed", MatrixCriterion.Sequence, targetSlot.slotName);

        OnDrapePlaced?.Invoke(slotIndex, targetSlot);

        // Safely release interactors before destroying the used linen cloth
        if (linenObj != null)
        {
            var grab = linenObj.GetComponent<XRGrabInteractable>()
                    ?? linenObj.GetComponentInParent<XRGrabInteractable>();
            if (grab != null && grab.isSelected)
            {
                var mgr = grab.interactionManager;
                if (mgr != null)
                {
                    var selecting = new List<IXRSelectInteractor>(grab.interactorsSelecting);
                    foreach (var holder in selecting)
                    {
                        mgr.SelectExit(holder, grab);
                    }
                }
            }
            Destroy(linenObj);
        }

        // Advance to next remaining target slot
        currentTargetIndex = ComputeNextCanonicalTargetIndex();
        UpdateAllZoneVisualGuides();

        // 4. Spawn fresh linen on Table 1 for next drape, or complete all draping
        if (currentTargetIndex < drapeSlots.Count)
        {
            SpawnNextLinenOnTable1();
        }
        else
        {
            // All draping zones (Mother + Mayo Stand) completed!
            CompleteAllDraping();
        }

        return true;
    }

    /// <summary>
    /// Called when MayoStandDrapeSocket successfully snaps the sterile drape onto the Mayo Stand.
    /// </summary>
    public void CompleteMayoDraping()
    {
        CompleteAllDraping();
    }

    private void CompleteAllDraping()
    {
        isDrapingComplete = true;
        isDrapingActive = false;
        if (MayoStandDrapeSocket.Instance != null)
        {
            MayoStandDrapeSocket.Instance.isDraped = true;
            MayoStandDrapeSocket.Instance.isSocketActive = false;
        }
        if (TablePrepManager.Instance != null)
        {
            TablePrepManager.Instance.isMayoDraped = true;
        }
        Debug.Log("[MotherDrapingManager] All Drapes (Patient + Mayo Stand) placed successfully! Delivery field & Mayo table sterile & complete.");
        OnAllDrapesCompleted?.Invoke();
    }

    private void FlashMistakeRed(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= drapeSlots.Count) return;

        var slot = drapeSlots[slotIndex];
        if (slot.isDraped) return;

        if (Time.time - lastMistakeWarningTime > 1.25f)
        {
            lastMistakeWarningTime = Time.time;

            if (audioSource != null && drapeMistakeAudio != null)
            {
                audioSource.PlayOneShot(drapeMistakeAudio);
            }

            if (DrapingGuideUI.Instance != null)
            {
                DrapingGuideUI.Instance.ShowMistakeFeedback("Out of order! Drape: 1. Under Buttocks -> 2. Abdomen -> Left or Right Leg (either order) -> Mayo Stand!");
            }

            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordViolation(ClinicalViolationType.Out_Of_Order_Drape, $"Target: {GetExpectedStepDescription()}, Placed: {slot.slotName}");
                VRDemoGameManager.Instance.ShowWarning("Out of order! Drape: 1. Under Buttocks -> 2. Abdomen -> Left or Right Leg (either order) -> Mayo Stand!");
            }
        }

        if (slot.mistakeFlashCo != null) StopCoroutine(slot.mistakeFlashCo);
        slot.mistakeFlashCo = StartCoroutine(MistakeFlashRoutine(slot));
    }

    private IEnumerator MistakeFlashRoutine(DrapeSlot slot)
    {
        if (slot.ghostIndicatorObject != null)
        {
            SetGhostMaterial(slot.ghostIndicatorObject, mistakeRedMaterial != null ? mistakeRedMaterial : ghostMaterial);
            slot.ghostIndicatorObject.SetActive(true);

            yield return new WaitForSeconds(1.25f);

            Material restoreMat = inactiveGuideMaterial != null ? inactiveGuideMaterial : ghostMaterial;
            SetGhostMaterial(slot.ghostIndicatorObject, restoreMat);

            if (!slot.isDraped && isDrapingActive)
            {
                slot.ghostIndicatorObject.SetActive(true);
            }
            else
            {
                slot.ghostIndicatorObject.SetActive(false);
            }
        }
        slot.mistakeFlashCo = null;
    }

    private void SetGhostMaterial(GameObject ghostObj, Material mat)
    {
        if (ghostObj == null || mat == null) return;
        var renderers = ghostObj.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null) r.material = mat;
        }
    }

    private void SpawnNextLinenOnTable1()
    {
        GameObject templateToUse = cachedLinenTemplate != null ? cachedLinenTemplate : dryLinenTemplate;

        GameObject nextLinen = null;
        if (templateToUse != null)
        {
            if (table1LinenParent != null)
            {
                nextLinen = Instantiate(templateToUse, table1LinenParent);
                nextLinen.transform.localPosition = table1LinenLocalPosition;
                nextLinen.transform.localRotation = table1LinenLocalRotation;
                nextLinen.transform.localScale = table1LinenLocalScale;
            }
            else
            {
                nextLinen = Instantiate(templateToUse);
                nextLinen.transform.localScale = table1LinenLocalScale;
            }

            nextLinen.name = "Dry_Linen";
            nextLinen.SetActive(true);
        }

        if (nextLinen != null)
        {
            var twoHand = nextLinen.GetComponent<TwoHandedLinenCloth>();
            if (twoHand == null) twoHand = nextLinen.AddComponent<TwoHandedLinenCloth>();
            twoHand.CacheHomeTransform();

            var rb = nextLinen.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
            }
        }

        Debug.Log($"[MotherDrapingManager] Spawned fresh linen for next step ({GetExpectedStepDescription()}) on Table 1.");
    }

    public void HideAllDrapesAndGhosts()
    {
        ResolveAdjustedDrapeModelsIfNeeded();
        foreach (var slot in drapeSlots)
        {
            if (slot.mistakeFlashCo != null)
            {
                StopCoroutine(slot.mistakeFlashCo);
                slot.mistakeFlashCo = null;
            }
            if (slot.ghostIndicatorObject != null) slot.ghostIndicatorObject.SetActive(false);
            SetSlotPlacedVisual(slot, false);
        }
    }

    /// <summary>
    /// Binds unassigned slots 0..3 to each zone's placed drape child (below GhostIndicator / under targetPoint)
    /// or falls back to an Adjusted_Drapes root in the scene, without altering any transforms.
    /// </summary>
    public void ResolveAdjustedDrapeModelsIfNeeded()
    {
        bool anyMissing = false;
        int limit = Mathf.Min(4, drapeSlots.Count);
        for (int i = 0; i < limit; i++)
        {
            if (drapeSlots[i] != null && drapeSlots[i].final3DModelPrefabOrObject == null)
            {
                anyMissing = true;
                break;
            }
        }

        if (!anyMissing) return;

        Transform adjustedRoot = FindAdjustedDrapesRoot();
        for (int i = 0; i < limit; i++)
        {
            if (drapeSlots[i] == null || drapeSlots[i].final3DModelPrefabOrObject != null) continue;
            GameObject resolved = ResolvePlacedDrapeForSlot(drapeSlots[i], drapeSlots, i, adjustedRoot);
            if (resolved != null)
            {
                drapeSlots[i].final3DModelPrefabOrObject = resolved;
            }
        }
    }

    public static Transform FindAdjustedDrapesRoot()
    {
        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            if (allTransforms[i] != null && allTransforms[i].name == "Adjusted_Drapes")
            {
                return allTransforms[i];
            }
        }
        return null;
    }

    public static GameObject ResolvePlacedDrapeForSlot(DrapeSlot slot, IList<DrapeSlot> allSlots, int slotIndex, Transform adjustedRoot = null)
    {
        if (slot == null) return null;

        // Priority 1: Drape mesh placed directly inside this zone (e.g., under targetPoint below GhostIndicator)
        Transform zoneParent = slot.targetPoint != null
            ? slot.targetPoint
            : (slot.ghostIndicatorObject != null ? slot.ghostIndicatorObject.transform.parent : null);

        if (zoneParent != null)
        {
            for (int c = 0; c < zoneParent.childCount; c++)
            {
                Transform child = zoneParent.GetChild(c);
                if (!IsEligibleZoneDrapeChild(child, slot, allSlots)) continue;
                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    return child.gameObject;
                }
            }
        }

        // Priority 2: Adjusted_Drapes container by slot index / canonical name variants
        if (adjustedRoot != null && slotIndex >= 0 && slotIndex < AdjustedDrapeNameVariants.Length)
        {
            string[] candidates = AdjustedDrapeNameVariants[slotIndex];
            for (int n = 0; n < candidates.Length; n++)
            {
                Transform match = adjustedRoot.Find(candidates[n]);
                if (match != null) return match.gameObject;
            }
        }

        return null;
    }

    private static readonly string[][] AdjustedDrapeNameVariants =
    {
        new[] { "Drape_Buttocks", "Buttocks" },
        new[] { "Drape_Abdomen", "Drape_Abdominal" },
        new[] { "Drape_LeftLeg", "Drape_Left leg", "Drape_Left Leg" },
        new[] { "Drape_RightLeg", "Drape_Right Leg", "Drape_Right leg" }
    };

    private static bool IsEligibleZoneDrapeChild(Transform candidate, DrapeSlot ownerSlot, IList<DrapeSlot> allSlots)
    {
        if (candidate == null) return false;
        GameObject go = candidate.gameObject;
        if (go == ownerSlot.ghostIndicatorObject || go == ownerSlot.placeholderGreenDrape) return false;

        if (allSlots != null)
        {
            for (int i = 0; i < allSlots.Count; i++)
            {
                var s = allSlots[i];
                if (s == null) continue;
                if (s.targetPoint == candidate || s.ghostIndicatorObject == go) return false;
            }
        }

        return true;
    }

    private void SetSlotPlacedVisual(DrapeSlot slot, bool visible)
    {
        if (slot == null) return;

        if (slot.final3DModelPrefabOrObject != null)
        {
            if (visible)
            {
                EnsureAncestorsActiveWithoutExposingUnplacedSiblings(slot.final3DModelPrefabOrObject.transform);
            }
            slot.final3DModelPrefabOrObject.SetActive(visible);

            if (slot.placeholderGreenDrape != null && slot.placeholderGreenDrape != slot.final3DModelPrefabOrObject)
            {
                slot.placeholderGreenDrape.SetActive(false);
            }
        }
        else if (slot.placeholderGreenDrape != null)
        {
            slot.placeholderGreenDrape.SetActive(visible);
        }
    }

    private void EnsureAncestorsActiveWithoutExposingUnplacedSiblings(Transform targetChild)
    {
        if (targetChild == null) return;

        // If an Adjusted_Drapes parent starts inactive, ensure its undraped children are off before activating it.
        Transform parent = targetChild.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf && parent.name == "Adjusted_Drapes")
            {
                for (int c = 0; c < parent.childCount; c++)
                {
                    Transform sibling = parent.GetChild(c);
                    if (sibling == targetChild) continue;
                    bool keepActive = false;
                    for (int s = 0; s < drapeSlots.Count; s++)
                    {
                        var slot = drapeSlots[s];
                        if (slot != null && slot.isDraped && slot.final3DModelPrefabOrObject == sibling.gameObject)
                        {
                            keepActive = true;
                            break;
                        }
                    }
                    if (!keepActive)
                    {
                        sibling.gameObject.SetActive(false);
                    }
                }
            }
            if (!parent.gameObject.activeSelf)
            {
                parent.gameObject.SetActive(true);
            }
            parent = parent.parent;
        }
    }
}

