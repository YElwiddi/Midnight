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
    public bool ShovelBroken = false;
    public bool GraveyardEndingTriggered = false;
    public bool ChurchInsanity = false;
    public bool BrideKillerReady = false;

    [Header("Dirt Pile Stats")]
    public int IncorrectDigCount = 0;
    public int CorrectDigCount = 0;

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
            // Sanity system stats (read from their managers)
            case "sanity":
                return SanityManager.Instance != null ? SanityManager.Instance.CurrentSanity : 100;
            case "graveyardprotection":
            case "protection":
                return GraveyardProtectionManager.Instance != null ? GraveyardProtectionManager.Instance.CurrentProtection : 100;
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
            // Sanity system stats (delegate to their managers)
            case "sanity":
                if (SanityManager.Instance != null)
                {
                    if (value > 0)
                        SanityManager.Instance.RestoreSanity(value);
                    else
                        SanityManager.Instance.DrainSanity(-value);
                    Debug.Log($"Sanity changed by {value}. New value: {SanityManager.Instance.CurrentSanity}");
                }
                break;
            case "graveyardprotection":
            case "protection":
                if (GraveyardProtectionManager.Instance != null)
                {
                    if (value > 0)
                        GraveyardProtectionManager.Instance.RestoreProtection(value);
                    else
                        GraveyardProtectionManager.Instance.DrainProtection(-value);
                    Debug.Log($"GraveyardProtection changed by {value}. New value: {GraveyardProtectionManager.Instance.CurrentProtection}");
                }
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
            case "shovelbroken": return ShovelBroken;
            case "graveyardendtriggered": return GraveyardEndingTriggered;
            case "churchinsanity": return ChurchInsanity;
            case "bridekillerready": return BrideKillerReady;
            default:
                Debug.LogWarning($"GameManager: Bool flag '{flagName}' not found");
                return false;
        }
    }

    /// <summary>
    /// Checks whether a boolean flag name is registered in the GameManager.
    /// </summary>
    public bool HasBoolFlag(string flagName)
    {
        switch (flagName.ToLower())
        {
            case "cryptunlocked":
            case "shovelpickedup":
            case "shovelbroken":
            case "graveyardendtriggered":
            case "churchinsanity":
            case "bridekillerready":
                return true;
            default:
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
            case "shovelbroken":
                ShovelBroken = value;
                Debug.Log($"ShovelBroken set to {value}");
                break;
            case "graveyardendtriggered":
                GraveyardEndingTriggered = value;
                Debug.Log($"GraveyardEndingTriggered set to {value}");
                break;
            case "churchinsanity":
                ChurchInsanity = value;
                Debug.Log($"ChurchInsanity set to {value}");
                break;
            case "bridekillerready":
                BrideKillerReady = value;
                Debug.Log($"BrideKillerReady set to {value}");
                break;
            default:
                Debug.LogWarning($"Bool flag {flagName} not found in GameManager");
                break;
        }
    }

    /// <summary>
    /// Resets all game state to initial values. Called when returning to main menu.
    /// </summary>
    public void ResetAllFlags()
    {
        // Reset player stats
        player_scared = 0;
        player_mean = 0;
        player_stupid = 0;
        SpiritAngered = 0;
        GraveRobberSetup = 0;
        GraveKeeperAngered = 0;

        // Reset boolean flags
        CryptUnlocked = false;
        ShovelPickedUp = false;
        ShovelBroken = false;
        GraveyardEndingTriggered = false;
        ChurchInsanity = false;
        BrideKillerReady = false;

        // Reset dirt pile stats
        IncorrectDigCount = 0;
        CorrectDigCount = 0;

        Debug.Log("GameManager: All flags and stats reset");
    }

    // Method to display current stats (for debugging)
    private void OnGUI()
    {
        if (!showDebugStats) return;

        GUI.Box(new Rect(10, 10, 200, 290), "Player Stats");
        GUI.Label(new Rect(20, 30, 180, 20), $"Scared: {player_scared}");
        GUI.Label(new Rect(20, 50, 180, 20), $"Mean: {player_mean}");
        GUI.Label(new Rect(20, 70, 180, 20), $"Stupid: {player_stupid}");
        GUI.Label(new Rect(20, 90, 180, 20), $"SpiritAngered: {SpiritAngered}");
        GUI.Label(new Rect(20, 110, 180, 20), $"GraveRobberSetup: {GraveRobberSetup}");
        GUI.Label(new Rect(20, 130, 180, 20), $"GraveKeeperAngered: {GraveKeeperAngered}");
        GUI.Label(new Rect(20, 150, 180, 20), $"CryptUnlocked: {CryptUnlocked}");
        GUI.Label(new Rect(20, 170, 180, 20), $"ShovelPickedUp: {ShovelPickedUp}");
        GUI.Label(new Rect(20, 190, 180, 20), $"ShovelBroken: {ShovelBroken}");
        GUI.Label(new Rect(20, 210, 180, 20), $"CorrectDigCount: {CorrectDigCount}");
        GUI.Label(new Rect(20, 230, 180, 20), $"IncorrectDigCount: {IncorrectDigCount}");
        GUI.Label(new Rect(20, 250, 180, 20), $"ChurchInsanity: {ChurchInsanity}");
        GUI.Label(new Rect(20, 270, 180, 20), $"BrideKillerReady: {BrideKillerReady}");
    }
}
