using UnityEngine;

/// <summary>
/// The player's brightness setting, applied as a shift of the gameplay environment's ambient
/// SKY colour.
///
/// The options slider runs 0..1 (default 0.5). It produces a signed sky offset of ±32 (in
/// 0-255 space) — -32 at the bottom, 0 at the centre, +32 at the top — which is added to each
/// area's authored "default" sky. So every area keeps its default at the slider centre and
/// swings the same amount either way:
///   * Outdoor  default 32  ->  0 .. 32 .. 64
///   * Crypt    default 48  ->  16 .. 48 .. 80
///   * Cabin    default 38  ->  6 .. 38 .. 70
/// Only the sky channel is shifted; equator and ground keep their per-scene values.
///
/// OptionsPanel saves the slider value here; LightingController reads it back and writes
/// RenderSettings.ambientSkyColor when the gameplay scene initialises and on every preset
/// change, so the menu's own lighting is never touched.
/// </summary>
public static class BrightnessSettings
{
    public const string PrefKey = "Brightness";
    public const float Default = 0.5f;

    // Full slider travel as a 0..1 sky delta: ±32/255 around the centre (64/255 total).
    private const float Swing = 64f / 255f;
    // The outdoor / default sky at the slider centre: 32/255.
    private const float OutdoorCentre = 32f / 255f;

    public static float Value => Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKey, Default));

    public static void Save(float value) => PlayerPrefs.SetFloat(PrefKey, Mathf.Clamp01(value));

    /// <summary>Signed sky delta for the current setting: -32/255 at slider 0, 0 at centre, +32/255 at max.</summary>
    public static float Offset => (Value - 0.5f) * Swing;

    /// <summary>The outdoor / default ambient sky for the current setting (0 .. 32 .. 64 in 0-255).</summary>
    public static Color SkyColor => Shift(new Color(OutdoorCentre, OutdoorCentre, OutdoorCentre, 1f));

    /// <summary>Shifts a preset's base sky by the current brightness offset, keeping its default at the centre.</summary>
    public static Color Shift(Color baseSky)
    {
        float o = Offset;
        return new Color(
            Mathf.Clamp01(baseSky.r + o),
            Mathf.Clamp01(baseSky.g + o),
            Mathf.Clamp01(baseSky.b + o),
            baseSky.a);
    }
}
