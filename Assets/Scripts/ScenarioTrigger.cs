using UnityEngine;

public class ScenarioTriggerZone : MonoBehaviour
{
    [Header("Trigger Settings")]
    public int requiredScenarioPhase = 2;
    public DialogueScenario dialogueData;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        // Make sure your XR Origin / Camera has the tag "Player" or "MainCamera"
        if (other.CompareTag("Player") || other.CompareTag("MainCamera"))
        {
            if (hasTriggered) return;

            int currentPhase = VRDemoGameManager.Instance.currentScenarioPhase;

            if (currentPhase < requiredScenarioPhase)
            {
                // Early Access Prevention
                Debug.LogWarning("[UI Prompt] Please finish the Mayo preparation first.");
                // TODO: Link this to your static World Space HUD to show the text to the player
            }
            else if (currentPhase == requiredScenarioPhase)
            {
                // Correct Access
                hasTriggered = true;
                DialogueUIController.Instance.StartDialogue(dialogueData);

                // Optional: Turn off the floor highlight visual
                GetComponent<MeshRenderer>().enabled = false;
            }
        }
    }
}