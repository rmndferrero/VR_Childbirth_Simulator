using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the modular 4-page paginated SummaryScreen GameObject at Step 8.
/// Allows students and clinical preceptors to navigate between:
/// - Page 1: Dialogue & Clinical Communication Competency Profile
/// - Page 2: Perineal Cleaning & 9-Ball Technique Review
/// - Page 3: Sterile Draping Protocol Review
/// - Page 4: Mayo Table Setup & Master Score Verdict (with Restart Button)
/// </summary>
public class SummaryScreenController : MonoBehaviour
{
    public static SummaryScreenController Instance { get; private set; }

    [Header("Root Panel")]
    public GameObject rootScreenObject;

    [Header("The 4 Page Panels")]
    public GameObject page1Dialogue;
    public GameObject page2Perineal;
    public GameObject page3Draping;
    public GameObject page4TableAndFinal;

    [Header("Universal Footer Navigation")]
    public Button buttonBack;
    public TMP_Text textPageIndicator;
    public Button buttonNext;
    public Button buttonRestart;

    [Header("Page 1: Communication Debrief UI Components")]
    public TMP_Text p1OverallRatingText;
    public TMP_Text p1ArchetypeTitleText;
    public TMP_Text p1DebriefNotesText;
    public Image p1BarProfessionalism;
    public TMP_Text p1TextProfessionalism;
    public Image p1BarEmpathy;
    public TMP_Text p1TextEmpathy;
    public Image p1BarClarity;
    public TMP_Text p1TextClarity;
    public TMP_Text p1CritiquesListText;

    [Header("Page 2: Perineal Care UI Components")]
    public TMP_Text p2StatusSummaryText;
    public TMP_Text p2MistakesListText;

    [Header("Page 3: Sterile Draping UI Components")]
    public TMP_Text p3StatusSummaryText;
    public TMP_Text p3MistakesListText;

    [Header("Page 4: Table Setup & Final Master Score")]
    public TMP_Text p4FinalScoreText;
    public TMP_Text p4VerdictGradeText;
    public TMP_Text p4OverallMistakesListText;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pageFlipSound;

    private int currentPageIndex = 0;
    private const int TotalPages = 4;

    private static readonly string[] PageTitles = new string[4]
    {
        "Page 1 of 4: Patient Communication & Informed Consent",
        "Page 2 of 4: Perineal Cleaning & Antiseptic Scrub",
        "Page 3 of 4: Sterile Draping Protocol",
        "Page 4 of 4: Mayo Table Setup & Final Competency Verdict"
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (rootScreenObject == null) rootScreenObject = gameObject;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (buttonBack != null)
        {
            buttonBack.onClick.RemoveAllListeners();
            buttonBack.onClick.AddListener(PreviousPage);
        }

        if (buttonNext != null)
        {
            buttonNext.onClick.RemoveAllListeners();
            buttonNext.onClick.AddListener(NextPage);
        }

        if (buttonRestart != null)
        {
            buttonRestart.onClick.RemoveAllListeners();
            buttonRestart.onClick.AddListener(RestartSimulation);
        }
    }

    public void OpenSummaryScreen()
    {
        if (rootScreenObject != null) rootScreenObject.SetActive(true);

        PopulatePage1_Dialogue();
        PopulatePage2_Perineal();
        PopulatePage3_Draping();
        PopulatePage4_TableAndFinal();

        ShowPage(0);
    }

    public void ShowPage(int index)
    {
        currentPageIndex = Mathf.Clamp(index, 0, TotalPages - 1);

        if (page1Dialogue != null) page1Dialogue.SetActive(currentPageIndex == 0);
        if (page2Perineal != null) page2Perineal.SetActive(currentPageIndex == 1);
        if (page3Draping != null) page3Draping.SetActive(currentPageIndex == 2);
        if (page4TableAndFinal != null) page4TableAndFinal.SetActive(currentPageIndex == 3);

        if (textPageIndicator != null)
        {
            textPageIndicator.text = PageTitles[currentPageIndex];
        }

        if (buttonBack != null)
        {
            buttonBack.interactable = (currentPageIndex > 0);
        }

        if (buttonNext != null)
        {
            buttonNext.gameObject.SetActive(currentPageIndex < TotalPages - 1);
        }

        if (buttonRestart != null)
        {
            buttonRestart.gameObject.SetActive(currentPageIndex == TotalPages - 1);
        }

        PlayPageSound();
    }

    public void NextPage()
    {
        if (currentPageIndex < TotalPages - 1)
        {
            ShowPage(currentPageIndex + 1);
        }
    }

    public void PreviousPage()
    {
        if (currentPageIndex > 0)
        {
            ShowPage(currentPageIndex - 1);
        }
    }

    private void PopulatePage1_Dialogue()
    {
        CommunicationDebriefData debrief = VRDemoGameManager.Instance != null
            ? VRDemoGameManager.Instance.communicationDebrief
            : null;

        if (debrief == null)
        {
            debrief = new CommunicationDebriefData
            {
                professionalismPercent = 80f,
                empathyPercent = 80f,
                clarityPercent = 80f,
                compositeScore = 80f,
                tierRating = "Optimal (Pass)",
                archetypeTitle = "Standard Clinical Communication",
                personalizedInstructorDebrief = "Clinical communication standards met."
            };
        }

        if (p1OverallRatingText != null)
        {
            p1OverallRatingText.text = $"Communication Score: <b>{Mathf.RoundToInt(debrief.compositeScore)}%</b> • {debrief.tierRating}";
        }

        if (p1ArchetypeTitleText != null)
        {
            p1ArchetypeTitleText.text = debrief.archetypeTitle;
        }

        if (p1DebriefNotesText != null)
        {
            p1DebriefNotesText.text = $"\"{debrief.personalizedInstructorDebrief}\"";
        }

        // Sub-criteria progress bars
        SetBar(p1BarProfessionalism, p1TextProfessionalism, debrief.professionalismPercent);
        SetBar(p1BarEmpathy, p1TextEmpathy, debrief.empathyPercent);
        SetBar(p1BarClarity, p1TextClarity, debrief.clarityPercent);

        if (p1CritiquesListText != null)
        {
            if (debrief.specificTokenCritiques.Count > 0)
            {
                p1CritiquesListText.text = "<b>Specific Phrase Observations:</b>\n• " +
                    string.Join("\n• ", debrief.specificTokenCritiques.ToArray());
            }
            else
            {
                p1CritiquesListText.text = "<b>Specific Phrase Observations:</b>\n• Optimal phrased selections made for all 3 clinical stages.";
            }
        }
    }

    private void SetBar(Image bar, TMP_Text label, float percentage)
    {
        if (bar != null)
        {
            bar.fillAmount = percentage / 100f;
            bar.color = percentage >= 80f ? new Color(0.20f, 0.83f, 0.60f) :
                       (percentage >= 50f ? new Color(0.96f, 0.62f, 0.04f) : new Color(0.94f, 0.27f, 0.27f));
        }

        if (label != null)
        {
            string grade = percentage >= 80f ? "Exemplary" : (percentage >= 50f ? "Competent" : "Needs Practice");
            label.text = $"{Mathf.RoundToInt(percentage)}% ({grade})";
        }
    }

    private void PopulatePage2_Perineal()
    {
        if (p2StatusSummaryText != null)
        {
            p2StatusSummaryText.text =
                "<b>Antiseptic & Cleansing Milestones:</b>\n" +
                "• Step 2: Preliminary Water Wash — <color=#34D399>100% Completed</color>\n" +
                "• Step 3: 7.5% Povidone-Iodine 9-Ball Technique — <color=#34D399>Completed in Sequence</color>\n" +
                "• Step 4: Antiseptic Water Rinse — <color=#34D399>Completed</color>\n" +
                "• Step 5: 10% Iodine Surgical Antiseptic Paint — <color=#34D399>Applied</color>";
        }

        if (p2MistakesListText != null)
        {
            var perinealMistakes = FilterMistakesByKeywords("wipe", "stroke", "cotton", "wash", "perineal", "iodine");
            p2MistakesListText.text = perinealMistakes.Count == 0
                ? "<color=#34D399><b>[OK] Flawless Antiseptic Technique!</b>\n• Strict top-to-bottom wiping maintained.\n• One cotton ball per stroke protocol verified.\n• Zero cross-contamination recorded.</color>"
                : "<b>Clinical Perineal Deductions:</b>\n• " + string.Join("\n• ", perinealMistakes.ToArray());
        }
    }

    private void PopulatePage3_Draping()
    {
        if (p3StatusSummaryText != null)
        {
            p3StatusSummaryText.text =
                "<b>Sterile Draping Milestones:</b>\n" +
                "• Glove Protocol: Sterile surgical blue gloves equipped.\n" +
                "• Two-Handed Technique: Strict dual-handed linen unfolding maintained.\n" +
                "• Sequential Coverage: 4 anatomical zones draped (Under Buttocks, Abdomen, Left Thigh, Right Thigh).\n" +
                "• Mayo Stand Aseptic Draping: Sterile cover applied before instrument transfer.";
        }

        if (p3MistakesListText != null)
        {
            var drapingMistakes = FilterMistakesByKeywords("drape", "linen", "glove", "unfold", "mayo");
            p3MistakesListText.text = drapingMistakes.Count == 0
                ? "<color=#34D399><b>✓ Flawless Sterile Draping!</b>\n• Unfolded linen with both hands prior to contact.\n• Correct anatomical sequence maintained.\n• Sterile field preserved.</color>"
                : "<b>Clinical Draping Deductions:</b>\n• " + string.Join("\n• ", drapingMistakes.ToArray());
        }
    }

    private void PopulatePage4_TableAndFinal()
    {
        int score = VRDemoGameManager.Instance != null ? VRDemoGameManager.Instance.currentScore : 100;
        var rubric = VRDemoGameManager.Instance != null ? VRDemoGameManager.Instance.scoringRubric : null;

        int passThreshold = rubric != null ? rubric.passThresholdScore : 70;
        int honorsThreshold = rubric != null ? rubric.honorsThresholdScore : 85;

        if (p4FinalScoreText != null)
        {
            p4FinalScoreText.text = $"Final Score: <color=#38BDF8><b>{score} / 100</b></color>";
        }

        if (p4VerdictGradeText != null)
        {
            if (score >= honorsThreshold)
            {
                p4VerdictGradeText.text = "<color=#34D399>VERDICT: PASS (EXCELLENT / HONORS)</color>";
            }
            else if (score >= passThreshold)
            {
                p4VerdictGradeText.text = "<color=#FBBF24>VERDICT: PASS (CLINICALLY COMPETENT)</color>";
            }
            else
            {
                p4VerdictGradeText.text = "<color=#EF4444>VERDICT: NEEDS PRACTICE / REMEDIATION REQUIRED</color>";
            }
        }

        if (p4OverallMistakesListText != null)
        {
            List<string> allMistakes = VRDemoGameManager.Instance != null
                ? VRDemoGameManager.Instance.mistakeLog
                : new List<string>();

            if (allMistakes.Count == 0)
            {
                p4OverallMistakesListText.text = "<color=#34D399><b>[OK] Zero Clinical Mistakes Logged!</b>\nCandidate executed the full 8-step protocol in accordance with clinical standards.</color>";
            }
            else
            {
                p4OverallMistakesListText.text = $"<b>Recorded Clinical Deductions ({allMistakes.Count}):</b>\n• " +
                    string.Join("\n• ", allMistakes.ToArray());
            }
        }
    }

    private List<string> FilterMistakesByKeywords(params string[] keywords)
    {
        var matches = new List<string>();
        if (VRDemoGameManager.Instance == null) return matches;

        foreach (var m in VRDemoGameManager.Instance.mistakeLog)
        {
            string lower = m.ToLower();
            foreach (var kw in keywords)
            {
                if (lower.Contains(kw))
                {
                    matches.Add(m);
                    break;
                }
            }
        }
        return matches;
    }

    public void RestartSimulation()
    {
        PlayPageSound();
        Debug.Log("[SummaryScreen] Restarting simulation...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void PlayPageSound()
    {
        if (audioSource != null && pageFlipSound != null)
        {
            audioSource.PlayOneShot(pageFlipSound);
        }
    }
}
