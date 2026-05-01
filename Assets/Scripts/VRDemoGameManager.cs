using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public class ScenarioData
{
    [TextArea(2, 5)]
    public string instruction;

    public Sprite scenarioImage;

    public string correctTag;

    [TextArea(2, 5)]
    public string correctDescription;
}

public class VRDemoGameManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject startPanel;
    public GameObject instructionPanel;
    public GameObject gamePanel;

    [Header("UI")]
    public Image scenarioImage;
    public TMP_Text scenarioText;
    public TMP_Text feedbackText;

    public Button startButton;
    public Button nextButton;
    public Button retryButton;

    [Header("Tools")]
    public List<ToolItem> tools = new List<ToolItem>();

    [Header("Scenarios")]
    public List<ScenarioData> scenarios = new List<ScenarioData>();

    private ScenarioData currentScenario;
    private bool answered = false;

    void Start()
    {
        startPanel.SetActive(true);
        instructionPanel.SetActive(false);
        gamePanel.SetActive(false);

        feedbackText.text = "";
        retryButton.gameObject.SetActive(false);

        startButton.onClick.AddListener(ShowInstructions);
        nextButton.onClick.AddListener(StartScenario);
        retryButton.onClick.AddListener(StartScenario);
    }

    void ShowInstructions()
    {
        startPanel.SetActive(false);
        instructionPanel.SetActive(true);
    }

    void StartScenario()
    {
        instructionPanel.SetActive(false);
        gamePanel.SetActive(true);

        LoadRandomScenario();
    }

    void LoadRandomScenario()
    {
        answered = false;

        feedbackText.text = "";
        retryButton.gameObject.SetActive(false);

        ResetAllTools();

        int rand = Random.Range(0, scenarios.Count);
        currentScenario = scenarios[rand];

        scenarioImage.sprite = currentScenario.scenarioImage;
        scenarioText.text = currentScenario.instruction;
    }

    public void CheckTool(ToolItem tool)
    {
        if (answered || tool == null || currentScenario == null)
            return;

        if (tool.gameObject.CompareTag(currentScenario.correctTag))
        {
            answered = true;

            tool.MarkCorrect();
            feedbackText.text = "Correct!\n" + currentScenario.correctDescription;

            retryButton.gameObject.SetActive(true);
        }
        else
        {
            tool.MarkWrong();
            feedbackText.text = "Wrong tool. Try again.";
        }
    }

    void ResetAllTools()
    {
        foreach (ToolItem tool in tools)
        {
            if (tool != null)
                tool.ResetTool();
        }
    }
}