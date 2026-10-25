using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Manages the Mayo Stand Sterile Draping Socket (Step 6B).
/// Provides a holographic blue preview of the drape over the Mayo Stand.
/// Enforces strict two-handed unfolding technique before snapping.
/// Upon correct placement, reveals MayoStand_Drape, marks the Mayo table as sterile,
/// and unlocks Step 7 (Instrument Setup).
/// </summary>
public class MayoStandDrapeSocket : MonoBehaviour
{
    public static MayoStandDrapeSocket Instance { get; private set; }

    [Header("Visual References")]
    [Tooltip("Holographic translucent ghost indicator previewing drape placement.")]
    public GameObject ghostDrapeObject;

    [Tooltip("The actual 3D model drape mesh (MayoStand_Drape) that is revealed on lock.")]
    public GameObject actualDrapeObject;

    [Header("Materials")]
    public Material ghostMaterial;
    public Material mistakeRedMaterial;

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip drapePlaceAudio;
    public AudioClip drapeMistakeAudio;

    [Header("State")]
    public bool isSocketActive = false;
    public bool isDraped = false;

    private Coroutine mistakeFlashCo = null;
    private float lastMistakeWarningTime = 0f;
    private float dwellTimer = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        LoadAssetsIfNull();
        InitializeObjects();
    }

    private void Start()
    {
        InitializeObjects();
    }

    private void LoadAssetsIfNull()
    {
#if UNITY_EDITOR
        if (ghostMaterial == null)
        {
            ghostMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Blue_Active_Mat.mat")
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Drape_Ghost_Mat.mat");
        }

        if (mistakeRedMaterial == null)
        {
            mistakeRedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Zone_Guide_Red_Mistake_Mat.mat");
        }
#endif
    }

    private void InitializeObjects()
    {
        if (actualDrapeObject == null)
        {
            var drapeTransform = transform.Find("MayoStand_Drape");
            if (drapeTransform != null) actualDrapeObject = drapeTransform.gameObject;
        }

        if (actualDrapeObject != null && !isDraped)
        {
            actualDrapeObject.SetActive(false);
        }

        if (ghostDrapeObject != null && !isSocketActive)
        {
            ghostDrapeObject.SetActive(false);
        }
    }

    /// <summary>
    /// Activates the Mayo Stand placement socket once the mother's 4 draping zones are complete.
    /// Shows the holographic blue ghost over the Mayo stand tray.
    /// </summary>
    public void ActivateSocket()
    {
        if (isDraped) return;

        isSocketActive = true;

        if (ghostDrapeObject != null)
        {
            SetGhostMaterial(ghostMaterial);
            ghostDrapeObject.SetActive(true);
        }

        Debug.Log("[MayoStandDrapeSocket] Activated Mayo Stand Draping Socket. Holographic ghost preview visible.");
    }

    private void OnTriggerStay(Collider other)
    {
        bool draped = isDraped || (TablePrepManager.Instance != null && TablePrepManager.Instance.isMayoDraped);
        if (draped) return; // Mayo Stand is draped with sterile linen, completely sterile!

        // 1. Tool Hazard Check: If player attempts to place an instrument on bare Mayo stand
        var tool = other.GetComponent<ToolItem>()
                ?? other.GetComponentInParent<ToolItem>()
                ?? other.GetComponentInChildren<ToolItem>();

        if (tool != null && !tool.toolID.ToLower().Contains("linen"))
        {
            if (VRHeadsetVisualFeedback.Instance != null)
            {
                VRHeadsetVisualFeedback.Instance.TriggerUnsterileMayoViolation(tool.toolID);
            }
            return;
        }

        if (!isSocketActive) return;

        // 2. Linen Detection
        GameObject linenObj = GetLinenObject(other);
        if (linenObj == null) return;

        var twoHand = linenObj.GetComponent<TwoHandedLinenCloth>()
                   ?? linenObj.GetComponentInParent<TwoHandedLinenCloth>()
                   ?? linenObj.GetComponentInChildren<TwoHandedLinenCloth>();

        bool isHeldWithTwo = twoHand != null && twoHand.IsHeldWithTwoHands();
        bool isHeldWithOne = twoHand != null && twoHand.IsBeingHeld() && !isHeldWithTwo;
        bool isUnheld = twoHand != null && !twoHand.IsBeingHeld();

        if (isHeldWithOne)
        {
            dwellTimer += Time.deltaTime;
            if (dwellTimer >= 0.3f)
            {
                dwellTimer = 0f;
                RejectOneHandedPlacement(twoHand);
            }
        }
        else if (isHeldWithTwo)
        {
            dwellTimer += Time.deltaTime;
            if (dwellTimer >= 0.35f)
            {
                dwellTimer = 0f;
                TryPlaceMayoDrape(linenObj);
            }
        }
        else if (isUnheld)
        {
            dwellTimer = 0f;
            RejectOneHandedPlacement(twoHand);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        dwellTimer = 0f;
    }

    private void RejectOneHandedPlacement(TwoHandedLinenCloth twoHand)
    {
        if (Time.time - lastMistakeWarningTime > 1.5f)
        {
            lastMistakeWarningTime = Time.time;

            if (audioSource != null && drapeMistakeAudio != null)
            {
                audioSource.PlayOneShot(drapeMistakeAudio);
            }

            if (VRDemoGameManager.Instance != null)
            {
                VRDemoGameManager.Instance.RecordViolation(
                    ClinicalViolationType.One_Handed_Drape_Violation,
                    "Mayo Stand drape handled with 1 hand"
                );
                VRDemoGameManager.Instance.ShowWarning("Sterile Technique: Grab and unfold the drape with BOTH hands before placing!");
            }
        }

        FlashMistakeRed();

        if (twoHand != null)
        {
            twoHand.ResetToTable1(false);
        }
    }

    /// <summary>
    /// Snaps and locks the sterile drape onto the Mayo Stand.
    /// Strictly requires two-handed hold at the moment of placement.
    /// </summary>
    public bool TryPlaceMayoDrape(GameObject linenObj)
    {
        if (!isSocketActive || isDraped) return false;

        // 1. Strict Two-Handed Technique Validation: It MUST be held with BOTH hands
        if (linenObj != null)
        {
            var twoHand = linenObj.GetComponent<TwoHandedLinenCloth>()
                       ?? linenObj.GetComponentInParent<TwoHandedLinenCloth>()
                       ?? linenObj.GetComponentInChildren<TwoHandedLinenCloth>();

            if (twoHand != null && !twoHand.IsHeldWithTwoHands())
            {
                RejectOneHandedPlacement(twoHand);
                return false;
            }
        }

        // 1. Success Lock
        isDraped = true;
        isSocketActive = false;

        // 2. Hide Ghost Indicator
        if (ghostDrapeObject != null)
        {
            ghostDrapeObject.SetActive(false);
        }

        // 3. Reveal Actual Mayo Stand Drape Mesh
        if (actualDrapeObject != null)
        {
            actualDrapeObject.SetActive(true);
        }

        // 4. Play Placement Audio Chime
        if (audioSource != null && drapePlaceAudio != null)
        {
            audioSource.PlayOneShot(drapePlaceAudio);
        }

        // 5. Cleanly destroy the held linen cloth
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

        // 6. Notify TablePrepManager: Mayo Stand is draped and sterile!
        if (TablePrepManager.Instance != null)
        {
            TablePrepManager.Instance.isMayoDraped = true;
        }

        if (VRDemoGameManager.Instance != null)
        {
            VRDemoGameManager.Instance.ShowWarning("Mayo Stand Draped & Sterile! Field prepared for instrument setup.");
        }

        Debug.Log("[MayoStandDrapeSocket] Mayo Stand successfully draped with sterile linen! Revealing MayoStand_Drape and unlocking Step 7.");

        // 7. Complete Step 6 in MotherDrapingManager and advance to Step 7
        if (MotherDrapingManager.Instance != null)
        {
            MotherDrapingManager.Instance.CompleteMayoDraping();
        }

        return true;
    }

    private void FlashMistakeRed()
    {
        if (ghostDrapeObject == null) return;
        if (mistakeFlashCo != null) StopCoroutine(mistakeFlashCo);
        mistakeFlashCo = StartCoroutine(MistakeFlashRoutine());
    }

    private IEnumerator MistakeFlashRoutine()
    {
        SetGhostMaterial(mistakeRedMaterial != null ? mistakeRedMaterial : ghostMaterial);
        yield return new WaitForSeconds(1.25f);
        SetGhostMaterial(ghostMaterial);
        mistakeFlashCo = null;
    }

    private void SetGhostMaterial(Material mat)
    {
        if (ghostDrapeObject == null || mat == null) return;
        var renderers = ghostDrapeObject.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null) r.material = mat;
        }
    }

    private GameObject GetLinenObject(Collider col)
    {
        if (col == null) return null;

        var twoHand = col.GetComponent<TwoHandedLinenCloth>()
                   ?? col.GetComponentInParent<TwoHandedLinenCloth>()
                   ?? col.GetComponentInChildren<TwoHandedLinenCloth>();
        if (twoHand != null) return twoHand.gameObject;

        string colName = col.gameObject.name.ToLower();
        if (colName.Contains("linen") || colName.Contains("drape"))
        {
            return col.gameObject;
        }

        return null;
    }
}
