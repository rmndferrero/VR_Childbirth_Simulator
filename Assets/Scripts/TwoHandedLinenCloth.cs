using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Controls dual grab points and two-handed interaction for sterile linen drapes:
/// - Uses native XRI multi-grab routing with dedicated Left and Right attach points.
/// - Automatically snaps back to Table 1 if dropped or rejected.
/// - Enforces two-handed holding requirement for sterile draping.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class TwoHandedLinenCloth : MonoBehaviour
{
    [Header("Dual Grab Attach Points (Left & Right Ends)")]
    public Transform leftAttachPoint;
    public Transform rightAttachPoint;

    [Header("Default Offsets")]
    public Vector3 leftAttachLocalOffset = new Vector3(-0.35f, 0.05f, 0f);
    public Vector3 rightAttachLocalOffset = new Vector3(0.35f, 0.05f, 0f);

    [Header("Table 1 Home Cache")]
    public Transform homeParent;
    public Vector3 homeLocalPosition;
    public Quaternion homeLocalRotation;
    public Vector3 homeLocalScale;

    private XRGrabInteractable grab;
    private Rigidbody rb;
    private bool isBeingHeld = false;
    private float lastOneHandWarningTime = 0f;
    private Coroutine returnCo;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab == null) grab = GetComponentInParent<XRGrabInteractable>();
        if (grab == null) grab = GetComponentInChildren<XRGrabInteractable>();

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = GetComponentInParent<Rigidbody>();

        if (grab != null)
        {
            grab.selectMode = InteractableSelectMode.Multiple;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.trackPosition = true;
            grab.trackRotation = true;
            grab.trackScale = false;
            grab.selectEntered.AddListener(OnSelectEntered);
            grab.selectExited.AddListener(OnSelectExited);
        }

        EnsureAttachPoints();
        CacheHomeTransform();
    }

    private void Start()
    {
        CacheHomeTransform();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    public void CacheHomeTransform()
    {
        if (homeParent == null && transform.parent != null)
        {
            homeParent = transform.parent;
            homeLocalPosition = transform.localPosition;
            homeLocalRotation = transform.localRotation;
            homeLocalScale = transform.localScale;
        }
        else if (homeParent == null)
        {
            var table1 = GameObject.Find("Table 1") ?? GameObject.Find("Table1");
            if (table1 != null)
            {
                homeParent = table1.transform;
            }
            homeLocalPosition = transform.localPosition;
            homeLocalRotation = transform.localRotation;
            homeLocalScale = transform.localScale;
        }
    }

    public void EnsureAttachPoints()
    {
        if (leftAttachPoint == null)
        {
            Transform existing = transform.Find("LeftGrabAttach");
            if (existing != null) leftAttachPoint = existing;
            else
            {
                GameObject leftGo = new GameObject("LeftGrabAttach");
                leftGo.transform.SetParent(transform, false);
                leftGo.transform.localPosition = leftAttachLocalOffset;
                leftGo.transform.localRotation = Quaternion.identity;
                leftAttachPoint = leftGo.transform;
            }
        }

        if (rightAttachPoint == null)
        {
            Transform existing = transform.Find("RightGrabAttach");
            if (existing != null) rightAttachPoint = existing;
            else
            {
                GameObject rightGo = new GameObject("RightGrabAttach");
                rightGo.transform.SetParent(transform, false);
                rightGo.transform.localPosition = rightAttachLocalOffset;
                rightGo.transform.localRotation = Quaternion.identity;
                rightAttachPoint = rightGo.transform;
            }
        }
    }

    private void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        isBeingHeld = true;

        if (returnCo != null)
        {
            StopCoroutine(returnCo);
            returnCo = null;
        }

        EnsureAttachPoints();
        RouteAttachPointForInteractor(args.interactorObject);
        CheckGripState();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (grab == null || grab.interactorsSelecting.Count == 0)
        {
            isBeingHeld = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            // Start auto-return timer to Table 1 if unheld
            if (returnCo != null) StopCoroutine(returnCo);
            returnCo = StartCoroutine(AutoReturnToTable1Routine(0.85f));
        }
        else
        {
            CheckGripState();
        }
    }

    private void RouteAttachPointForInteractor(IXRSelectInteractor interactor)
    {
        if (interactor == null || grab == null) return;

        string interactorName = interactor.transform.name.ToLower();
        bool isLeftHand = interactorName.Contains("left");
        bool isRightHand = interactorName.Contains("right");

        Transform chosenAttach = null;
        if (isLeftHand && leftAttachPoint != null)
        {
            chosenAttach = leftAttachPoint;
        }
        else if (isRightHand && rightAttachPoint != null)
        {
            chosenAttach = rightAttachPoint;
        }
        else if (leftAttachPoint != null && rightAttachPoint != null)
        {
            float dLeft = Vector3.Distance(interactor.transform.position, leftAttachPoint.position);
            float dRight = Vector3.Distance(interactor.transform.position, rightAttachPoint.position);
            chosenAttach = (dLeft <= dRight) ? leftAttachPoint : rightAttachPoint;
        }

        if (chosenAttach != null)
        {
            grab.attachTransform = chosenAttach;
        }
    }

    public bool IsHeldWithTwoHands()
    {
        if (grab == null) return false;
        return grab.interactorsSelecting.Count >= 2;
    }

    public bool IsBeingHeld()
    {
        if (grab == null) return false;
        return grab.interactorsSelecting.Count > 0;
    }

    private void CheckGripState()
    {
        if (grab == null) return;
        int handCount = grab.interactorsSelecting.Count;

        if (handCount == 1)
        {
            if (MotherDrapingManager.Instance != null && MotherDrapingManager.Instance.isDrapingActive)
            {
                if (Time.time - lastOneHandWarningTime > 2.5f)
                {
                    lastOneHandWarningTime = Time.time;
                    if (VRDemoGameManager.Instance != null)
                    {
                        VRDemoGameManager.Instance.ShowWarning("Sterile Technique: Grab the linen with BOTH hands to unfold and drape.");
                    }
                }
            }
        }
        else if (handCount >= 2)
        {
            if (leftAttachPoint != null && rightAttachPoint != null)
            {
                grab.attachTransform = leftAttachPoint;
                grab.secondaryAttachTransform = rightAttachPoint;
            }
        }
    }

    private void Update()
    {
        // Safety check: if dropped below floor level while unheld, return immediately
        if (!isBeingHeld && transform.position.y < 0.25f)
        {
            ResetToTable1(true);
        }
    }

    private IEnumerator AutoReturnToTable1Routine(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!isBeingHeld && (grab == null || grab.interactorsSelecting.Count == 0))
        {
            ResetToTable1(false);
        }

        returnCo = null;
    }

    /// <summary>
    /// Smoothly / instantly returns the linen back to its exact Table 1 home transform.
    /// </summary>
    public void ResetToTable1(bool forceInstant = false)
    {
        if (returnCo != null)
        {
            StopCoroutine(returnCo);
            returnCo = null;
        }

        if (grab != null && grab.isSelected)
        {
            var mgr = grab.interactionManager;
            if (mgr != null)
            {
                var selecting = new List<IXRSelectInteractor>(grab.interactorsSelecting);
                foreach (var holder in selecting)
                {
                    mgr.SelectExit(holder, grab);
                }
            }
        }

        isBeingHeld = false;

        if (homeParent != null)
        {
            transform.SetParent(homeParent);
            transform.localPosition = homeLocalPosition;
            transform.localRotation = homeLocalRotation;
            transform.localScale = homeLocalScale;
        }
        else
        {
            transform.position = new Vector3(0.892f, 1.09f, 1.338f);
            transform.rotation = Quaternion.Euler(-90f, 0f, 8f);
            transform.localScale = homeLocalScale;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
        Debug.Log($"[TwoHandedLinenCloth] {gameObject.name} returned cleanly to Table 1.");
    }

    private void OnDrawGizmosSelected()
    {
        if (leftAttachPoint != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.85f);
            Gizmos.DrawSphere(leftAttachPoint.position, 0.05f);
            Gizmos.DrawWireSphere(leftAttachPoint.position, 0.08f);
        }

        if (rightAttachPoint != null)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.85f);
            Gizmos.DrawSphere(rightAttachPoint.position, 0.05f);
            Gizmos.DrawWireSphere(rightAttachPoint.position, 0.08f);
        }
    }
}
