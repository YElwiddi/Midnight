using TMPro;
using UnityEngine;

/// <summary>
/// Loads the EndingFontConfig from Resources and returns the right font per ending.
/// Returns null if no config is present (the reveal then keeps its default font).
/// </summary>
public static class EndingFonts
{
    private static EndingFontConfig config;
    private static bool loaded;

    public static TMP_FontAsset Get(Ending e)
    {
        if (!loaded)
        {
            config = Resources.Load<EndingFontConfig>("EndingFontConfig");
            loaded = true;
        }
        return config != null ? config.GetFont(e) : null;
    }
}
