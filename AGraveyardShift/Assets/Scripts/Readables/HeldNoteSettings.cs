using TMPro;
using UnityEngine;

/// <summary>
/// Tuning and assets for reading notes by holding them (see <see cref="HeldNote"/>).
/// Lives at Resources/Readables/HeldNoteSettings, so its textures, font, material and sounds are always in the build.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Held Note Settings", fileName = "HeldNoteSettings")]
public class HeldNoteSettings : ScriptableObject
{
    public const string ResourcePath = "Readables/HeldNoteSettings";

    [Header("Which notes")]
    [Tooltip("The paper photo ordinary notes use. A note with any other background keeps its own art when held (the bloody page)")]
    public Sprite standardBackground;
    [Tooltip("Notes under an object with one of these names are crypt notes (torn ledger scraps in pencil)")]
    public string[] cryptRoots = { "MapFBXFile" };

    [Header("Paper and writing")]
    public Texture2D cabinPaper;
    [Tooltip("A torn, ruled ledger page; its alpha is the torn edge")]
    public Texture2D cryptPaper;
    [Tooltip("Pencil handwriting for the crypt notes")]
    public TMP_FontAsset cryptFont;
    public Color inkColor = new Color(0.035f, 0.028f, 0.026f);
    public Color pencilColor = new Color(0.20f, 0.20f, 0.22f);
    [Tooltip("Cabin notes: the note's own text size times this, on a 375 x 540 page")]
    public float textScale = 1.28f;
    [Tooltip("Crypt notes: the note's own text size times this, on a 300 x 381 scrap")]
    public float cryptTextScale = 1.67f;

    [Header("The sheet")]
    [Tooltip("Standard, cutout, with emission (the page catches a little of the room's light outside the torch beam)")]
    public Material sheetMaterial;
    public Color cabinGlow = new Color(0.34f, 0.25f, 0.16f);
    public Color cryptGlow = new Color(0.22f, 0.20f, 0.18f);
    public Vector2 cabinSheet = new Vector2(0.43f, 0.62f);
    public float cabinHold = 0.70f;
    public Vector2 cryptSheet = new Vector2(0.30f, 0.381f);
    public float cryptHold = 0.48f;
    [Tooltip("Layer the held sheet is drawn on, by its own camera after the picture's tape effects (keep it unused elsewhere)")]
    public int pageLayer = 30;

    [Header("Motion")]
    public float liftSeconds = 0.7f;
    public float turnSeconds = 0.55f;
    public float putBackSeconds = 0.6f;
    [Tooltip("The head comes up to at most this many degrees below level while holding a note")]
    public float maxLookDown = 25f;
    [Tooltip("Edge darkening while holding a note (0 = none)")]
    [Range(0f, 1f)] public float vignette = 0.7f;

    [Header("Torch")]
    [Tooltip("Where the torch hand goes while reading (right, up, forward from the eyes, metres)")]
    public Vector3 readingHand = new Vector3(0.06f, -0.10f, 0.05f);
    [Tooltip("Lets the beam aim at the page instead of 3 m beyond it")]
    public float readingAimMin = 0.25f;

    [Header("Sounds")]
    public AudioClip pickUpSound;
    public AudioClip turnSound;
    public AudioClip putBackSound;
    [Range(0f, 1f)] public float soundVolume = 0.7f;

    public static HeldNoteSettings Load() { return Resources.Load<HeldNoteSettings>(ResourcePath); }
}
