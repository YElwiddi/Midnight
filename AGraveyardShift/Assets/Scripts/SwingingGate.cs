using System.Collections;
using UnityEngine;

/// <summary>
/// A single gate leaf (BackGate1 / BackGate2) that swings around its hinge.
/// The leaf's pivot already sits at the hinge edge, so we simply rotate the
/// local rotation around the hinge axis. The decision of when to open/close is
/// made by <see cref="BackGateController"/>; this component owns the animation
/// and forwards interaction to the controller.
///
/// Swing direction is set by the sign of <see cref="openAngle"/> — both leaves
/// of the double gate are configured to swing the same way (outward).
/// </summary>
public class SwingingGate : MonoBehaviour, IInteractable
{
    public enum GateState { Closed, Open }

    [Header("References")]
    [Tooltip("Controller that owns the back-gate logic. Auto-found in parents if left empty.")]
    [SerializeField] private BackGateController controller;

    [Header("Swing")]
    [Tooltip("Angle (degrees, around the hinge axis) the leaf rotates to when open. Sign sets the swing direction.")]
    [SerializeField] private float openAngle = 70f;
    [Tooltip("Local axis the leaf rotates around (the hinge). Usually up.")]
    [SerializeField] private Vector3 hingeAxis = Vector3.up;
    [Tooltip("Seconds for a full open/close swing.")]
    [SerializeField] private float swingDuration = 1.2f;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "Open";

    private Quaternion closedLocalRotation;
    private GateState currentState = GateState.Closed;
    private Coroutine swingRoutine;

    /// <summary>The leaf's current logical state.</summary>
    public GateState CurrentState => currentState;

    /// <summary>True when the leaf is open.</summary>
    public bool IsOpen => currentState == GateState.Open;

    private void Awake()
    {
        closedLocalRotation = transform.localRotation;
        if (controller == null) controller = GetComponentInParent<BackGateController>();
    }

    // IInteractable
    public void Interact()
    {
        if (controller != null) controller.OnGateInteracted();
    }

    public string GetInteractionPrompt() => interactionPrompt;

    /// <summary>Swings the leaf open or closed with a smooth animation.</summary>
    public void SetOpen(bool open)
    {
        SwingTo(open ? GateState.Open : GateState.Closed);
    }

    /// <summary>Swings the leaf to the given state with a smooth animation.</summary>
    public void SwingTo(GateState state)
    {
        currentState = state;
        Quaternion target = TargetRotationFor(state);

        if (!gameObject.activeInHierarchy)
        {
            transform.localRotation = target;
            return;
        }

        if (swingRoutine != null) StopCoroutine(swingRoutine);
        swingRoutine = StartCoroutine(SwingRoutine(target));
    }

    /// <summary>Snaps to a state instantly (no animation). Used to restore saved state.</summary>
    public void SetStateInstant(GateState state)
    {
        currentState = state;
        if (swingRoutine != null) { StopCoroutine(swingRoutine); swingRoutine = null; }
        transform.localRotation = TargetRotationFor(state);
    }

    /// <summary>
    /// Swings the leaf to a specific angle (degrees from closed), in the same direction as
    /// <see cref="openAngle"/>. Used for the low-protection "creak": a partial, non-passable
    /// opening. Marks the leaf Open when the angle is non-zero.
    /// </summary>
    public void SwingToAngle(float degrees)
    {
        float dir = openAngle < 0f ? -1f : 1f;
        float signed = dir * Mathf.Abs(degrees);
        currentState = Mathf.Abs(signed) > 0.01f ? GateState.Open : GateState.Closed;
        Quaternion target = closedLocalRotation * Quaternion.AngleAxis(signed, hingeAxis.normalized);
        if (!gameObject.activeInHierarchy)
        {
            transform.localRotation = target;
            return;
        }
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        swingRoutine = StartCoroutine(SwingRoutine(target));
    }

    private Quaternion TargetRotationFor(GateState state)
    {
        float angle = state == GateState.Open ? openAngle : 0f;
        return closedLocalRotation * Quaternion.AngleAxis(angle, hingeAxis.normalized);
    }

    private IEnumerator SwingRoutine(Quaternion target)
    {
        Quaternion start = transform.localRotation;
        float dur = Mathf.Max(0.01f, swingDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / dur);
            transform.localRotation = Quaternion.Slerp(start, target, u);
            yield return null;
        }
        transform.localRotation = target;
        swingRoutine = null;
    }
}
