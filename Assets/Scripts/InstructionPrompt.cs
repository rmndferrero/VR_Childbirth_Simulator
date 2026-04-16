using UnityEngine;
using TMPro; // Required for TextMeshPro

public class InstructionPrompt : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject instructionCanvas;
    public TextMeshProUGUI instructionText;

    [Header("Instructions")]
    [TextArea(3, 5)] // Makes the text boxes bigger in the Unity Inspector
    public string[] messages;
    private int currentIndex = 0;

    void Start()
    {
        // Hide the canvas when the game starts
        instructionCanvas.SetActive(false);

        // Set the first message if we have any
        if (messages.Length > 0)
        {
            instructionText.text = messages[0];
        }
    }

    // This runs when the VR Player enters the table's trigger zone
    private void OnTriggerEnter(Collider other)
    {
        // Make sure your VR Player/XR Origin is tagged as "Player"
        if (other.CompareTag("Player"))
        {
            instructionCanvas.SetActive(true);
        }
    }

    // This runs when the player walks away from the table
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            instructionCanvas.SetActive(false);
            // Optional: Reset the text back to the beginning when they leave
            currentIndex = 0;
            if (messages.Length > 0) instructionText.text = messages[0];
        }
    }

    // This function will be called by your "Next" button
    public void NextMessage()
    {
        currentIndex++;

        // Check if we still have messages left to show
        if (currentIndex < messages.Length)
        {
            instructionText.text = messages[currentIndex];
        }
        else
        {
            // If we are out of messages, hide the canvas
            instructionCanvas.SetActive(false);
            currentIndex = 0; // Reset for next time
        }
    }
}