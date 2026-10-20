using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ScenarioDataGenerator
{
    private const string BaseDataDir = "Assets/ScenarioData";
    private const string ScoringDir = BaseDataDir + "/Scoring";
    private const string TokensDir = BaseDataDir + "/PhraseTokens";
    private const string ScenariosDir = BaseDataDir + "/Scenarios";

    [MenuItem("Tools/Generate Scenario & Scoring Data")]
    public static void GenerateAllData()
    {
        EnsureDirectories();

        // 1. Generate Centralized Scoring Rubric
        var rubric = ScriptableObject.CreateInstance<SimulationScoringRubricSO>();
        rubric.maxTotalScore = 100;
        rubric.passThresholdScore = 70;
        rubric.honorsThresholdScore = 85;
        rubric.suboptimalDialoguePenalty = 5;
        rubric.inadequateDialoguePenalty = 10;
        rubric.instrumentHandledBeforeConsentPenalty = 5;
        rubric.reverseWipeDirectionPenalty = 5;
        rubric.outOfOrderStrokePenalty = 5;
        rubric.reusedCottonBallPenalty = 5;
        rubric.oneHandedDrapeViolationPenalty = 5;
        rubric.outOfOrderDrapePenalty = 5;
        rubric.incorrectTableSlotPenalty = 5;
        rubric.floorDropPenalty = 5;
        rubric.contaminatedDropPenalty = 10;

        string rubricPath = ScoringDir + "/OSCE_Strict_Scoring_Rubric.asset";
        AssetDatabase.CreateAsset(rubric, rubricPath);
        Debug.Log($"[DataGenerator] Created Rubric at: {rubricPath}");

        // 2. Generate 9 Phrase Tokens
        var tokenList = new List<DialoguePhraseTokenSO>();

        // Category 0: Introduction
        tokenList.Add(CreateToken("Intro_Optimal", PhraseSlotCategory.Introduction,
            "Good day, ma'am. I am your student nurse today assisting with your delivery care.",
            "Formal Intro & Role",
            5.0f, 4.8f, 4.8f,
            "Exemplary professional decorum. Clear student nurse role disclosure and warm greeting established immediate trust."));

        tokenList.Add(CreateToken("Intro_Suboptimal", PhraseSlotCategory.Introduction,
            "Hello there, I'll be helping out in here today for your procedure.",
            "Casual Greeting",
            3.2f, 3.5f, 3.0f,
            "Casual bedside greeting. Failed to identify specific clinical role or introduce self properly by name/title."));

        tokenList.Add(CreateToken("Intro_Inadequate", PhraseSlotCategory.Introduction,
            "Lie back and keep still, I need to get started right now.",
            "Abrupt Command",
            1.2f, 1.0f, 1.5f,
            "Authoritarian and abrupt. Omitted personal greeting and role introduction, significantly elevating patient anxiety."));

        // Category 1: Patient Identification
        tokenList.Add(CreateToken("ID_Optimal", PhraseSlotCategory.PatientIdentification,
            "May I verify your full name and date of birth, and check your hospital wristband?",
            "Two-Way ID & Wristband",
            5.0f, 4.5f, 5.0f,
            "Gold standard two-way patient verification complying with universal patient safety protocols."));

        tokenList.Add(CreateToken("ID_Suboptimal", PhraseSlotCategory.PatientIdentification,
            "You are Mrs. Santos, correct?",
            "Name Confirmation Only",
            3.0f, 3.2f, 3.0f,
            "Leading question format without verifying wristband ID or asking for secondary identifier (date of birth)."));

        tokenList.Add(CreateToken("ID_Inadequate", PhraseSlotCategory.PatientIdentification,
            "I assume you're the delivery patient in this room.",
            "Presumed Identity",
            1.0f, 1.5f, 1.0f,
            "Severe patient safety hazard. Zero formal identification or wristband verification before initiating invasive care."));

        // Category 2: Procedure Explanation
        tokenList.Add(CreateToken("Explain_Optimal", PhraseSlotCategory.ProcedureExplanation,
            "I will be performing perineal cleaning with warm water and antiseptic solution to prevent infection, followed by sterile draping.",
            "Full Sensory & Warm Water",
            5.0f, 5.0f, 5.0f,
            "Comprehensive clinical preparation. Clearly outlined warm water wash, antiseptic scrub, and sterile drape rationale."));

        tokenList.Add(CreateToken("Explain_Suboptimal", PhraseSlotCategory.ProcedureExplanation,
            "I need to clean your perineal area with antiseptic now.",
            "Brief Wash Mention",
            3.5f, 2.8f, 3.2f,
            "Vague procedural explanation. Omitted sensory preparation (warm water warning) and rationale for draping."));

        tokenList.Add(CreateToken("Explain_Inadequate", PhraseSlotCategory.ProcedureExplanation,
            "I'm just going to pour some stuff down there and wipe it down.",
            "No Explanation Given",
            1.0f, 1.2f, 1.5f,
            "Inappropriate clinical vocabulary and zero sensory preparation, violating fundamental informed consent standards."));

        // 3. Generate Scenario Asset
        var scenario = ScriptableObject.CreateInstance<PhraseFormulationScenarioSO>();
        scenario.scenarioTitle = "Patient Informed Consent & Procedural Briefing";
        scenario.weightIntroduction = 0.25f;
        scenario.weightIdentification = 0.35f;
        scenario.weightExplanation = 0.40f;
        scenario.greenThreshold = 80f;
        scenario.orangeThreshold = 50f;
        scenario.availableTokens = tokenList;
        scenario.optimalPatientResponse = "\"Thank you for explaining, nurse. I understand and am ready for the cleaning procedure.\"";
        scenario.suboptimalPatientResponse = "\"Okay nurse... please be gentle during the procedure.\"";
        scenario.inadequatePatientResponse = "\"Wait, who are you? What are you going to do to me? Please explain first!\"";

        string scenarioPath = ScenariosDir + "/PhraseFormulationScenario.asset";
        AssetDatabase.CreateAsset(scenario, scenarioPath);
        Debug.Log($"[DataGenerator] Created Scenario at: {scenarioPath}");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DataGenerator] All Scenario and Scoring Assets successfully generated!");
    }

    private static DialoguePhraseTokenSO CreateToken(string tokenID, PhraseSlotCategory category, string fullText, string shortLabel, float prof, float emp, float clar, string critique)
    {
        var token = ScriptableObject.CreateInstance<DialoguePhraseTokenSO>();
        token.tokenID = tokenID;
        token.category = category;
        token.fullPhraseText = fullText;
        token.chipShortLabel = shortLabel;
        token.professionalism = prof;
        token.empathy = emp;
        token.clarity = clar;
        token.clinicalCritiqueNote = critique;

        string path = $"{TokensDir}/{tokenID}.asset";
        AssetDatabase.CreateAsset(token, path);
        Debug.Log($"[DataGenerator] Created Token at: {path}");
        return token;
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(BaseDataDir)) Directory.CreateDirectory(BaseDataDir);
        if (!Directory.Exists(ScoringDir)) Directory.CreateDirectory(ScoringDir);
        if (!Directory.Exists(TokensDir)) Directory.CreateDirectory(TokensDir);
        if (!Directory.Exists(ScenariosDir)) Directory.CreateDirectory(ScenariosDir);
        AssetDatabase.Refresh();
    }
}
