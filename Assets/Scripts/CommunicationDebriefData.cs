using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueDecisionEntry
{
    public PhraseSlotCategory category;
    public string slotLabel;
    public string promptQuestion;
    public string selectedPhrase;
    public float selectedScore;
    public bool isSuboptimalOrWrong;
    public string whyWrongNote;
    public string recommendedPhrase;
    public string clinicalRationale;
}

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
    public int dialogueDeduction;

    public string tierRating = "Optimal"; // Optimal, Suboptimal, Inadequate
    public string archetypeTitle = "Exemplary Clinical Communication";
    public string personalizedInstructorDebrief = "";
    public List<string> specificTokenCritiques = new List<string>();
    public List<DialogueDecisionEntry> decisionEntries = new List<DialogueDecisionEntry>();

    /// <summary>
    /// Analyzes the student's three affective dimension scores and generates
    /// personalized clinical instructor feedback matching their specific strengths and deficiencies.
    /// </summary>
    public static CommunicationDebriefData CreateFromEvaluation(
        float pScore, float eScore, float cScore, float composite,
        DialoguePhraseTokenSO introToken, DialoguePhraseTokenSO idToken, DialoguePhraseTokenSO explainToken,
        IEnumerable<DialoguePhraseTokenSO> availableTokens = null, int deductionApplied = 0)
    {
        var data = new CommunicationDebriefData
        {
            professionalismPercent = Mathf.Clamp(pScore, 0f, 100f),
            empathyPercent = Mathf.Clamp(eScore, 0f, 100f),
            clarityPercent = Mathf.Clamp(cScore, 0f, 100f),
            compositeScore = Mathf.Clamp(composite, 0f, 100f),
            dialogueDeduction = Mathf.Max(0, deductionApplied)
        };

        if (composite >= 80f) data.tierRating = "Optimal (Pass)";
        else if (composite >= 50f) data.tierRating = "Suboptimal (Minor Penalty)";
        else data.tierRating = "Inadequate (Needs Review)";

        // Collect specific token critiques
        if (introToken != null && !string.IsNullOrEmpty(introToken.clinicalCritiqueNote))
            data.specificTokenCritiques.Add($"Introduction: {introToken.clinicalCritiqueNote}");
        if (idToken != null && !string.IsNullOrEmpty(idToken.clinicalCritiqueNote))
            data.specificTokenCritiques.Add($"Patient ID: {idToken.clinicalCritiqueNote}");
        if (explainToken != null && !string.IsNullOrEmpty(explainToken.clinicalCritiqueNote))
            data.specificTokenCritiques.Add($"Explanation: {explainToken.clinicalCritiqueNote}");

        data.decisionEntries.Add(BuildDecisionEntry(
            PhraseSlotCategory.Introduction,
            "1. Nurse Introduction",
            "How did you introduce yourself and establish your role?",
            introToken,
            FindBestToken(availableTokens, PhraseSlotCategory.Introduction),
            "Clear self-introduction establishes accountability, builds patient trust, and confirms who is performing care before touching the mother."));

        data.decisionEntries.Add(BuildDecisionEntry(
            PhraseSlotCategory.PatientIdentification,
            "2. Patient Identification",
            "How did you verify the mother's identity before treatment?",
            idToken,
            FindBestToken(availableTokens, PhraseSlotCategory.PatientIdentification),
            "Two-identifier verification prevents wrong-patient interventions and is a core Joint Commission / obstetrics safety check."));

        data.decisionEntries.Add(BuildDecisionEntry(
            PhraseSlotCategory.ProcedureExplanation,
            "3. Procedure & Sensory Explanation",
            "How did you explain perineal cleansing and expected sensations?",
            explainToken,
            FindBestToken(availableTokens, PhraseSlotCategory.ProcedureExplanation),
            "Explaining each step and expected sensations (warm water, antiseptic clean) reduces maternal anxiety and secures valid informed consent."));

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

    private static DialoguePhraseTokenSO FindBestToken(IEnumerable<DialoguePhraseTokenSO> tokens, PhraseSlotCategory category)
    {
        if (tokens == null) return null;
        DialoguePhraseTokenSO best = null;
        foreach (var t in tokens)
        {
            if (t == null || t.category != category) continue;
            if (best == null || t.RawScore > best.RawScore)
            {
                best = t;
            }
        }
        return best;
    }

    private static DialogueDecisionEntry BuildDecisionEntry(
        PhraseSlotCategory category,
        string slotLabel,
        string promptQuestion,
        DialoguePhraseTokenSO selected,
        DialoguePhraseTokenSO recommended,
        string fallbackRationale)
    {
        float selectedRaw = selected != null ? selected.RawScore : 0f;
        bool isWrong = selected == null || selectedRaw < 4.5f;
        string recText = recommended != null && !string.IsNullOrEmpty(recommended.fullPhraseText)
            ? recommended.fullPhraseText
            : (selected != null ? selected.fullPhraseText : "Provide a clear, empathetic clinical statement.");
        string rationale = recommended != null && !string.IsNullOrEmpty(recommended.clinicalCritiqueNote)
            ? recommended.clinicalCritiqueNote
            : fallbackRationale;

        return new DialogueDecisionEntry
        {
            category = category,
            slotLabel = slotLabel,
            promptQuestion = promptQuestion,
            selectedPhrase = selected != null ? selected.fullPhraseText : "No phrase selected",
            selectedScore = selectedRaw,
            isSuboptimalOrWrong = isWrong,
            whyWrongNote = selected != null && !string.IsNullOrEmpty(selected.clinicalCritiqueNote)
                ? selected.clinicalCritiqueNote
                : (isWrong ? "Lacks complete therapeutic clarity, empathy, or protocol specificity." : "Meets clinical communication standards."),
            recommendedPhrase = recText,
            clinicalRationale = rationale
        };
    }
}
