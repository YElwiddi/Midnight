using System;
using UnityEngine;

/// <summary>The kinds of floor that get their own footstep sound.</summary>
public enum FootSurface
{
    Grass,
    Dirt,
    Stone,
    CabinWood,
    ChapelWood,
    Staircase,
    Crypt,
    Forest
}

/// <summary>
/// Footstep sounds per floor and the rules that decide which floor the player is standing on.
/// The asset lives at Resources/Footsteps/FootstepSettings and the clips in Resources/Footsteps/&lt;folder&gt;.
/// PlayerFootsteps reads it; without this asset the player keeps Movement's old single footstep set.
/// </summary>
[CreateAssetMenu(fileName = "FootstepSettings", menuName = "Graveyard/Footstep Settings")]
public class FootstepSettings : ScriptableObject
{
    public const string ResourcePath = "Footsteps/FootstepSettings";
    public const string ClipRoot = "Footsteps/";

    [Serializable]
    public class SurfaceSound
    {
        public FootSurface surface;
        [Tooltip("Folder under Resources/Footsteps holding this floor's steps")]
        public string folder;
        [Tooltip("Optional second folder played on some steps instead (the cabin's creaks)")]
        public string accentFolder;
        [Tooltip("Chance that a step uses the accent folder")]
        [Range(0f, 1f)] public float accentChance;
        [Tooltip("Playback volume. The clips are loudness-matched, so this sets the floor's level against the ambience")]
        [Range(0f, 1f)] public float volume = 0.1f;
    }

    [Serializable]
    public class Rule
    {
        [Tooltip("Text to look for")]
        public string match;
        public FootSurface surface;
    }

    [Header("Sounds")]
    public SurfaceSound[] sounds = new SurfaceSound[0];
    [Tooltip("Scales every floor at once")]
    [Range(0f, 2f)] public float masterVolume = 1f;
    [Tooltip("Random pitch spread per step (0.04 = up to 4% up or down)")]
    [Range(0f, 0.2f)] public float pitchJitter = 0.04f;
    [Tooltip("Random volume spread per step, in dB up or down")]
    [Range(0f, 6f)] public float volumeJitterDb = 1.5f;
    [Tooltip("Running steps are this many dB louder")]
    public float runGainDb = 2f;
    [Tooltip("Running steps are pitched up by this much")]
    public float runPitch = 0.02f;
    [Tooltip("A landing is this many dB louder than a step")]
    public float landGainDb = 3f;
    [Tooltip("A landing is pitched by this much (negative = heavier)")]
    public float landPitch = -0.04f;
    [Tooltip("Seconds in the air before touching down counts as a landing (a jump is about half a second)")]
    public float minAirTime = 0.3f;
    [Tooltip("A fall of at least this many metres also lands with the heavier step")]
    public float minDropHeight = 1f;

    [Header("Which floor you stand on")]
    [Tooltip("Checked first, in order: how the hierarchy path of the object under your feet starts (e.g. 'Interior Cabin')")]
    public Rule[] pathRules = new Rule[0];
    [Tooltip("Then, in order: text inside the terrain layer's texture name, or inside the floor material's or texture's name (any case)")]
    public Rule[] nameRules = new Rule[0];
    [Tooltip("Used when no rule matches")]
    public FootSurface fallback = FootSurface.Dirt;

    public SurfaceSound Find(FootSurface surface)
    {
        if (sounds == null) return null;
        foreach (var s in sounds) if (s != null && s.surface == surface) return s;
        return null;
    }

    /// <summary>The shipped values (the asset in Resources holds the same).</summary>
    public static FootstepSettings CreateDefault()
    {
        var s = CreateInstance<FootstepSettings>();
        s.name = "FootstepSettings";
        // volumes reproduce the levels of the Graveyard Footfalls pitch (2026-11-26) at the player's position,
        // except the crypt, halved on 2026-11-27 (it was too loud)
        s.sounds = new[]
        {
            Sound(FootSurface.Grass, "Grass", 0.0722f),
            Sound(FootSurface.Dirt, "Dirt", 0.1321f),
            Sound(FootSurface.Stone, "Stone", 0.1257f),
            Sound(FootSurface.CabinWood, "CabinHollow", 0.0675f, "CabinCreak", 0.3f),
            Sound(FootSurface.ChapelWood, "Chapel", 0.1556f),
            Sound(FootSurface.Staircase, "Staircase", 0.1913f),
            Sound(FootSurface.Crypt, "Crypt", 0.243f),
            Sound(FootSurface.Forest, "Forest", 0.1343f),
        };
        s.pathRules = new[]
        {
            R("Interior Cabin", FootSurface.CabinWood),
            R("eglise-sketchfab5/Interior PSX", FootSurface.ChapelWood),
            R("eglise-sketchfab5/SecretRoom", FootSurface.Staircase),
            R("InfiniteStaircase", FootSurface.Staircase),
            R("MapFBXFile", FootSurface.Crypt),
            R("The Forest/ProceduralRocks/AbandonedHouse", FootSurface.Stone),
            R("The Forest", FootSurface.Forest),
        };
        s.nameRules = new[]
        {
            R("road", FootSurface.Dirt),            // GroundRoad, Road_Turn, DirtRoad, Road(1)
            R("horror_misc_06", FootSurface.Dirt),  // the graveyard path layer
            R("dirt", FootSurface.Dirt),            // grave plots, mounds
            R("path", FootSurface.Dirt),
            R("farmland", FootSurface.Dirt),
            R("grass", FootSurface.Grass),
            R("ground", FootSurface.Grass),         // GraveyardMap Floor1 tiles
            R("stairs", FootSurface.Stone),
            R("fountain", FootSurface.Stone),
            R("mausoleum", FootSurface.Stone),
            R("stone", FootSurface.Stone),
            R("rock", FootSurface.Stone),
            R("brick", FootSurface.Stone),
            R("wall", FootSurface.Stone),
            R("concrete", FootSurface.Stone),       // tombstone plinths
            R("pillar", FootSurface.Stone),
            R("gate", FootSurface.Stone),
            R("metal", FootSurface.Stone),
            R("fance", FootSurface.Stone),
        };
        s.fallback = FootSurface.Dirt;
        return s;
    }

    static SurfaceSound Sound(FootSurface surface, string folder, float volume, string accent = null, float accentChance = 0f)
    {
        return new SurfaceSound { surface = surface, folder = folder, volume = volume, accentFolder = accent ?? "", accentChance = accentChance };
    }

    static Rule R(string match, FootSurface surface) { return new Rule { match = match, surface = surface }; }
}
