using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Player Stats")]
    public int playerKarma = 0;
    public bool questAccepted = false;

    [Header("Other Game Variables")]
    public int playerGold = 100;
    public int playerLevel = 1;

    private static GameManager instance;

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

    public void ChangeVariable(string varName, int value)
    {
        switch (varName)
        {
            case "karma":
                playerKarma += value;
                Debug.Log($"Karma changed by {value}. New karma: {playerKarma}");
                break;
            case "gold":
                playerGold += value;
                Debug.Log($"Gold changed by {value}. New gold: {playerGold}");
                break;
            case "level":
                playerLevel = value;
                Debug.Log($"Level set to: {playerLevel}");
                break;
            default:
                Debug.LogWarning($"Variable {varName} not found in GameManager");
                break;
        }
    }

    public void SetQuestStatus(string questName, bool status)
    {
        if (questName == "main_quest")
        {
            questAccepted = status;
            Debug.Log($"Quest '{questName}' accepted: {status}");
        }
    }

    // Method to display current stats (for debugging)
    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 200, 90), "Game Stats");
        GUI.Label(new Rect(20, 30, 180, 20), $"Karma: {playerKarma}");
        GUI.Label(new Rect(20, 50, 180, 20), $"Gold: {playerGold}");
        GUI.Label(new Rect(20, 70, 180, 20), $"Quest Accepted: {questAccepted}");
    }
}