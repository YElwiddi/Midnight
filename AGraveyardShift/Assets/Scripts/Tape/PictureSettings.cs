using System;
using UnityEngine;

/// <summary>How the game picture is drawn (Settings > Picture).</summary>
public enum PictureMode
{
    /// <summary>The tape picture at full strength (the default).</summary>
    Tape = 0,
    /// <summary>The tape picture with less noise and wobble, and gentler wear.</summary>
    Light = 1,
    /// <summary>The original 320x240 pixel grid with the old VHS grade.</summary>
    Classic = 2,
}

/// <summary>
/// The player's Picture choice, saved in PlayerPrefs as soon as it changes. The camera effects
/// (<see cref="VHSRetroFeature"/>, <see cref="PSXPostProcessing"/>) read it every frame, so a change in the
/// pause menu shows at once.
/// </summary>
public static class PictureSettings
{
    public const string PrefKey = "PictureMode";
    public const PictureMode Default = PictureMode.Tape;

    /// <summary>Raised after the mode changed.</summary>
    public static event Action Changed;

    private static bool loaded;
    private static PictureMode mode;

    public static PictureMode Mode
    {
        get
        {
            if (!loaded)
            {
                mode = (PictureMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)Default), 0, 2);
                loaded = true;
            }
            return mode;
        }
        set
        {
            if (loaded && mode == value) return;
            mode = value;
            loaded = true;
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>True for Tape and Light: the tape picture replaces the pixel grid.</summary>
    public static bool IsTape => Mode != PictureMode.Classic;
}
