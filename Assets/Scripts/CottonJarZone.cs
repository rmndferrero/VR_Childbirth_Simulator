using UnityEngine;

public class CottonJarZone : MonoBehaviour
{
    [Header("Antiseptic Configuration")]
    [Tooltip("Which antiseptic this jar dispenses.")]
    public AntisepticType antisepticType = AntisepticType.Iodine_7_5_Scrub;

    [Tooltip("Material applied to the soaked cotton ball.")]
    public Material soakedMaterial;

    private float lastHandlingForcepsEntryTime = -99f;

    /// <summary>
    /// Validates whether cotton may be extracted from this jar in the active MainScene phase.
    /// Leaves TutorialScene (where PerinealCareManager.Instance is null) unrestricted.
    /// </summary>
    public bool IsAllowedInCurrentPhase(out AntisepticType expectedAntiseptic, out string denialMessage)
    {
        if (PerinealCareManager.Instance == null)
        {
            expectedAntiseptic = antisepticType;
            denialMessage = string.Empty;
            return true;
        }

        PerinealCareState state = PerinealCareManager.Instance.currentState;
        switch (state)
        {
            case PerinealCareState.STATE_3_IODINE_7_5_SCRUB:
                expectedAntiseptic = AntisepticType.Iodine_7_5_Scrub;
                if (antisepticType == expectedAntiseptic)
                {
                    denialMessage = string.Empty;
                    return true;
                }
                denialMessage = "WRONG ANTISEPTIC!\nUse 7.5% Iodine Scrub first (not 10%).";
                return false;

            case PerinealCareState.STATE_4_ANTISEPTIC_RINSE:
                expectedAntiseptic = AntisepticType.None;
                denialMessage = "RINSE FIRST!\nWash off 7.5% scrub before using 10% Iodine.";
                return false;

            case PerinealCareState.STATE_5_IODINE_10_PAINT:
                expectedAntiseptic = AntisepticType.Iodine_10_Paint;
                if (antisepticType == expectedAntiseptic)
                {
                    denialMessage = string.Empty;
                    return true;
                }
                denialMessage = "WRONG ANTISEPTIC!\nUse 10% Iodine Paint after rinse (not 7.5%).";
                return false;

            case PerinealCareState.STATE_2_PRELIMINARY_WATER_WASH:
                expectedAntiseptic = AntisepticType.None;
                denialMessage = "COMPLETE WATER WASH FIRST!\nPour sterile water wash before using Iodine.";
                return false;

            default:
                expectedAntiseptic = AntisepticType.None;
                denialMessage = "NOT IODINE STEP!\nFollow the current procedure step.";
                return false;
        }
    }

    public static string FormatAntisepticName(AntisepticType type)
    {
        switch (type)
        {
            case AntisepticType.Iodine_7_5_Scrub: return "7.5% Iodine Scrub";
            case AntisepticType.Iodine_10_Paint: return "10% Iodine Paint";
            default: return "Water Wash";
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ForcepsController forceps = other.GetComponentInParent<ForcepsController>();
        if (forceps != null)
        {
            if (forceps.role == ForcepsRole.Pickup)
            {
                forceps.activeJarZone = this;
                forceps.isInJarZone = true;
            }
            else
            {
                Debug.LogWarning("[CottonJarZone] Only Pickup Forceps may extract cotton from antiseptic jars!");

                // Decision Matrix only: the Handling forceps contaminates the stock jar.
                if (Time.time - lastHandlingForcepsEntryTime > 1.5f)
                {
                    lastHandlingForcepsEntryTime = Time.time;
                    EvaluationLogger.Instance.LogMatrixOnlyViolation(ClinicalViolationType.Forceps_Transfer_Error,
                        "Handling forceps entered the cotton jar", antisepticType.ToString());
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        ForcepsController forceps = other.GetComponentInParent<ForcepsController>();
        if (forceps != null)
        {
            if (forceps.activeJarZone == this)
            {
                forceps.activeJarZone = null;
                forceps.isInJarZone = false;
            }
        }
    }
}