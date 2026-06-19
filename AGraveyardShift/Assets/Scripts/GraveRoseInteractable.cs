using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A graveyard grave (GraveyardMap/DirtGrave_*) the player can place the rose on, once they
/// have it (GameManager "rosepickedup"). The grave is only interactable while the player
/// holds the rose (its layer is toggled to the Interactable layer). Interacting shows a
/// Yes/No confirm ("Place the rose on the gravesite?"). Yes consumes the rose and a rose
/// sinks into the ground, then:
///  - normal grave -> after a beat, "The rose immediately wilted away..." and nothing else.
///  - the Father grave (isFatherGrave) -> the player sinks, the screen fades to black, and
///    they are teleported to the forest (forestSpawn).
/// Only the graveyard graves get this; the crypt dig piles (DirtPileInteractable) are untouched.
/// </summary>
public class GraveRoseInteractable : MonoBehaviour, IInteractable
{
    [Header("Gating")]
    [Tooltip("GameManager flag the player must have (the rose) for this grave to be interactable.")]
    [SerializeField] private string requiredRoseFlag = "rosepickedup";
    [Tooltip("Layer used while interactable (must be in the InteractionSystem's interactableMask).")]
    [SerializeField] private int interactableLayer = 6;
    [Tooltip("Layer used while NOT interactable (before the rose / after it has been used).")]
    [SerializeField] private int dormantLayer = 0;
    [SerializeField] private string interactionPrompt = "Place rose";

    [Header("Confirm")]
    [TextArea] [SerializeField] private string placePrompt = "Place the rose on the gravesite?";
    [SerializeField] private string yesText = "Yes";
    [SerializeField] private string noText = "No";
    [SerializeField] private string speakerName = "";
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float typewriterSpeed = 40f;

    [Header("Rose visual")]
    [Tooltip("Rose model spawned on the grave and sunk into the ground.")]
    [SerializeField] private GameObject rosePrefab;
    [SerializeField] private float roseScale = 1.5f;
    [Tooltip("Local euler rotation for the placed rose (so it lies on the gravesite the way you want).")]
    [SerializeField] private Vector3 roseEuler = Vector3.zero;
    [Tooltip("Tiny gap between the rose's BASE and the grave surface (it rests on the surface).")]
    [SerializeField] private float roseAboveGrave = 0.02f;
    [SerializeField] private float roseSinkDepth = 0.5f;
    [SerializeField] private float roseSinkTime = 1.4f;

    [Header("Wilt (normal / wrong graves)")]
    [Tooltip("How long the rose sits on the gravesite before it sinks (wrong gravesites).")]
    [SerializeField] private float wrongRosePause = 1.2f;
    [TextArea] [SerializeField] private string wiltText = "The rose immediately wilted away...";
    [Tooltip("Pause between the rose finishing its sink and the wilt line.")]
    [SerializeField] private float wiltDelay = 0.8f;
    [SerializeField] private float wiltDuration = 3.5f;

    [Header("Father grave")]
    [SerializeField] private bool isFatherGrave = false;
    [Tooltip("Where the player is teleported after the cut to black (a spot in the forest).")]
    [SerializeField] private Transform forestSpawn;
    [Tooltip("Beat the player spends looking at the placed rose before the drop.")]
    [SerializeField] private float lookTime = 1.5f;
    [Tooltip("Sudden drop into the ground -- kept short for a jumpscare-y fall, not a slow sink.")]
    [SerializeField] private float playerSinkDepth = 3f;
    [SerializeField] private float playerSinkTime = 0.45f;
    [Tooltip("Heavy-impact sound played the instant the player drops.")]
    [SerializeField] private AudioClip fallSound;
    [Range(0f, 1f)] [SerializeField] private float fallVolume = 1f;
    [Tooltip("How long the screen stays pitch black before arriving in the forest.")]
    [SerializeField] private float blackHold = 0.7f;
    [Tooltip("Fade-in time once in the forest.")]
    [SerializeField] private float fadeTime = 1.0f;
    [SerializeField] private Color fadeColor = Color.black;
    [Tooltip("Internal-thought line shown a moment after the player spawns in the forest.")]
    [TextArea] [SerializeField] private string arrivalLine = "Where am I?";
    [Tooltip("Delay after the fade-in before the arrival line.")]
    [SerializeField] private float arrivalLineDelay = 1.5f;
    [SerializeField] private float arrivalLineDuration = 3f;

    // runtime
    private DialogueUI dialogueUI;
    private Movement playerMovement;
    private CrosshairManager crosshairManager;
    private bool isShowingChoice;
    private bool placed;
    private Action<int> activeChoiceHandler;

    private static Image fadeOverlay;
    private static Canvas fadeCanvas;

    private void Start()
    {
        dialogueUI = FindObjectOfType<DialogueUI>();
        playerMovement = FindObjectOfType<Movement>();
        crosshairManager = FindObjectOfType<CrosshairManager>();
    }

    private void Update()
    {
        // Interactable only while the player has the rose (and this grave hasn't been used).
        bool hasRose = !placed && GameManager.Instance != null && GameManager.Instance.GetBoolFlag(requiredRoseFlag);
        int desired = hasRose ? interactableLayer : dormantLayer;
        if (gameObject.layer != desired) gameObject.layer = desired;
    }

    private void OnDestroy()
    {
        if (activeChoiceHandler != null && dialogueUI != null)
            dialogueUI.OnChoiceSelected -= activeChoiceHandler;
    }

    // IInteractable
    public void Interact()
    {
        if (isShowingChoice || placed || IsBusy()) return;
        if (GameManager.Instance == null || !GameManager.Instance.GetBoolFlag(requiredRoseFlag)) return;

        StartCoroutine(ShowConfirm(placePrompt, yes => { if (yes) PlaceRose(); }));
    }

    public string GetInteractionPrompt() => interactionPrompt;

    private void PlaceRose()
    {
        if (placed) return;
        placed = true;

        // The rose is consumed -> every grave goes dormant (and re-interaction is blocked).
        if (GameManager.Instance != null) GameManager.Instance.SetBoolFlag(requiredRoseFlag, false);
        gameObject.layer = dormantLayer;

        GameObject rose = SpawnRose();

        if (isFatherGrave)
        {
            // The rose stays put so the player looks at it for a beat, then the player drops.
            StartCoroutine(FatherSequence(rose));
        }
        else
        {
            StartCoroutine(WrongGraveSequence(rose));
        }
    }

    private GameObject SpawnRose()
    {
        if (rosePrefab == null) return null;

        // Find the grave surface at its centre (raycast onto this grave's own mound collider).
        var col = GetComponent<Collider>();
        float surfaceY = transform.position.y;
        if (col != null)
        {
            surfaceY = col.bounds.max.y;
            RaycastHit hit;
            Vector3 origin = new Vector3(transform.position.x, col.bounds.max.y + 1f, transform.position.z);
            if (col.Raycast(new Ray(origin, Vector3.down), out hit, 5f)) surfaceY = hit.point.y;
        }

        var rose = Instantiate(rosePrefab);
        rose.name = "PlacedRose";
        rose.transform.rotation = Quaternion.Euler(roseEuler);
        rose.transform.localScale = Vector3.one * roseScale;
        rose.transform.position = new Vector3(transform.position.x, surfaceY + 1f, transform.position.z);
        foreach (var c in rose.GetComponentsInChildren<Collider>()) c.enabled = false; // purely visual

        // Rest the rose's BASE on the surface so it sits on the gravesite properly.
        var rends = rose.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float lift = (surfaceY + roseAboveGrave) - b.min.y;
            rose.transform.position += new Vector3(0f, lift, 0f);
        }
        return rose;
    }

    private IEnumerator SinkObject(Transform tr, float depth, float time, bool destroyAtEnd)
    {
        Vector3 start = tr.position;
        Vector3 end = start - Vector3.up * depth;
        float t = 0f, dur = Mathf.Max(0.01f, time);
        while (t < 1f && tr != null)
        {
            t += Time.deltaTime / dur;
            tr.position = Vector3.Lerp(start, end, t);
            yield return null;
        }
        if (tr != null && destroyAtEnd) Destroy(tr.gameObject);
    }

    private IEnumerator WrongGraveSequence(GameObject rose)
    {
        // The rose sits on the gravesite for a moment...
        yield return new WaitForSeconds(wrongRosePause);
        // ...then sinks into the ground and disappears.
        if (rose != null) yield return SinkObject(rose.transform, roseSinkDepth, roseSinkTime, true);
        yield return new WaitForSeconds(wiltDelay);
        SimpleDialogueTrigger.ShowDialogue(wiltText, speakerName, wiltDuration, useTypewriter ? typewriterSpeed : 0f);
    }

    private IEnumerator FatherSequence(GameObject placedRose)
    {
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        Transform player = playerGo != null ? playerGo.transform : null;
        var cc = playerGo != null ? playerGo.GetComponent<CharacterController>() : null;

        if (playerMovement != null) playerMovement.DisableAllInput();
        if (crosshairManager != null) crosshairManager.Hide();
        EnsureFadeOverlay();

        // 1. The player looks at the placed rose for a beat (screen still clear).
        yield return new WaitForSecondsRealtime(lookTime);

        // 2. Sudden drop -- heavy impact the instant it starts, fast accelerating fall.
        PlayFallSound();
        Vector3 fallStart = player != null ? player.position : Vector3.zero;
        Vector3 fallEnd = fallStart - Vector3.up * playerSinkDepth;
        if (cc != null) cc.enabled = false;
        float t = 0f, dur = Mathf.Max(0.01f, playerSinkTime);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float s = Mathf.Clamp01(t); s *= s; // ease-in => accelerating fall
            if (player != null) player.position = Vector3.Lerp(fallStart, fallEnd, s);
            yield return null;
        }

        // 3. Cut to pitch black immediately (no fade).
        SetFade(1f);
        if (placedRose != null) Destroy(placedRose);
        yield return new WaitForSecondsRealtime(blackHold);

        // 4. Arrive in the forest.
        if (player != null && forestSpawn != null)
        {
            player.position = forestSpawn.position;
            player.rotation = forestSpawn.rotation;
        }
        if (cc != null) cc.enabled = true;
        PlayerZoneTracker.RefreshZonesAfterTeleport();

        yield return new WaitForSecondsRealtime(0.1f);

        // 5. Fade back in at the forest.
        t = 0f; dur = Mathf.Max(0.01f, fadeTime);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            SetFade(1f - Mathf.Clamp01(t));
            yield return null;
        }
        SetFade(0f);

        if (playerMovement != null) playerMovement.EnableAllInput();
        if (crosshairManager != null) crosshairManager.Show();

        // A moment after arriving, the player's internal thought.
        if (!string.IsNullOrEmpty(arrivalLine))
        {
            yield return new WaitForSecondsRealtime(arrivalLineDelay);
            SimpleDialogueTrigger.ShowDialogue(arrivalLine, speakerName, arrivalLineDuration, useTypewriter ? typewriterSpeed : 0f);
        }
    }

    private void PlayFallSound()
    {
        if (fallSound == null) return;
        var go = new GameObject("FallSound2D");
        var src = go.AddComponent<AudioSource>();
        src.clip = fallSound;
        src.volume = fallVolume;
        src.spatialBlend = 0f; // 2D jumpscare hit, full volume
        src.Play();
        Destroy(go, fallSound.length + 0.1f);
    }

    // ---- fullscreen fade (mirrors TeleportInteractable) ----
    private void EnsureFadeOverlay()
    {
        if (fadeCanvas == null)
        {
            var canvasObj = new GameObject("GraveFadeCanvas");
            fadeCanvas = canvasObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();

            var imgObj = new GameObject("FadeOverlay");
            imgObj.transform.SetParent(canvasObj.transform, false);
            fadeOverlay = imgObj.AddComponent<Image>();
            var rt = fadeOverlay.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            DontDestroyOnLoad(canvasObj);
        }
        SetFade(0f);
    }

    private void SetFade(float a)
    {
        if (fadeOverlay != null) fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, a);
    }

    private bool IsBusy()
    {
        if (SimpleDialogueTrigger.IsAnySimpleDialogueActive) return true;
        DialogueManager dm = DialogueManager.GetInstance();
        return dm != null && dm.IsDialoguePlaying();
    }

    /// <summary>Single line then Yes/No choices (mirrors SecretKeyInteractable / ChestInteractable).</summary>
    private IEnumerator ShowConfirm(string message, Action<bool> onResult)
    {
        if (dialogueUI == null)
        {
            onResult?.Invoke(false);
            yield break;
        }

        isShowingChoice = true;

        if (playerMovement != null) playerMovement.DisableAllInput();
        if (crosshairManager != null) crosshairManager.Hide();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        dialogueUI.Show();
        if (useTypewriter) dialogueUI.SetTypewriterSpeedOverride(typewriterSpeed);
        dialogueUI.SetTypewriterSoundMuted(true);
        dialogueUI.SetDialogueText(message, string.IsNullOrEmpty(speakerName) ? null : speakerName);

        while (dialogueUI.IsTypewriting) yield return null;
        yield return null;

        dialogueUI.DisplayChoices(new List<string> { yesText, noText });

        bool choiceMade = false;
        int selected = -1;
        activeChoiceHandler = index =>
        {
            selected = index;
            choiceMade = true;
            if (dialogueUI != null && activeChoiceHandler != null)
                dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            activeChoiceHandler = null;
        };
        dialogueUI.OnChoiceSelected += activeChoiceHandler;

        while (!choiceMade && dialogueUI != null && dialogueUI.IsVisible)
            yield return null;

        if (activeChoiceHandler != null && dialogueUI != null)
        {
            dialogueUI.OnChoiceSelected -= activeChoiceHandler;
            activeChoiceHandler = null;
        }
        if (dialogueUI != null)
        {
            dialogueUI.ClearTypewriterSpeedOverride();
            dialogueUI.SetTypewriterSoundMuted(false);
        }

        bool stillOurs = dialogueUI != null && dialogueUI.IsVisible;
        if (stillOurs)
        {
            dialogueUI.ClearChoices();
            dialogueUI.Hide();
            if (playerMovement != null) playerMovement.EnableAllInput();
            if (crosshairManager != null) crosshairManager.Show();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        isShowingChoice = false;
        onResult?.Invoke(choiceMade && selected == 0);
    }
}
