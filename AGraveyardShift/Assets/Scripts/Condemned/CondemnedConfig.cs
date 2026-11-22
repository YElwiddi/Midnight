using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything the night's extra scares need at runtime (Cut Down, the window reflection).
/// Lives in Resources so a build includes the clips, sounds and materials it points to.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Condemned Config", fileName = "CondemnedConfig")]
public class CondemnedConfig : ScriptableObject
{
    public const string ResourcePath = "Condemned/CondemnedConfig";

    [Header("Cut Down: the first visitor you turned away, third watch, by the east grave row")]
    public bool cutDownEnabled = true;
    [Tooltip("The watch it can happen in.")]
    public int cutDownPhase = 3;
    [Tooltip("Only while the player's sanity is below this share of the maximum when they walk by (0.6 = below 60%).")]
    [Range(0f, 1f)] public float cutDownBelowSanity = 0.6f;
    [Tooltip("The hanging pose (sampled once).")]
    public AnimationClip hangClip;
    public float hangClipTime = 1.2f;
    [Tooltip("The whole soundtrack, from 0.4 s before the fall to the end.")]
    public AudioClip cutDownSound;
    public Material ropeMaterial;
    [Tooltip("0 keeps the visitor's own colours, 1 is fully grey.")]
    [Range(0f, 1f)] public float bodyDrain = 0.5f;
    public Color bodyTint = new Color(0.86f, 0.88f, 0.9f);
    public int cutDownSanityCost = 8;
    [Tooltip("Where the body comes down (the ground under it is found at runtime).")]
    public Vector3 dropPoint = new Vector3(-100.7f, 0f, 122.9f);
    public float triggerMinDistance = 3.2f;
    public float triggerMaxDistance = 11f;
    [Tooltip("Visitors who hang on their own (Maria). Turned away, they never hang here (the next one you turn away is used instead); let in, they turn Cut Down off for that night, so a night never has two hangings.")]
    public List<GameEvent> cutDownSkip = new List<GameEvent>();

    [Header("The window: reflections in the cabin glass, and what walks in behind you")]
    public bool reflectionsEnabled = true;
    [Range(0f, 1f)] public float reflectionStrength = 0.6f;
    [Tooltip("The desk lamp's light on the room behind you, in the reflection only (0 = none). Without it a black shape has nothing to stand out against.")]
    public float reflectionFill = 1.1f;
    [Tooltip("Soft light over the whole room, in the reflection only (0 = none).")]
    public float reflectionRoomFill = 2.0f;
    public float reflectionRoomRange = 18f;
    [Tooltip("Additive material the reflection is drawn with (Legacy Shaders/Particles/Additive).")]
    public Material reflectionMaterial;
    [Tooltip("Dark night glass for the panes (null keeps the glass as it is).")]
    public Material darkGlassMaterial;
    [Tooltip("Height in pixels of each pane's reflection.")]
    public int reflectionHeight = 320;
    [Tooltip("A little of the door lantern's glow on the door, in the reflection only (0 = none).")]
    public float doorSpill = 1.25f;
    public bool windowScareEnabled = true;
    [Tooltip("The watch from which it can happen (0 = any time in the night).")]
    public int windowScareFromPhase = 0;
    [Tooltip("How long the desk lamp has to be off, with you in the cabin, before it can happen. Only the desk lamp counts.")]
    public float windowScareDarkSeconds = 7f;
    [Tooltip("Then the chance for each second you look into a window. Once a night.")]
    [Range(0f, 1f)] public float windowScareChance = 0.15f;
    public GameObject silhouettePrefab;
    public AnimationClip silhouetteWalk;
    [Tooltip("Pure black, unlit.")]
    public Material silhouetteMaterial;
    public float silhouetteScale = 1.18f;
    [Tooltip("The real door shutting, heard behind you.")]
    public AudioClip doorShut;
    [Tooltip("One board, right behind you.")]
    public AudioClip boardCreak;
    [Tooltip("Sanity it costs once seen. The cabin is a sanity safe zone; this gets through it (never the last point).")]
    public int windowScareSanityCost = 15;
}
