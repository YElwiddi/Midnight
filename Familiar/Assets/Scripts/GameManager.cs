using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugStats = true;

    [Header("Player Stats")]
    public int player_scared = 0;
    public int player_mean = 0;
    public int player_stupid = 0;
    public int SpiritAngered = 0;
    public int GraveRobberSetup = 0;

    private static GameManager instance;
    public static GameManager Instance => instance;

    private void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Gets an integer stat value by name.
    /// </summary>
    public int GetStatValue(string statName)
    {
        switch (statName.ToLower())
        {
            case "scared": return player_scared;
            case "mean": return player_mean;
            case "stupid": return player_stupid;
            case "spiritangered": return SpiritAngered;
            case "graverobbersetup": return GraveRobberSetup;
            default:
                Debug.LogWarning($"GameManager: Stat '{statName}' not found");
                return 0;
        }
    }

    public void ChangeVariable(string varName, int value)
    {
        switch (varName)
        {
            case "scared":
                player_scared += value;
                Debug.Log($"Scared changed by {value}. New value: {player_scared}");
                break;
            case "mean":
                player_mean += value;
                Debug.Log($"Mean changed by {value}. New value: {player_mean}");
                break;
            case "stupid":
                player_stupid += value;
                Debug.Log($"Stupid changed by {value}. New value: {player_stupid}");
                break;
            case "spiritangered":
                SpiritAngered += value;
                Debug.Log($"SpiritAngered changed by {value}. New value: {SpiritAngered}");
                break;
            case "graverobbersetup":
                GraveRobberSetup += value;
                Debug.Log($"GraveRobberSetup changed by {value}. New value: {GraveRobberSetup}");
                break;
            default:
                Debug.LogWarning($"Variable {varName} not found in GameManager");
                break;
        }
    }

    // Method to display current stats (for debugging)
    private void OnGUI()
    {
        if (!showDebugStats) return;

        GUI.Box(new Rect(10, 10, 200, 130), "Player Stats");
        GUI.Label(new Rect(20, 30, 180, 20), $"Scared: {player_scared}");
        GUI.Label(new Rect(20, 50, 180, 20), $"Mean: {player_mean}");
        GUI.Label(new Rect(20, 70, 180, 20), $"Stupid: {player_stupid}");
        GUI.Label(new Rect(20, 90, 180, 20), $"SpiritAngered: {SpiritAngered}");
        GUI.Label(new Rect(20, 110, 180, 20), $"GraveRobberSetup: {GraveRobberSetup}");
    }
}
