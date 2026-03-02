using UnityEngine;
using UnityEngine.Events;

public class InfiniteStaircase : MonoBehaviour
{
    [Header("Loop Markers")]
    [Tooltip("Position at the top of Segment A where the player arrives after teleport")]
    [SerializeField] private Transform loopStartMarker;

    [Tooltip("Matching position at the bottom of Segment B where teleport fires")]
    [SerializeField] private Transform loopEndMarker;

    [Header("Loop Settings")]
    [Tooltip("How many times the player has looped (read-only)")]
    [SerializeField] private int loopCount = 0;

    [Tooltip("Set to -1 for infinite loops, or a positive number to break the loop after N iterations")]
    [SerializeField] private int maxLoops = -1;

    [Header("Events")]
    [Tooltip("Fires each time the player loops (for progressive horror effects)")]
    public UnityEvent onPlayerLooped;

    [Tooltip("Fires when maxLoops is reached (for game events like opening a new path)")]
    public UnityEvent onLoopComplete;

    private bool loopActive = true;

    public int LoopCount => loopCount;

    private void OnTriggerEnter(Collider other)
    {
        if (!loopActive) return;
        if (!other.CompareTag("Player")) return;

        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc == null) return;

        // Calculate the offset from the end marker to the start marker
        Vector3 offset = loopStartMarker.position - loopEndMarker.position;

        // Teleport: disable CharacterController, move, re-enable
        cc.enabled = false;
        other.transform.position += offset;
        cc.enabled = true;

        // Refresh zone tracking since OnTriggerEnter/Exit won't fire on teleport
        PlayerZoneTracker.RefreshZonesAfterTeleport();

        // Track loops
        loopCount++;
        onPlayerLooped?.Invoke();

        // Check if we've reached the max loop count
        if (maxLoops > 0 && loopCount >= maxLoops)
        {
            loopActive = false;
            onLoopComplete?.Invoke();
        }
    }

    /// <summary>
    /// Reset the loop counter and reactivate the loop.
    /// </summary>
    public void ResetLoop()
    {
        loopCount = 0;
        loopActive = true;
    }

    /// <summary>
    /// Manually deactivate the loop (e.g., from another script or event).
    /// </summary>
    public void DeactivateLoop()
    {
        loopActive = false;
    }
}
