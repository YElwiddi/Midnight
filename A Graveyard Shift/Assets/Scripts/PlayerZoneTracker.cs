using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks which zones the player is currently in using trigger colliders.
/// Attach this to the Player object. Zone objects should have trigger colliders and be tagged "Zone" or have "Zone" in their name.
/// When the player teleports, RefreshZonesAfterTeleport() notifies exited zones via SendMessage("OnZoneExitByTeleport")
/// and entered zones via SendMessage("OnZoneEnterByTeleport").
/// </summary>
public class PlayerZoneTracker : MonoBehaviour
{
    public static PlayerZoneTracker Instance { get; private set; }

    private Dictionary<string, Collider> currentZones = new Dictionary<string, Collider>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Track any trigger collider the player enters
        string zoneName = other.gameObject.name;
        if (!currentZones.ContainsKey(zoneName))
        {
            currentZones[zoneName] = other;
            Debug.Log($"PlayerZoneTracker: Entered zone '{zoneName}'");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        string zoneName = other.gameObject.name;
        if (currentZones.ContainsKey(zoneName))
        {
            currentZones.Remove(zoneName);
            Debug.Log($"PlayerZoneTracker: Exited zone '{zoneName}'");
        }
    }

    /// <summary>
    /// Check if the player is currently in the specified zone.
    /// </summary>
    public static bool IsInZone(string zoneName)
    {
        if (Instance == null)
        {
            Debug.LogWarning("PlayerZoneTracker: No instance found. Add PlayerZoneTracker to the Player object.");
            return false;
        }

        return Instance.currentZones.ContainsKey(zoneName);
    }

    /// <summary>
    /// Get all zones the player is currently in.
    /// </summary>
    public static IEnumerable<string> GetCurrentZones()
    {
        if (Instance == null) return new string[0];
        return Instance.currentZones.Keys;
    }

    /// <summary>
    /// Refreshes zone tracking after teleportation by checking which triggers the player is now inside.
    /// Call this after teleporting the player.
    /// </summary>
    public static void RefreshZonesAfterTeleport()
    {
        if (Instance == null) return;
        Instance.RefreshZones();
    }

    private void RefreshZones()
    {
        // Store old zones to detect exits
        Dictionary<string, Collider> oldZones = new Dictionary<string, Collider>(currentZones);
        currentZones.Clear();

        // Find all trigger colliders overlapping the player's position
        Collider[] overlaps = Physics.OverlapSphere(transform.position, 0.5f);
        foreach (Collider col in overlaps)
        {
            if (col.isTrigger && col.gameObject != gameObject)
            {
                string zoneName = col.gameObject.name;
                currentZones[zoneName] = col;

                if (!oldZones.ContainsKey(zoneName))
                {
                    Debug.Log($"PlayerZoneTracker: Entered zone '{zoneName}' (after teleport)");
                    col.gameObject.SendMessage("OnZoneEnterByTeleport", SendMessageOptions.DontRequireReceiver);
                }
                oldZones.Remove(zoneName);
            }
        }

        // Notify zones we exited
        foreach (var kvp in oldZones)
        {
            Debug.Log($"PlayerZoneTracker: Exited zone '{kvp.Key}' (after teleport)");
            if (kvp.Value != null)
            {
                kvp.Value.gameObject.SendMessage("OnZoneExitByTeleport", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
