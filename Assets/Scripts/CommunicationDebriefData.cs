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

        // Determine Archetype and Preceptor Debrief Commentary
        if (data.professionalismPercent >= 75f && data.empathyPercent >= 75f && data.clarityPercent >= 75f)
        {
            data.archetypeTitle = "Exemplary Therapeutic Communication";
            data.personalizedInstructorDebrief =
                "Candidate demonstrated well-rounded therapeutic communication. You formally introduced yourself as a student nurse, verified patient identity, and prepared the mother with warmth and clear sensory expectations.";
        }
        else if (data.empathyPercent >= 75f && (data.professionalismPercent < 60f || data.clarityPercent < 60f))
        {
            data.archetypeTitle = "Empathetic but Lacks Protocol / Procedural Rigor";
            data.personalizedInstructorDebrief =
                "Candidate displayed genuine bedside warmth and reassurance, putting the mother at ease. However, professional protocol was lacking: you omitted formal role identification or failed to clearly explain the physical sensations (warm water wash, vaginal touch, sterile drapes). Remember: empathy must always be anchored by rigorous clinical protocol.";
        }
        else if (data.professionalismPercent >= 75f && data.clarityPercent >= 75f && data.empathyPercent < 60f)
        {
            data.archetypeTitle = "Clinically Precise but Lacks Bedside Empathy";
            data.personalizedInstructorDebrief =
                "Candidate followed formal hospital protocol and explained the procedural sequence accurately, but tone was cold and impersonal. In obstetric care, laboring patients are emotionally vulnerable; clinical precision must be accompanied by compassionate comfort reassurance.";
        }
        else if (data.professionalismPercent >= 70f && data.empathyPercent >= 70f && data.clarityPercent < 50f)
        {
            data.archetypeTitle = "Courteous but Vague on Procedure";
            data.personalizedInstructorDebrief =
                "Candidate was polite and reassuring, but failed to prepare the mother for the physical sensations of the cleaning procedure. An unbriefed patient can be startled when warm water is poured or antiseptic swabs make contact.";
        }
        else
        {
            data.archetypeTitle = "Abrupt / Inadequate Bedside Manner";
            data.personalizedInstructorDebrief =
                "Communication was hurried, blunt, or dismissive. Rushing through the patient encounter without proper self-introduction, identity verification, or sensory preparation triggers anxiety and violates informed consent standards.";
        }

        return data;
    }
}
