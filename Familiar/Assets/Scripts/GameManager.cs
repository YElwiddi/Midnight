using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Player Stats")]
    public int player_friendly = 0;
    public int player_scared = 0;
    public int player_brave = 0;
    public int player_mean = 0;
    public int player_smart = 0;
    public int player_stupid = 0;

    [Header("Bool Flags")]
    public bool metHuang = false;
    public bool helpedHuang = false;
    public bool metCat = false;
    public bool befriendedCat = false;

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
            case "friendly": return player_friendly;
            case "scared": return player_scared;
            case "brave": return player_brave;
            case "mean": return player_mean;
            case "smart": return player_smart;
            case "stupid": return player_stupid;
            default:
                Debug.LogWarning($"GameManager: Stat '{statName}' not found");
                return 0;
        }
    }

    /// <summary>
    /// Gets a bool flag value by name.
    /// </summary>
    public bool GetBoolValue(string boolName)
    {
        switch (boolName.ToLower())
        {
            case "methuang": return metHuang;
            case "helpedhuang": return helpedHuang;
            case "metcat": return metCat;
            case "befriendedcat": return befriendedCat;
            default:
                Debug.LogWarning($"GameManager: Bool '{boolName}' not found");
                return false;
        }
    }

    /// <summary>
    /// Sets a bool flag value by name.
    /// </summary>
    public void SetBoolValue(string boolName, bool value)
    {
        switch (boolName.ToLower())
        {
            case "methuang": metHuang = value; break;
            case "helpedhuang": helpedHuang = value; break;
            case "metcat": metCat = value; break;
            case "befriendedcat": befriendedCat = value; break;
            default:
                Debug.LogWarning($"GameManager: Bool '{boolName}' not found");
                break;
        }
        Debug.Log($"GameManager: {boolName} set to {value}");
    }

    public void ChangeVariable(string varName, int value)
    {
        switch (varName)
        {
            case "friendly":
                player_friendly += value;
                Debug.Log($"Friendly changed by {value}. New value: {player_friendly}");
                break;
            case "scared":
                player_scared += value;
                Debug.Log($"Scared changed by {value}. New value: {player_scared}");
                break;
            case "brave":
                player_brave += value;
                Debug.Log($"Brave changed by {value}. New value: {player_brave}");
                break;
            case "mean":
                player_mean += value;
                Debug.Log($"Mean changed by {value}. New value: {player_mean}");
                break;
            case "smart":
                player_smart += value;
                Debug.Log($"Smart changed by {value}. New value: {player_smart}");
                break;
            case "stupid":
                player_stupid += value;
                Debug.Log($"Stupid changed by {value}. New value: {player_stupid}");
                break;
            default:
                Debug.LogWarning($"Variable {varName} not found in GameManager");
                break;
        }
    }

    // Method to display current stats (for debugging)
    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 200, 150), "Player Stats");
        GUI.Label(new Rect(20, 30, 180, 20), $"Friendly: {player_friendly}");
        GUI.Label(new Rect(20, 50, 180, 20), $"Scared: {player_scared}");
        GUI.Label(new Rect(20, 70, 180, 20), $"Brave: {player_brave}");
        GUI.Label(new Rect(20, 90, 180, 20), $"Mean: {player_mean}");
        GUI.Label(new Rect(20, 110, 180, 20), $"Smart: {player_smart}");
        GUI.Label(new Rect(20, 130, 180, 20), $"Stupid: {player_stupid}");
    }
}