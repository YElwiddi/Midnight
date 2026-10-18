using System.Collections;
using UnityEngine;

/// <summary>
/// Moves the main menu camera over the front gate: a slow handheld drift at rest, and a push toward
/// the cabin while the screen fades when the night starts. Unscaled time throughout.
/// </summary>
public class MenuCameraDirector : MonoBehaviour
{
    [Header("Idle drift")]
    [Tooltip("Position wander, metres.")]
    public float driftPosition = 0.06f;
    [Tooltip("Rotation wander, degrees.")]
    public float driftRotation = 0.45f;
    public float driftSpeed = 0.08f;

    [Header("Play")]
    [Tooltip("Pose the camera pushes toward when the night starts (near the cabin's lit side).")]
    public Transform pushTarget;

    private Vector3 basePos;
    private Quaternion baseRot;
    private float driftWeight = 1f;
    private float seed;
    private Coroutine motion;

    public bool IsBusy => motion != null;

    private void Awake()
    {
        basePos = transform.position;
        baseRot = transform.rotation;
        seed = Random.value * 100f;
    }

    private void LateUpdate()
    {
        float t = Time.unscaledTime * driftSpeed;
        float w = driftWeight;
        var off = new Vector3(Mathf.PerlinNoise(seed, t) - 0.5f, Mathf.PerlinNoise(seed + 13.1f, t) - 0.5f, 0f) * (2f * driftPosition * w);
        var rot = new Vector3(Mathf.PerlinNoise(seed + 27.3f, t) - 0.5f, Mathf.PerlinNoise(seed + 41.7f, t) - 0.5f, 0f) * (2f * driftRotation * w);
        transform.position = basePos + baseRot * off;
        transform.rotation = baseRot * Quaternion.Euler(rot);
    }

    /// <summary>Eases toward the push target over <paramref name="duration"/> seconds, settling the drift.</summary>
    public void Push(float duration)
    {
        if (pushTarget == null) return;
        if (motion != null) StopCoroutine(motion);
        motion = StartCoroutine(PushRoutine(duration));
    }

    private IEnumerator PushRoutine(float duration)
    {
        Vector3 fromPos = basePos;
        Quaternion fromRot = baseRot;
        for (float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            float e = t * t * (3f - 2f * t) * t;    // slow start, accelerating into the fade
            basePos = Vector3.Lerp(fromPos, pushTarget.position, e);
            baseRot = Quaternion.Slerp(fromRot, pushTarget.rotation, e);
            driftWeight = 1f - t;
            yield return null;
        }
        motion = null;
    }
}
