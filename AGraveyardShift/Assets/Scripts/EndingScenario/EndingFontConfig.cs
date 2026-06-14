using TMPro;
using UnityEngine;

/// <summary>
/// Per-ending fonts for the reveal screen. Lives in a Resources folder so it can be
/// loaded from any scene (death endings happen in the gameplay scenes). Edit the font
/// slots in the inspector.
/// </summary>
[CreateAssetMenu(fileName = "EndingFontConfig", menuName = "Game/Ending Font Config")]
public class EndingFontConfig : ScriptableObject
{
    [Tooltip("Font for the Bad endings (I Wrathful Spirit, II Ambush).")]
    public TMP_FontAsset badEndingFont;

    [Tooltip("Font for the Good ending (III Wrongful Conviction).")]
    public TMP_FontAsset goodEndingFont;

    [Tooltip("Font for the 'Good?' ending (IV Happily Ever After).")]
    public TMP_FontAsset goodQuestionFont;

    public TMP_FontAsset GetFont(Ending e)
    {
        switch (e)
        {
            case Ending.WrathfulSpirit:
            case Ending.Ambush:
                return badEndingFont;
            case Ending.WrongfulConviction:
                return goodEndingFont;
            case Ending.HappilyEverAfter:
                return goodQuestionFont;
            default:
                return null;
        }
    }
}
