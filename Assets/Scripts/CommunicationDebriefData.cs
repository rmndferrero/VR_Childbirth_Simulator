using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable data container caching the student's affective communication scores
/// and personalized debrief notes from Step 1, persisting them until the Step 8 Summary Screen.
/// </summary>
[System.Serializable]
public class CommunicationDebriefData
{
    public float professionalismPercent;
    public float empathyPercent;
    public float clarityPercent;
    public float compositeScore;

    public string tierRating = "Optimal"; // Optimal, Suboptimal, Inadequate
    public string archetypeTitle = "Exemplary Clinical Communication";
    public string personalizedInstructorDebrief = "";
    public List<string> specificTokenCritiques = new List<string>();

    /// <summary>
    /// Analyzes the student's three affective dimension scores and generates
    /// personalized clinical instructor feedback matching their specific strengths and deficiencies.
    /// </summary>
    public static CommunicationDebriefData CreateFromEvaluation(
        float pScore, float eScore, float cScore, float composite,
        DialoguePhraseTokenSO introToken, DialoguePhraseTokenSO idToken, DialoguePhraseTokenSO explainToken)
    {
        var data = new CommunicationDebriefData
        {
            professionalismPercent = Mathf.Clamp(pScore, 0f, 100f),
            empathyPercent = Mathf.Clamp(eScore, 0f, 100f),
            clarityPercent = Mathf.Clamp(cScore, 0f, 100f),
            compositeScore = Mathf.Clamp(composite, 0f, 100f)
        };

        if (composite >= 80f) data.tierRating = "Optimal (Pass)";
        else if (composite >= 50f) data.tierRating = "Suboptimal (Minor Penalty)";
        else data.tierRating = "Inadequate (Protocol Failure)";

        // Collect specific token critiques
        if (introToken != null && !string.IsNullOrEmpty(introToken.clinicalCritiqueNote))
            data.specificTokenCritiques.Add($"Introduction: {introToken.clinicalCritiqueNote}");
        if (idToken != null && !string.IsNullOrEmpty(idToken.clinicalCritiqueNote))
            data.specificTokenCritiques.Add($"Patient ID: {idToken.clinicalCritiqueNote}");
        if (explainToken != null && !string.IsNullOrEmpty(explainToken.clinicalCritiqueNote))
            data.specificTokenCritiques.Add($"Explanation: {explainToken.clinicalCritiqueNote}");

        // Determine archetype and a descriptive note about the selected phrases (no judgement of the student)
        if (data.professionalismPercent >= 75f && data.empathyPercent >= 75f && data.clarityPercent >= 75f)
        {
            data.archetypeTitle = "Exemplary Therapeutic Communication";
            data.personalizedInstructorDebrief =
                "The selected phrases included a role introduction, patient identity verification, reassurance, and preparation for the sensations of the procedure.";
        }
        else if (data.empathyPercent >= 75f && (data.professionalismPercent < 60f || data.clarityPercent < 60f))
        {
            data.archetypeTitle = "Empathetic but Lacks Protocol / Procedural Rigor";
            data.personalizedInstructorDebrief =
                "The selected phrases scored higher on reassurance. The role introduction and/or the explanation of physical sensations (warm water wash, touch, sterile drapes) scored lower in this run.";
        }
        else if (data.professionalismPercent >= 75f && data.clarityPercent >= 75f && data.empathyPercent < 60f)
        {
            data.archetypeTitle = "Clinically Precise but Lacks Bedside Empathy";
            data.personalizedInstructorDebrief =
                "The selected phrases scored higher on role introduction and procedural explanation, and lower on reassurance and comfort in this run.";
        }
        else if (data.professionalismPercent >= 70f && data.empathyPercent >= 70f && data.clarityPercent < 50f)
        {
            data.archetypeTitle = "Courteous but Vague on Procedure";
            data.personalizedInstructorDebrief =
                "The selected phrases scored higher on courtesy and reassurance, and lower on preparing the mother for the physical sensations of the cleaning procedure in this run.";
        }
        else
        {
            data.archetypeTitle = "Abrupt / Inadequate Bedside Manner";
            data.personalizedInstructorDebrief =
                "The selected phrases scored lower on self-introduction, identity verification and sensory preparation in this run.";
        }

        return data;
    }
}
