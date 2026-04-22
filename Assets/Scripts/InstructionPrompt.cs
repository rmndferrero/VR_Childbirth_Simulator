using UnityEngine;
using TMPro; // Required for TextMeshPro

public class InstructionPrompt : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject instructionCanvas; // Drag your Canvas or Panel here
    public TextMeshProUGUI instructionText; // Drag your TextMeshPro text here

    [Header("Instructions")]
    [TextArea(3, 5)] // Makes the text boxes bigger in the Unity Inspector
    public string[] messages;
    private int currentIndex = 0;

    void Start()
    {
        // 1. Make sure the Canvas is turned ON when the game starts
        if (instructionCanvas != null)
        {
            instructionCanvas.SetActive(true);
        }

        // 2. Show the first message immediately
        if (messages.Length > 0)
        {
            currentIndex = 0;
            UpdateText();
        }
        else
        {
            Debug.LogWarning("You forgot to add messages in the Inspector!");
        }
    }

    // Call this if you want a "Next" button later
    public void NextMessage()
    {
        if (currentIndex < messages.Length - 1)
        {
            currentIndex++;
            UpdateText();
        }
        else
        {
            // Turn off the canvas when out of messages
            instructionCanvas.SetActive(false);
        }
    }

    private void UpdateText()
    {
        if (instructionText != null)
        {
            instructionText.text = messages[currentIndex];
        }
    }
}