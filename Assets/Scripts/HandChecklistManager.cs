using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the hand checklist UI on the player's left hand canvas.
/// Configures existing canvas elements:
/// - Task text: "Set the Table"
/// - Box color: Red when task is NOT done (in progress)
/// - Box color: Lights up GREEN when task is finished (all tools placed correctly)
/// </summary>
public class HandChecklistManager : MonoBehaviour
{
    [Header("Task Settings")]
    public string taskTextString = "Set the Table";

    [Header("Box & Text References (Optional Manual Overrides)")]
    public Image boxImage;
    public TMP_Text taskLabelTMP;
    public TMP_Text statusTMP;

    [Header("Colors")]
    public Color redNotDoneColor = new Color(0.9f, 0.22f, 0.22f, 1f);
    public Color greenDoneColor = new Color(0.22f, 0.85f, 0.42f, 1f);
    public Color normalTextColor = new Color(0.92f, 0.95f, 0.98f, 1f);

    private bool isCompleted = false;

    private void Awake()
    {
        AutoFindComponents();
    }

    private void Start()
    {
        InitializeUIState();
    }

    private void AutoFindComponents()
    {
        // Find existing text component (e.g. "TASK CHU CHU")
        if (taskLabelTMP == null)
        {
            var tmps = GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in tmps)
            {
                if (tmp != null && (tmp.text.Contains("TASK") || tmp.gameObject.name.Contains("Panel") || tmp.gameObject.name.Contains("Text")))
                {
                    taskLabelTMP = tmp;
                    break;
                }
            }
            if (taskLabelTMP == null && tmps.Length > 0)
                taskLabelTMP = tmps[0];
        }

        // Find existing Button box image component
        if (boxImage == null)
        {
            var btn = GetComponentInChildren<Button>(true);
            if (btn != null)
            {
                boxImage = btn.GetComponent<Image>();
            }
            else
            {
                var imgs = GetComponentsInChildren<Image>(true);
                foreach (var img in imgs)
                {
                    if (img != null && img.gameObject.name.Contains("Button"))
                    {
                        boxImage = img;
                        break;
                    }
                }
            }
        }
    }

    private void InitializeUIState()
    {
        // 1. Task Text: "Set the Table"
        if (taskLabelTMP != null)
        {
            taskLabelTMP.text = taskTextString;
            taskLabelTMP.color = normalTextColor;
            taskLabelTMP.fontStyle = FontStyles.Normal;
        }

        // 2. Box Color: RED while task is NOT done
        if (boxImage != null)
        {
            boxImage.color = redNotDoneColor;
        }

        if (statusTMP != null)
        {
            statusTMP.text = "Incomplete";
            statusTMP.color = redNotDoneColor;
        }

        Debug.Log("[HandChecklistManager] Checklist initialized. Box is RED (Incomplete). Task: Set the Table");
    }

    /// <summary>
    /// Called when the player finishes setting the table in the correct order.
    /// Lights up the box GREEN and marks task complete.
    /// </summary>
    public void UnlockTask(string completedTaskName)
    {
        SetTaskComplete();
    }

    /// <summary>
    /// Lights up the box GREEN and applies completion styling.
    /// </summary>
    public void SetTaskComplete()
    {
        if (isCompleted) return;
        isCompleted = true;

        // Box lights up GREEN when task is done!
        if (boxImage != null)
        {
            boxImage.color = greenDoneColor;
        }

        // Task text turns GREEN with Strikethrough
        if (taskLabelTMP != null)
        {
            taskLabelTMP.fontStyle = FontStyles.Strikethrough;
            taskLabelTMP.color = greenDoneColor;
        }

        if (statusTMP != null)
        {
            statusTMP.text = "COMPLETED";
            statusTMP.color = greenDoneColor;
            statusTMP.fontStyle = FontStyles.Bold;
        }

        Debug.Log("[HandChecklistManager] Task 'Set the Table' FINISHED in correct order! Box lit up GREEN!");
    }
}
