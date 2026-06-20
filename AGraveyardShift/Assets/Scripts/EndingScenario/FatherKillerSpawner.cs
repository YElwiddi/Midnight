using UnityEngine;

/// <summary>
/// Spawns the FatherKiller prefab when the finale begins, so the killer is NOT present in the
/// scene for the whole game (it only exists from the rose-on-Father-grave teleport onward).
/// Mirrors EndingScenarioManager's spawn-on-demand pattern. Lives as a lightweight scene object;
/// GraveRoseInteractable.FatherSequence() calls <see cref="Begin"/> after the player arrives.
/// </summary>
public class FatherKillerSpawner : MonoBehaviour
{
    [Tooltip("The FatherKiller prefab to instantiate (Assets/Prefabs/FatherKiller.prefab).")]
    [SerializeField] private GameObject fatherKillerPrefab;

    [Tooltip("Where the killer spawns and lurks (its far spawn). If unset, spawns at this object.")]
    [SerializeField] private Transform spawnPoint;

    private FatherKillerSequence spawned;

    /// <summary>True once the killer has been spawned (guards against double-spawn).</summary>
    public bool HasSpawned => spawned != null;

    /// <summary>Spawns the FatherKiller at the spawn point and starts the finale.</summary>
    public void Begin()
    {
        if (spawned != null) return;
        if (fatherKillerPrefab == null)
        {
            Debug.LogError("FatherKillerSpawner: no FatherKiller prefab assigned.");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject go = Instantiate(fatherKillerPrefab, pos, rot);
        go.name = "FatherKiller";

        spawned = go.GetComponent<FatherKillerSequence>();
        if (spawned != null)
        {
            spawned.Begin(spawnPoint); // inject the far spawn (prefab can't reference a scene transform)
        }
        else
        {
            Debug.LogError("FatherKillerSpawner: spawned prefab has no FatherKillerSequence component.");
        }
    }
}
