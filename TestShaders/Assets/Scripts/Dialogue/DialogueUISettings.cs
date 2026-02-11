using UnityEngine;
using TMPro;

/// <summary>
/// ScriptableObject that stores dialogue UI styling settings.
/// Create instances via Assets > Create > Dialogue > UI Settings.
/// These settings can be applied to DialogueUI components to customize appearance.
/// </summary>
[CreateAssetMenu(fileName = "DialogueUISettings", menuName = "Dialogue/UI Settings")]
public class DialogueUISettings : ScriptableObject
{
    [Header("Dialogue Text Style")]
    [Tooltip("Font asset for the main dialogue text")]
    public TMP_FontAsset dialogueFont;

    [Tooltip("Font size for dialogue text")]
    public float dialogueFontSize = 24f;

    [Tooltip("Color of the dialogue text")]
    public Color dialogueTextColor = Color.white;

    [Tooltip("Font style for dialogue text (Normal, Bold, Italic, etc.)")]
    public FontStyles dialogueFontStyle = FontStyles.Normal;

    [Header("Choice Button Text Style")]
    [Tooltip("Font asset for choice button text")]
    public TMP_FontAsset choiceButtonFont;

    [Tooltip("Font size for choice button text")]
    public float choiceButtonFontSize = 18f;

    [Tooltip("Color of choice button text")]
    public Color choiceButtonTextColor = Color.white;

    [Tooltip("Font style for choice buttons")]
    public FontStyles choiceButtonFontStyle = FontStyles.Normal;

    [Header("Choice Button Colors")]
    [Tooltip("Normal state color of choice buttons")]
    public Color buttonNormalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

    [Tooltip("Highlighted/hover state color")]
    public Color buttonHighlightedColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);

    [Tooltip("Pressed state color")]
    public Color buttonPressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    [Tooltip("Disabled state color")]
    public Color buttonDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Animation Settings")]
    [Tooltip("Enable typewriter effect for dialogue text")]
    public bool useTypewriterEffect = false;

    [Tooltip("Characters per second for typewriter effect")]
    public float typewriterSpeed = 50f;

    [Tooltip("Fade in duration for dialogue panel (0 = instant)")]
    public float panelFadeInDuration = 0.2f;

    [Tooltip("Fade out duration for dialogue panel")]
    public float panelFadeOutDuration = 0.15f;

    [Header("Speaker Name (Optional)")]
    [Tooltip("Show speaker name above dialogue")]
    public bool showSpeakerName = false;

    [Tooltip("Font for speaker name")]
    public TMP_FontAsset speakerNameFont;

    [Tooltip("Font size for speaker name")]
    public float speakerNameFontSize = 20f;

    [Tooltip("Color for speaker name")]
    public Color speakerNameColor = Color.yellow;
}
