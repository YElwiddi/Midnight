using System;
using UnityEngine;

/// <summary>
/// Player look settings shared by the Settings screen (main menu and pause menu) and the player
/// controller. Stored in PlayerPrefs; <see cref="Changed"/> lets Movement pick up edits made while
/// the game is paused.
/// </summary>
public static class GameSettings
{
    public const string MouseSensitivityKey = "MouseSensitivity";
    public const string InvertLookKey = "InvertLook";

    /// <summary>Movement.lookSpeed the game was tuned with.</summary>
    public const float DefaultSensitivity = 2f;
    public const float MinSensitivity = 0.5f;
    public const float MaxSensitivity = 5f;

    public static event Action Changed;

    public static float MouseSensitivity
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultSensitivity), MinSensitivity, MaxSensitivity); }
        set
        {
            PlayerPrefs.SetFloat(MouseSensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
            Changed?.Invoke();
        }
    }

    public static bool InvertLook
    {
        get { return PlayerPrefs.GetInt(InvertLookKey, 0) == 1; }
        set
        {
            PlayerPrefs.SetInt(InvertLookKey, value ? 1 : 0);
            Changed?.Invoke();
        }
    }
}
