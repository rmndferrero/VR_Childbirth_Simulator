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
/// Controls the 4-step sterile draping sequence on the mother:
/// 1. Under Buttocks -> 2. Abdominal -> 3. Left Leg -> 4. Right Leg.
/// Features:
/// - 4-Zone Blue Holographic Guides (active target pulses in bright cyan, upcoming in soft blue).
/// - Permanent green placed drapes on correct placement.
/// - Flashes glowing RED if placed on a wrong / out-of-order zone.
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

    [Header("Pulse Animation Settings")]
    public float ghostPulseSpeed = 4.0f;

    public event Action<int, DrapeSlot> OnDrapePlaced;
    public event Action OnAllDrapesCompleted;

    private float lastMistakeWarningTime = 0f;
    private GameObject cachedLinenTemplate = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        LoadMaterialsIfNull();
        CacheInitialLinenTransform();
    }

    private void LoadMaterialsIfNull()
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
            inactiveGuideMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Inactive_Mat.mat");
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

        foreach (var slot in drapeSlots)
        {
            slot.isDraped = false;
            if (slot.placeholderGreenDrape != null) slot.placeholderGreenDrape.SetActive(false);
            if (slot.final3DModelPrefabOrObject != null) slot.final3DModelPrefabOrObject.SetActive(false);
        }

        // Show all 4 static blue guides on patient
        UpdateAllZoneVisualGuides();

        Debug.Log("[MotherDrapingManager] Sterile Draping Phase started: Target 1 of 4 (Under Buttocks). Use both hands to drape.");
    }

    /// <summary>
    /// Updates visual guide states for all 4 zones.
    /// All unplaced zones show uniform static blue guides (no pulsing clue).
    /// Placed zones show permanent green drapes.
    /// </summary>
    public void UpdateAllZoneVisualGuides()
    {
        for (int i = 0; i < drapeSlots.Count; i++)
        {
            var slot = drapeSlots[i];

            if (slot.isDraped)
            {
                // Placed -> Show permanent green drape, hide ghost guide
                if (slot.ghostIndicatorObject != null) slot.ghostIndicatorObject.SetActive(false);
                if (slot.placeholderGreenDrape != null) slot.placeholderGreenDrape.SetActive(true);
            }
            else if (isDrapingActive && !isDrapingComplete)
            {
                // Unplaced Zone -> Uniform, static soft blue guide across all remaining zones
                if (slot.placeholderGreenDrape != null) slot.placeholderGreenDrape.SetActive(false);
                if (slot.ghostIndicatorObject != null)
                {
                    SetGhostMaterial(slot.ghostIndicatorObject, inactiveGuideMaterial != null ? inactiveGuideMaterial : ghostMaterial);
                    slot.ghostIndicatorObject.SetActive(true);
                }
            }
            else
            {
                if (slot.ghostIndicatorObject != null) slot.ghostIndicatorObject.SetActive(false);
                if (slot.placeholderGreenDrape != null) slot.placeholderGreenDrape.SetActive(false);
            }
        }
    }

    private void Update()
    {
        // Scale pulsing intentionally disabled so players cannot tell which order to place
    }

    public void OnLinenHoverEnter(int slotIndex, GameObject linenObj) { }
    public void OnLinenHoverStay(int slotIndex, GameObject linenObj) { }
    public void OnLinenHoverExit(int slotIndex, GameObject linenObj) { }

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
                        VRDemoGameManager.Instance.ShowWarning("Sterile Technique: You must hold and unfold the drape with BOTH hands!");
                    }
                }
                return false;
            }
        }

        // 2. Sequence Check: Must place drapes in exact clinical order
        if (slotIndex != currentTargetIndex)
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

        // 3. Successfully Place Drape on this correct active zone
        targetSlot.isDraped = true;

        if (targetSlot.ghostIndicatorObject != null)
        {
            targetSlot.ghostIndicatorObject.SetActive(false);
        }

        // Show permanent green placed drape
        if (targetSlot.final3DModelPrefabOrObject != null)
        {
            targetSlot.final3DModelPrefabOrObject.SetActive(true);
        }
        else if (targetSlot.placeholderGreenDrape != null)
        {
            targetSlot.placeholderGreenDrape.SetActive(true);
        }

        // Play audio chime
        if (audioSource != null && drapePlaceAudio != null)
        {
            audioSource.PlayOneShot(drapePlaceAudio);
        }

        int completedStep = currentTargetIndex + 1;
        Debug.Log($"[MotherDrapingManager] Placed Drape: {targetSlot.slotName} ({completedStep}/{drapeSlots.Count})");

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

        // Advance to next target slot
        currentTargetIndex++;
        UpdateAllZoneVisualGuides();

        // 4. Spawn fresh linen on Table 1 for next drape, or complete phase if all 4 are done
        if (currentTargetIndex < drapeSlots.Count)
        {
            SpawnNextLinenOnTable1();
        }
        else
        {
            isDrapingComplete = true;
            isDrapingActive = false;
            Debug.Log("[MotherDrapingManager] All 4 Drapes placed successfully! Delivery field sterile & complete.");
            OnAllDrapesCompleted?.Invoke();
        }

        return true;
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
                DrapingGuideUI.Instance.ShowMistakeFeedback("Out of order! Drape in order: 1. Under Buttocks -> 2. Abdomen -> 3. Left Thigh -> 4. Right Thigh!");
            }

            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordMistake("Draping_Order_Violation");
                VRDemoGameManager.Instance.ShowWarning("Out of order! Drape in order: 1. Under Buttocks -> 2. Abdomen -> 3. Left Thigh -> 4. Right Thigh!");
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

            int slotIdx = drapeSlots.IndexOf(slot);
            Material restoreMat = (slotIdx == currentTargetIndex) ? ghostMaterial : (inactiveGuideMaterial != null ? inactiveGuideMaterial : ghostMaterial);
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

        Debug.Log($"[MotherDrapingManager] Spawned fresh linen for Step {currentTargetIndex + 1} ({drapeSlots[currentTargetIndex].slotName}) on Table 1.");
    }

    public void HideAllDrapesAndGhosts()
    {
        foreach (var slot in drapeSlots)
        {
            if (slot.ghostIndicatorObject != null) slot.ghostIndicatorObject.SetActive(false);
            if (slot.placeholderGreenDrape != null) slot.placeholderGreenDrape.SetActive(false);
            if (slot.final3DModelPrefabOrObject != null) slot.final3DModelPrefabOrObject.SetActive(false);
        }
    }
}

