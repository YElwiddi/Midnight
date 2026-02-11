using UnityEngine;

/// <summary>
/// Configuration for a cinematic waypoint during player autopilot.
/// </summary>
[System.Serializable]
public class CinematicWaypointData
{
    [Tooltip("Name of the GameObject to use as the waypoint target")]
    public string waypointName;

    [Tooltip("Movement speed to reach this waypoint")]
    [Range(0.5f, 10f)]
    public float moveSpeed = 2f;

    [Tooltip("Time to wait at this waypoint before continuing")]
    [Range(0f, 10f)]
    public float waitTime = 0f;
}

/// <summary>
/// ScriptableObject that defines a cinematic ending sequence.
/// The player character walks along waypoints while the player watches.
/// Create via: Right-click -> Create -> Game -> Cinematic Ending Data
/// </summary>
[CreateAssetMenu(fileName = "New Cinematic Ending", menuName = "Game/Cinematic Ending Data")]
public class CinematicEndingData : ScriptableObject
{
    [Header("Cinematic Info")]
    [Tooltip("Name for this cinematic ending (for debugging)")]
    public string cinematicName = "New Cinematic Ending";

    [Header("Player Waypoints")]
    [Tooltip("Waypoints the player will walk through during the cinematic")]
    public CinematicWaypointData[] playerWaypoints;

    [Header("Fade Settings")]
    [Tooltip("Duration of the fade to black after player finishes walking")]
    [Range(0.5f, 5f)]
    public float fadeOutDuration = 2f;

    [Tooltip("Delay after reaching final waypoint before starting fade")]
    [Range(0f, 5f)]
    public float delayBeforeFade = 1f;

    [Header("Ending Screen")]
    [Tooltip("Title text shown on the ending screen")]
    public string endingTitle = "The End";

    [Tooltip("Description of what happens after (shown below title)")]
    [TextArea(3, 6)]
    public string endingDescription = "";

    [Tooltip("Duration to show the ending screen before returning to menu")]
    [Range(3f, 30f)]
    public float endingScreenDuration = 8f;

    [Tooltip("Scene name to load after ending (leave empty to quit application)")]
    public string menuSceneName = "MainMenu";

    [Header("Audio")]
    [Tooltip("Music/ambience to play during the cinematic walk")]
    public AudioClip cinematicMusic;

    [Range(0f, 1f)]
    public float musicVolume = 0.5f;

    [Tooltip("If true, fades out current audio before playing cinematic music")]
    public bool fadeOutCurrentAudio = true;

    [Range(0.5f, 3f)]
    public float audioFadeOutDuration = 1f;
}
