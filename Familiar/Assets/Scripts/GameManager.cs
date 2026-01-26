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
    public int GraveKeeperAngered = 0;

    [Header("Boolean Flags")]
    public bool CryptUnlocked = false;
    public bool ShovelPickedUp = false;
    public bool CorrectGraveFound = false;

    [Header("Dirt Pile Stats")]
    public int IncorrectDigCount = 0;

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
            case "gravekeeperangered": return GraveKeeperAngered;
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
            case "gravekeeperangered":
                GraveKeeperAngered += value;
                Debug.Log($"GraveKeeperAngered changed by {value}. New value: {GraveKeeperAngered}");
                break;
            default:
                Debug.LogWarning($"Variable {varName} not found in GameManager");
                break;
        }
    }

    /// <summary>
    /// Gets a boolean flag value by name.
    /// </summary>
    public bool GetBoolFlag(string flagName)
    {
        switch (flagName.ToLower())
        {
            case "cryptunlocked": return CryptUnlocked;
            case "shovelpickedup": return ShovelPickedUp;
            case "correctgravefound": return CorrectGraveFound;
            default:
                Debug.LogWarning($"GameManager: Bool flag '{flagName}' not found");
                return false;
        }
    }

    /// <summary>
    /// Sets a boolean flag value by name.
    /// </summary>
    public void SetBoolFlag(string flagName, bool value)
    {
        switch (flagName.ToLower())
        {
            case "cryptunlocked":
                CryptUnlocked = value;
                Debug.Log($"CryptUnlocked set to {value}");
                break;
            case "shovelpickedup":
                ShovelPickedUp = value;
                Debug.Log($"ShovelPickedUp set to {value}");
                break;
            case "correctgravefound":
                CorrectGraveFound = value;
                Debug.Log($"CorrectGraveFound set to {value}");
                break;
            default:
                Debug.LogWarning($"Bool flag {flagName} not found in GameManager");
                break;
        }
    }

    // Method to display current stats (for debugging)
    private void OnGUI()
    {
        if (!showDebugStats) return;

        GUI.Box(new Rect(10, 10, 200, 230), "Player Stats");
        GUI.Label(new Rect(20, 30, 180, 20), $"Scared: {player_scared}");
        GUI.Label(new Rect(20, 50, 180, 20), $"Mean: {player_mean}");
        GUI.Label(new Rect(20, 70, 180, 20), $"Stupid: {player_stupid}");
        GUI.Label(new Rect(20, 90, 180, 20), $"SpiritAngered: {SpiritAngered}");
        GUI.Label(new Rect(20, 110, 180, 20), $"GraveRobberSetup: {GraveRobberSetup}");
        GUI.Label(new Rect(20, 130, 180, 20), $"GraveKeeperAngered: {GraveKeeperAngered}");
        GUI.Label(new Rect(20, 150, 180, 20), $"CryptUnlocked: {CryptUnlocked}");
        GUI.Label(new Rect(20, 170, 180, 20), $"ShovelPickedUp: {ShovelPickedUp}");
        GUI.Label(new Rect(20, 190, 180, 20), $"CorrectGraveFound: {CorrectGraveFound}");
        GUI.Label(new Rect(20, 210, 180, 20), $"IncorrectDigCount: {IncorrectDigCount}");
    }
}
