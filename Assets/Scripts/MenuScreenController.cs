using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Main Menu VR UI flow:
/// 1. Title Screen (Play / Exit)
/// 2. Tutorial Prompt Screen ("Would you like a tutorial?" -> Yes: TutorialScene / No: MainScene)
/// </summary>
public class MenuScreenController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject titleScreenPanel;
    public GameObject tutorialPromptPanel;

    [Header("Title Screen Buttons")]
    public Button playButton;
    public Button exitButton;

    [Header("Tutorial Prompt Buttons")]
    public Button yesTutorialButton;
    public Button noTutorialButton;
    public Button backToMenuButton;

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip clickClip;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitClicked);
        }

        if (yesTutorialButton != null)
        {
            yesTutorialButton.onClick.RemoveAllListeners();
            yesTutorialButton.onClick.AddListener(OnYesTutorialClicked);
        }

        if (noTutorialButton != null)
        {
            noTutorialButton.onClick.RemoveAllListeners();
            noTutorialButton.onClick.AddListener(OnNoTutorialClicked);
        }

        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        }
    }

    private void Start()
    {
        ShowTitleScreen();
    }

    public void ShowTitleScreen()
    {
        if (titleScreenPanel != null) titleScreenPanel.SetActive(true);
        if (tutorialPromptPanel != null) tutorialPromptPanel.SetActive(false);
    }

    public void ShowTutorialPrompt()
    {
        if (titleScreenPanel != null) titleScreenPanel.SetActive(false);
        if (tutorialPromptPanel != null) tutorialPromptPanel.SetActive(true);
    }

    private void OnPlayClicked()
    {
        PlaySound();
        ShowTutorialPrompt();
    }

    private void OnExitClicked()
    {
        PlaySound();
        Debug.Log("[MenuScreen] Exiting application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnYesTutorialClicked()
    {
        PlaySound();
        Debug.Log("[MenuScreen] Loading TutorialScene...");
        VRSceneTransitionFader.FadeToScene("TutorialScene");
    }

    private void OnNoTutorialClicked()
    {
        PlaySound();
        Debug.Log("[MenuScreen] Skipping tutorial, loading MainScene...");
        VRSceneTransitionFader.FadeToScene("MainScene");
    }

    private void OnBackToMenuClicked()
    {
        PlaySound();
        ShowTitleScreen();
    }

    private void PlaySound()
    {
        if (audioSource != null && clickClip != null)
        {
            audioSource.PlayOneShot(clickClip);
        }
    }
}
