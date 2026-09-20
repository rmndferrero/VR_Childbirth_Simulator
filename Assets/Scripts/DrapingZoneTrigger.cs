using UnityEngine;

/// <summary>
/// Trigger volume attached to each of the 4 anatomical draping zones on the mother.
/// Detects when the player hovers or places a sterile linen drape on this zone.
/// </summary>
public class DrapingZoneTrigger : MonoBehaviour
{
    [Tooltip("Drape slot index: 0 = Under Buttocks, 1 = Abdominal, 2 = Left Leg, 3 = Right Leg.")]
    public int slotIndex = 0;

    [Tooltip("Anatomical name of this draping zone.")]
    public string zoneName = "Under Buttocks";

    [Tooltip("Reference to the master MotherDrapingManager.")]
    public MotherDrapingManager drapingManager;

    private void Awake()
    {
        if (drapingManager == null) drapingManager = FindFirstObjectByType<MotherDrapingManager>();
    }

    private float dwellTimer = 0f;

    private void OnTriggerEnter(Collider other)
    {
        if (drapingManager == null) drapingManager = FindFirstObjectByType<MotherDrapingManager>();
        if (drapingManager == null || !drapingManager.isDrapingActive) return;

        dwellTimer = 0f;
    }

    private void OnTriggerStay(Collider other)
    {
        if (drapingManager == null) drapingManager = FindFirstObjectByType<MotherDrapingManager>();
        if (drapingManager == null || !drapingManager.isDrapingActive) return;

        if (slotIndex < 0 || slotIndex >= drapingManager.drapeSlots.Count) return;
        if (drapingManager.drapeSlots[slotIndex].isDraped) return;

        GameObject linenObj = GetLinenObject(other);
        if (linenObj == null) return;

        var twoHand = linenObj.GetComponent<TwoHandedLinenCloth>()
                   ?? linenObj.GetComponentInParent<TwoHandedLinenCloth>()
                   ?? linenObj.GetComponentInChildren<TwoHandedLinenCloth>();

        bool isHeldWithTwo = twoHand != null && twoHand.IsHeldWithTwoHands();
        bool isUnheld = twoHand != null && !twoHand.IsBeingHeld();

        // Place on this zone if held with two hands for >= 0.35s or released inside the zone
        if (isHeldWithTwo)
        {
            dwellTimer += Time.deltaTime;
            if (dwellTimer >= 0.35f)
            {
                dwellTimer = 0f;
                drapingManager.TryPlaceDrape(slotIndex, linenObj);
            }
        }
        else if (isUnheld)
        {
            dwellTimer = 0f;
            drapingManager.TryPlaceDrape(slotIndex, linenObj);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        dwellTimer = 0f;
    }

    private GameObject GetLinenObject(Collider col)
    {
        if (col == null) return null;

        var twoHand = col.GetComponent<TwoHandedLinenCloth>()
                   ?? col.GetComponentInParent<TwoHandedLinenCloth>()
                   ?? col.GetComponentInChildren<TwoHandedLinenCloth>();
        if (twoHand != null) return twoHand.gameObject;

        var tool = col.GetComponent<ToolItem>()
                ?? col.GetComponentInParent<ToolItem>()
                ?? col.GetComponentInChildren<ToolItem>();
        if (tool != null && tool.toolID != null && tool.toolID.ToLower().Contains("linen"))
        {
            return tool.gameObject;
        }

        string colName = col.gameObject.name.ToLower();
        if (colName.Contains("linen") || colName.Contains("drape"))
        {
            return col.gameObject;
        }

        return null;
    }
}
