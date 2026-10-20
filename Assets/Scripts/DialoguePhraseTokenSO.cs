using UnityEngine;

public enum PhraseSlotCategory
{
    Introduction = 0,          // Stage Weight: 25% (Greeting, self-introduction, student nurse role)
    PatientIdentification = 1,  // Stage Weight: 35% (Two-way identity verification)
    ProcedureExplanation = 2    // Stage Weight: 40% (Sensory and procedural preparation)
}

/// <summary>
/// ScriptableObject defining an individual dialogue phrase token (chip)
/// used in the 3-Slot Drag-and-Drop Phrase Formulation Decision Matrix.
/// Evaluates Professionalism, Empathy, and Clarity on a 1.0 to 5.0 rubric scale.
/// </summary>
[CreateAssetMenu(fileName = "NewPhraseToken", menuName = "VR Simulator/Dialogue Phrase Token")]
public class DialoguePhraseTokenSO : ScriptableObject
{
    [Header("Token Identification")]
    public string tokenID;
    public PhraseSlotCategory category;

    [Header("Phrase Content")]
    [TextArea(2, 4)]
    [Tooltip("The complete spoken dialogue text that will appear in the assembled statement.")]
    public string fullPhraseText;

    [Tooltip("Optional shorter label displayed on the compact UI chip inside the pool.")]
    public string chipShortLabel;

    [Header("Affective Sub-Criteria (1.0 to 5.0 Benchmark Scale)")]
    [Range(1f, 5f)]
    [Tooltip("Formal protocol adherence, role clarification, and respectful decorum.")]
    public float professionalism = 5f;

    [Range(1f, 5f)]
    [Tooltip("Bedside manner, compassionate reassurance, recognizing patient vulnerability.")]
    public float empathy = 5f;

    [Range(1f, 5f)]
    [Tooltip("Accurate sensory and procedural preparation (warm water, cleaning, draping).")]
    public float clarity = 5f;

    [Header("Debrief & Pedagogical Feedback")]
    [TextArea(1, 3)]
    [Tooltip("Specific clinical feedback note displayed on the Step 8 debrief board when this token is chosen.")]
    public string clinicalCritiqueNote;

    /// <summary>
    /// Average raw affective score across all three dimensions (1.0 to 5.0 scale).
    /// </summary>
    public float RawScore => (professionalism + empathy + clarity) / 3f;
}
