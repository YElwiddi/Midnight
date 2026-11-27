using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Tuning and assets for reading headstones by kneeling in front of them (see <see cref="TombstoneKneel"/>).
/// Lives at Resources/Tombstones/TombstoneSettings, so the game finds it without any scene reference,
/// and so its fonts and materials (and their shader variants) are always in the build.
/// </summary>
[CreateAssetMenu(menuName = "Graveyard/Tombstone Settings", fileName = "TombstoneSettings")]
public class TombstoneSettings : ScriptableObject
{
    public const string ResourcePath = "Tombstones/TombstoneSettings";

    [Serializable]
    public class Face
    {
        [Tooltip("Mesh of the headstones this face belongs to (MeshFilter.sharedMesh.name)")]
        public string mesh;
        [Tooltip("Centre of the lettering panel, in the stone's local space")]
        public Vector3 center;
        [Tooltip("Out of the face, in the stone's local space")]
        public Vector3 normal = Vector3.back;
        [Tooltip("Up along the face, in the stone's local space")]
        public Vector3 up = Vector3.up;
        [Tooltip("Panel width and height in the stone's local units (scaled with the stone)")]
        public Vector2 size = new Vector2(0.0073f, 0.0066f);
        [Tooltip("Lettering size on this kind of stone, relative to the caps below")]
        public float textScale = 1f;
    }

    [Header("Faces that carry lettering")]
    public List<Face> faces = new List<Face>();

    [Header("Lettering")]
    [Tooltip("The name and the dates (Cinzel Bold)")]
    public TMP_FontAsset nameFont;
    [Tooltip("The opening words and the epitaph (Cinzel)")]
    public TMP_FontAsset textFont;
    [Tooltip("The carved look (TextMeshPro/Distance Field (Surface), inner bevel). Each font gets a copy with its own atlas")]
    public Material carvedTemplate;
    [Tooltip("Standard material in Fade mode, used for the dressed panel behind the lettering and the rule")]
    public Material panelMaterial;
    public Color grooveColor = new Color(0.16f, 0.155f, 0.145f, 1f);
    [Tooltip("Tint and opacity of the smoother dressed panel the lettering is cut into")]
    public Color panelColor = new Color(1f, 1f, 1f, 0.42f);
    [Tooltip("Cap heights as fractions of the panel height")]
    public float prefixCap = 0.051f, nameCap = 0.093f, textCap = 0.055f, datesCap = 0.058f;
    [Tooltip("Widest a line may be, as a fraction of the panel width")]
    public float maxLineWidth = 0.82f;

    [Header("Kneeling")]
    public float kneelSeconds = 0.95f;
    public float standSeconds = 0.85f;
    [Tooltip("Camera distance from the face, as a multiple of the panel height")]
    public float distance = 1.65f;
    [Tooltip("Camera height above the panel centre, as a multiple of the panel height")]
    public float height = 0.79f;
    [Tooltip("Lowest the eyes go above the ground when kneeling (metres)")]
    public float minEyeHeight = 0.95f;
    [Tooltip("Steepest the view looks down at the panel (degrees); low stones are read from further back instead")]
    public float maxPitch = 42f;
    [Tooltip("How much of the screen height the panel fills; sets the field of view, which never widens")]
    public float framing = 0.58f;
    public float minFov = 38f;
    [Tooltip("Where the torch hand goes while kneeling (right, up, forward from the eyes, metres)")]
    public Vector3 kneelHand = new Vector3(0.06f, -0.10f, 0.05f);
    [Tooltip("Lets the beam aim at the stone instead of 3 m beyond it while kneeling")]
    public float kneelAimMin = 0.25f;
    [Tooltip("Seconds after standing up before a stone can be read again")]
    public float rereadDelay = 0.25f;
    [Tooltip("Cloth and a knee on the ground")]
    public AudioClip kneelSound;
    [Tooltip("Cloth as you get up")]
    public AudioClip standSound;
    [Range(0f, 1f)] public float soundVolume = 0.7f;

    [Header("Low sanity")]
    [Tooltip("While a stone's low-sanity condition holds, its own lettering is replaced by its low-sanity words. Seconds the change takes when the condition starts or stops")]
    public float turnSeconds = 0.6f;
    [Tooltip("Shake amplitude per unit of the readable's shake intensity, as a fraction of the letter height")]
    public float shakeScale = 0.022f;
    [Tooltip("Cap height of the low-sanity words, as a fraction of the panel height")]
    public float lowSanityCap = 0.18f;

    public static TombstoneSettings Load() { return Resources.Load<TombstoneSettings>(ResourcePath); }
}
