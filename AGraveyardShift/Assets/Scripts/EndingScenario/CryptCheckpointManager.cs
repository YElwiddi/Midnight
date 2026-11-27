using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages checkpoint respawning for the crypt area.
/// When the player dies to a crypt killer, they respawn outside the crypt
/// with all crypt objects (shovel, dirt piles, killer) reset to their initial state.
/// While the screen is black, two message cards tell the player what happened
/// ("The Crypt Fiend has claimed your soul." / "You have been returned to the surface...").
/// After a death the crypt also eases up: a hint note appears, the shovel moves next to it
/// (if the player had found it) and the killer is slower and sees less far. After a second
/// death, red arrows on the floor lead toward the general areas of the correct graves.
/// </summary>
public class CryptCheckpointManager : MonoBehaviour
{
    public static CryptCheckpointManager Instance { get; private set; }

    [Header("Checkpoint Settings")]
    [Tooltip("Transform marking where the player respawns (outside crypt entrance)")]
    public Transform checkpointSpawnPoint;

    [Tooltip("Player rotation (Euler angles) when respawning. If zero, uses spawn point's rotation.")]
    public Vector3 checkpointRotation = Vector3.zero;

    [Tooltip("If true, uses checkpointRotation instead of spawn point's rotation")]
    public bool useCustomRotation = false;

    [Tooltip("Time to fade out before respawn")]
    public float fadeOutDuration = 1f;

    [Tooltip("Time to fade in after respawn")]
    public float fadeInDuration = 1f;

    [Tooltip("Delay at black screen before fading in")]
    public float blackScreenDelay = 0.5f;

    [Header("Fade Overlay")]
    [Tooltip("Full-screen black overlay for fade effect (should be a UI Image)")]
    public CanvasGroup fadeOverlay;

    [Header("Death Message")]
    [Tooltip("First card on the black screen after a crypt fiend catches the player. Written in English; other languages come from the localization tables, so a changed line needs translating again. Empty = no card.")]
    [TextArea(1, 3)]
    public string claimedMessage = "The Crypt Fiend has claimed your soul.";

    [Tooltip("Second card, on a fresh black screen after the first one fades away (the player is moved to the surface in between). Empty = no card.")]
    [TextArea(2, 4)]
    public string returnedMessage = "You have been returned to the surface.\nEnter the crypt and try again.";

    [Tooltip("Font of both cards. Empty = Caslon Antique like the intro pages (the good ending's font in Resources/EndingFontConfig). Other languages swap it through the localization font map.")]
    public TMP_FontAsset messageFont;

    public float messageFontSize = 48f;

    public Color messageColor = new Color(0.93f, 0.91f, 0.87f, 1f);

    [Tooltip("Silent black beat before each card (seconds)")]
    public float messagePause = 0.6f;

    [Tooltip("Seconds each card takes to fade in, and again to fade out")]
    public float messageFadeDuration = 0.8f;

    [Tooltip("Seconds the first card stays fully visible")]
    public float claimedMessageHold = 2.2f;

    [Tooltip("Seconds the second card stays fully visible")]
    public float returnedMessageHold = 3.2f;

    [Header("Shovel Respawn")]
    [Tooltip("Prefab for the shovel pickup")]
    public GameObject shovelPrefab;

    [Tooltip("Transform marking where the shovel spawns")]
    public Transform shovelSpawnPoint;

    [Header("Dirt Pile Respawn")]
    [Tooltip("Prefab for dirt pile interactables")]
    public GameObject dirtPilePrefab;

    [Tooltip("Transforms marking where each dirt pile spawns. Configure DirtPileType in the spawned instances via DirtPileSpawnData.")]
    public DirtPileSpawnData[] dirtPileSpawnPoints;

    [Header("Killer Respawn")]
    [Tooltip("If true, uses EndingScenarioManager to respawn the correct killer based on player stats. If false, requires manual killer prefab setup.")]
    public bool useEndingScenarioManager = true;

    [Tooltip("Only used if useEndingScenarioManager is false")]
    public CryptKillerConfig killerConfig;

    [Tooltip("Only used if useEndingScenarioManager is false")]
    public Transform killerSpawnPoint;

    [Tooltip("Only used if useEndingScenarioManager is false")]
    public GameObject killerPrefab;

    [Tooltip("Only used if useEndingScenarioManager is false")]
    public Transform[] killerRoamWaypoints;

    [Header("Audio")]
    [Tooltip("Sound to play on respawn")]
    public AudioClip respawnSound;
    [Range(0f, 1f)]
    public float respawnSoundVolume = 0.5f;

    [Header("Help After A Death")]
    [Tooltip("Hint note (inactive in the scene) that appears after the player's first death to the crypt killer and stays for the rest of the run")]
    public GameObject hintNote;

    [Tooltip("Where the shovel respawns after a death if the player had already found it (next to the hint note). Players who never found it get it at Shovel Spawn Point as usual.")]
    public Transform hintShovelSpawnPoint;

    [Tooltip("Killer speed multiplier after a death (0.85 = 15% slower). Dig penalties still stack on top.")]
    [Range(0.5f, 1f)]
    public float helpKillerSpeedMultiplier = 0.85f;

    [Tooltip("Killer sight range multiplier after a death (0.75 = sees 25% less far)")]
    [Range(0.3f, 1f)]
    public float helpKillerVisionMultiplier = 0.75f;

    [Tooltip("After a death, the Persistent Stalker only senses a lit flashlight within this distance (normally it senses it from anywhere). 0 = anywhere.")]
    public float helpStalkerFlashlightRange = 20f;

    [Tooltip("Red floor arrows (inactive in the scene) leading from the arrival nook toward the general areas of the correct graves. They appear once the player has died this many times and stay for the rest of the run.")]
    public GameObject hintArrows;

    [Tooltip("Deaths to the crypt killer before the hint arrows appear")]
    [Min(1)]
    public int arrowsAfterDeaths = 2;

    // Tracking spawned objects for cleanup
    private GameObject currentShovel;
    private List<GameObject> currentDirtPiles = new List<GameObject>();
    private GameObject currentKiller;
    private AudioSource audioSource;

    // Death message cards (built on first use, on their own canvas above the HUD)
    private GameObject messageRoot;
    private TextMeshProUGUI messageText;

    // State
    private bool isRespawning = false;
    private int deathCount = 0;
    private bool shovelFoundBefore = false;

    // Captured VHS settings (captured on Start for restoration after respawn)
    private bool vhsSettingsCaptured = false;
    private float capturedGlitchIntensity;
    private float capturedRGBShift;
    private float capturedNoiseIntensity;
    private float capturedScanlineIntensity;
    private float capturedTrackingNoise;

    // Captured lighting settings (captured before entering crypt for restoration after respawn)
    private bool lightingSettingsCaptured = false;
    private Color capturedSkyColor;
    private Color capturedEquatorColor;
    private Color capturedGroundColor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        // Ensure fade overlay starts invisible
        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.gameObject.SetActive(true);
        }

        // Capture current VHS settings before any jumpscare modifies them
        CaptureVHSSettings();

        // Capture current lighting settings before player enters crypt
        CaptureLightingSettings();

        // Track initially placed objects in the scene
        TrackExistingCryptObjects();

        // The killer spawns later (once the player leaves the crypt's arrival nook), so pick it up then
        if (EndingScenarioManager.Instance != null)
        {
            EndingScenarioManager.Instance.OnKillerSpawned.AddListener(HandleKillerSpawned);
        }
    }

    private void OnDestroy()
    {
        if (EndingScenarioManager.Instance != null)
        {
            EndingScenarioManager.Instance.OnKillerSpawned.RemoveListener(HandleKillerSpawned);
        }
    }

    /// <summary>
    /// A crypt killer just spawned (EndingScenarioManager releases it once the player leaves the
    /// arrival nook): track it for cleanup and ease it if the player has died before.
    /// </summary>
    private void HandleKillerSpawned(GameObject killer)
    {
        currentKiller = killer;
        ApplyKillerHelp(killer);
    }

    /// <summary>
    /// Captures the current VHS effect settings to restore after respawn.
    /// </summary>
    private void CaptureVHSSettings()
    {
        Camera playerCamera = Camera.main;
        if (playerCamera == null) return;

        VHSRetroFeature vhsEffect = playerCamera.GetComponent<VHSRetroFeature>();
        if (vhsEffect != null)
        {
            capturedGlitchIntensity = vhsEffect.glitchIntensity;
            capturedRGBShift = vhsEffect.rgbShiftAmount;
            capturedNoiseIntensity = vhsEffect.noiseIntensity;
            capturedScanlineIntensity = vhsEffect.scanlineIntensity;
            capturedTrackingNoise = vhsEffect.trackingNoise;
            vhsSettingsCaptured = true;
            Debug.Log("CryptCheckpointManager: Captured VHS settings for respawn restoration");
        }
    }

    /// <summary>
    /// Captures the current lighting settings to restore after respawn.
    /// Call this before the player enters the crypt to save the pre-crypt lighting state.
    /// </summary>
    public void CaptureLightingSettings()
    {
        capturedSkyColor = RenderSettings.ambientSkyColor;
        capturedEquatorColor = RenderSettings.ambientEquatorColor;
        capturedGroundColor = RenderSettings.ambientGroundColor;
        lightingSettingsCaptured = true;
        Debug.Log("CryptCheckpointManager: Captured lighting settings for respawn restoration");
    }

    /// <summary>
    /// Finds and tracks any crypt objects already in the scene.
    /// </summary>
    private void TrackExistingCryptObjects()
    {
        // Find existing shovel
        ShovelPickup existingShovel = FindObjectOfType<ShovelPickup>();
        if (existingShovel != null)
        {
            currentShovel = existingShovel.gameObject;
        }

        // Find existing dirt piles
        DirtPileInteractable[] existingPiles = FindObjectsOfType<DirtPileInteractable>();
        foreach (var pile in existingPiles)
        {
            currentDirtPiles.Add(pile.gameObject);
        }

        // Find existing killer
        CryptKiller existingKiller = FindObjectOfType<CryptKiller>();
        if (existingKiller != null)
        {
            currentKiller = existingKiller.gameObject;
        }
    }

    /// <summary>
    /// Call this to respawn the player at the checkpoint and reset the crypt.
    /// </summary>
    public void RespawnAtCheckpoint()
    {
        if (isRespawning) return;
        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        isRespawning = true;
        Debug.Log("CryptCheckpointManager: Starting respawn sequence");

        // Remember this death, and whether the shovel was found, before the crypt stats are reset
        deathCount++;
        if (GameManager.Instance != null && GameManager.Instance.ShovelPickedUp)
        {
            shovelFoundBefore = true;
        }

        // Hold the player until the screen comes back. This also keeps the pause menu shut
        // (PauseMenu.CanPause checks these flags): resuming would hand control back mid-sequence.
        LockPlayerInput();

        // Fade to black
        yield return StartCoroutine(FadeOut());

        // The death message takes over the black screen (its own canvas, above the HUD)
        bool showMessage = !string.IsNullOrWhiteSpace(claimedMessage) || !string.IsNullOrWhiteSpace(returnedMessage);
        if (showMessage)
        {
            ShowMessageScreen();
        }

        // Clean up existing crypt objects
        CleanupCryptObjects();

        // Reset GameManager crypt-related stats
        ResetCryptStats();

        // "The Crypt Fiend has claimed your soul." (still down in the crypt)
        yield return StartCoroutine(ShowMessageCard(claimedMessage, claimedMessageHold));

        // Teleport player to checkpoint
        TeleportPlayerToCheckpoint();

        // "You have been returned to the surface..." on a fresh black screen, now on the surface
        yield return StartCoroutine(ShowMessageCard(returnedMessage, returnedMessageHold));

        // Re-enable player controls and flashlight
        RestorePlayerState();

        // Wait at black screen
        yield return new WaitForSeconds(blackScreenDelay);

        // Respawn crypt objects
        SpawnCryptObjects();

        // Play respawn sound
        if (respawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(respawnSound, respawnSoundVolume);
        }

        // Hand the black screen back to the fade overlay, then fade in
        if (showMessage)
        {
            HideMessageScreen();
        }
        yield return StartCoroutine(FadeIn());

        isRespawning = false;
        Debug.Log("CryptCheckpointManager: Respawn complete");
    }

    private IEnumerator FadeOut()
    {
        if (fadeOverlay == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeOutDuration);
            yield return null;
        }
        fadeOverlay.alpha = 1f;
    }

    private IEnumerator FadeIn()
    {
        if (fadeOverlay == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeInDuration);
            yield return null;
        }
        fadeOverlay.alpha = 0f;
    }

    /// <summary>
    /// Freezes movement and look until RestorePlayerState. The jumpscare already switched the
    /// Movement component off; these flags also tell the pause menu that the player is held.
    /// </summary>
    private void LockPlayerInput()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Movement movement = player != null ? player.GetComponent<Movement>() : null;
        if (movement != null)
        {
            movement.DisableAllInput();
        }
    }

    /// <summary>Puts up the death message's own black screen (at once: the fade overlay is already black).</summary>
    private void ShowMessageScreen()
    {
        EnsureMessageUI();

        // Applied on every death, so a language picked in the pause menu since the last one is honoured
        TMP_FontAsset authoredFont = messageFont != null ? messageFont : EndingFonts.Get(Ending.WrongfulConviction);
        Loc.ApplyFont(messageText, authoredFont, messageFontSize);
        messageText.color = messageColor;
        messageText.alpha = 0f;
        messageText.text = string.Empty;
        messageRoot.SetActive(true);
    }

    private void HideMessageScreen()
    {
        if (messageRoot != null)
        {
            messageRoot.SetActive(false);
        }
    }

    /// <summary>One card: a silent beat, then the line fades in, holds, and fades out again.</summary>
    private IEnumerator ShowMessageCard(string englishText, float hold)
    {
        if (string.IsNullOrWhiteSpace(englishText) || messageText == null) yield break;

        yield return StartCoroutine(WaitUnscaled(messagePause));

        messageText.text = Loc.Tr(englishText);
        yield return StartCoroutine(FadeMessage(0f, 1f));
        yield return StartCoroutine(WaitUnscaled(hold));
        yield return StartCoroutine(FadeMessage(1f, 0f));
        messageText.text = string.Empty;
    }

    private IEnumerator FadeMessage(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < messageFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            messageText.alpha = Mathf.SmoothStep(from, to, elapsed / messageFadeDuration);
            yield return null;
        }
        messageText.alpha = to;
    }

    // Counts frame time like the fades do, so the cards keep their pacing frame for frame
    private IEnumerator WaitUnscaled(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>Builds the death message UI once: a black screen with one centred block of text.</summary>
    private void EnsureMessageUI()
    {
        if (messageRoot != null) return;

        messageRoot = new GameObject("CryptDeathMessage", typeof(RectTransform));
        messageRoot.transform.SetParent(transform, false);

        Canvas canvas = messageRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above the HUD and the respawn fade overlay (sorting order 0)

        CanvasScaler scaler = messageRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backdrop = new GameObject("Black", typeof(RectTransform));
        backdrop.transform.SetParent(messageRoot.transform, false);
        Image black = backdrop.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;
        Stretch(black.rectTransform, Vector2.zero, Vector2.one);

        GameObject textObject = new GameObject("Message", typeof(RectTransform));
        textObject.transform.SetParent(messageRoot.transform, false);
        messageText = textObject.AddComponent<TextMeshProUGUI>();
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.lineSpacing = 20f;
        messageText.raycastTarget = false;
        Stretch(messageText.rectTransform, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f));

        messageRoot.SetActive(false);
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void CleanupCryptObjects()
    {
        // Destroy current shovel if it exists
        if (currentShovel != null)
        {
            Destroy(currentShovel);
            currentShovel = null;
        }

        // Destroy all tracked dirt piles
        foreach (var pile in currentDirtPiles)
        {
            if (pile != null)
            {
                Destroy(pile);
            }
        }
        currentDirtPiles.Clear();

        // Also find any dirt piles that might have been spawned but not tracked
        DirtPileInteractable[] remainingPiles = FindObjectsOfType<DirtPileInteractable>();
        foreach (var pile in remainingPiles)
        {
            Destroy(pile.gameObject);
        }

        // Destroy current killer
        if (currentKiller != null)
        {
            Destroy(currentKiller);
            currentKiller = null;
        }

        // Also find any killers that might exist
        CryptKiller[] remainingKillers = FindObjectsOfType<CryptKiller>();
        foreach (var killer in remainingKillers)
        {
            Destroy(killer.gameObject);
        }

        // Notify EndingScenarioManager to reset its reference
        if (EndingScenarioManager.Instance != null)
        {
            EndingScenarioManager.Instance.ResetForRespawn();
        }

        Debug.Log("CryptCheckpointManager: Cleaned up crypt objects");
    }

    private void ResetCryptStats()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CorrectDigCount = 0;
            GameManager.Instance.IncorrectDigCount = 0;
            GameManager.Instance.ShovelPickedUp = false;
            GameManager.Instance.ShovelBroken = false;
            Debug.Log("CryptCheckpointManager: Reset crypt stats in GameManager");
        }
    }

    private void TeleportPlayerToCheckpoint()
    {
        if (checkpointSpawnPoint == null)
        {
            Debug.LogError("CryptCheckpointManager: No checkpoint spawn point set!");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("CryptCheckpointManager: Player not found!");
            return;
        }

        // Disable CharacterController to allow teleport
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // Teleport player
        player.transform.position = checkpointSpawnPoint.position;

        // Apply rotation - use custom rotation if enabled, otherwise use spawn point rotation
        if (useCustomRotation)
        {
            player.transform.rotation = Quaternion.Euler(checkpointRotation);
        }
        else
        {
            player.transform.rotation = checkpointSpawnPoint.rotation;
        }

        // Re-enable CharacterController
        if (controller != null)
        {
            controller.enabled = true;
        }

        Debug.Log($"CryptCheckpointManager: Teleported player to {checkpointSpawnPoint.position}");
    }

    private void RestorePlayerState()
    {
        // Reset time scale
        Time.timeScale = 1f;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        // Re-enable CharacterController FIRST
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = true;
        }

        // Re-enable all player scripts that may have been disabled by jumpscare
        ReEnablePlayerScripts(player);

        // Re-enable movement
        Movement movement = player.GetComponent<Movement>();
        if (movement != null)
        {
            movement.enabled = true;
            movement.EnableAllInput();
        }

        // Reset flashlight to normal state
        SimpleFlashlight flashlight = FindObjectOfType<SimpleFlashlight>();
        if (flashlight != null)
        {
            flashlight.enabled = true;
            flashlight.ReEnableAfterKillerDisable();

            // Reset flashlight intensity/range to defaults
            if (flashlight.spotLight != null)
            {
                flashlight.spotLight.intensity = flashlight.intensity;
                flashlight.spotLight.range = flashlight.range;
                flashlight.spotLight.spotAngle = flashlight.spotAngle;
            }
        }

        // Reset VHS effects
        ResetVHSEffects();

        // Reset sanity to full (also resets sprint, VHS thresholds, etc.)
        if (SanityManager.Instance != null)
        {
            SanityManager.Instance.ResetSanity();
        }

        // Restore lighting to pre-crypt state
        RestoreLightingSettings();

        // Hide game over UI if visible
        HideGameOverUI();

        // Show crosshair
        CrosshairManager crosshair = FindObjectOfType<CrosshairManager>();
        if (crosshair != null)
        {
            crosshair.Show();
        }

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("CryptCheckpointManager: Restored player state");
    }

    private void ReEnablePlayerScripts(GameObject player)
    {
        // Re-enable scripts on player
        MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            string typeName = script.GetType().Name;
            if (typeName.Contains("Controller") || typeName.Contains("Movement") ||
                typeName.Contains("Player") || typeName.Contains("Input"))
            {
                script.enabled = true;
            }
        }

        // Check parent too
        if (player.transform.parent != null)
        {
            scripts = player.transform.parent.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                string typeName = script.GetType().Name;
                if (typeName.Contains("Controller") || typeName.Contains("Movement") ||
                    typeName.Contains("Player") || typeName.Contains("Input"))
                {
                    script.enabled = true;
                }
            }
        }

        // Re-enable camera/look scripts on children
        scripts = player.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            string typeName = script.GetType().Name;
            if (typeName.Contains("MouseLook") || typeName.Contains("CameraController") ||
                typeName.Contains("Look") || typeName.Contains("Input"))
            {
                script.enabled = true;
            }
        }
    }

    private void ResetVHSEffects()
    {
        if (!vhsSettingsCaptured)
        {
            Debug.LogWarning("CryptCheckpointManager: VHS settings were not captured, cannot restore");
            return;
        }

        Camera playerCamera = Camera.main;
        if (playerCamera == null) return;

        VHSRetroFeature vhsEffect = playerCamera.GetComponent<VHSRetroFeature>();
        if (vhsEffect != null)
        {
            // Restore to captured values from before jumpscare
            vhsEffect.glitchIntensity = capturedGlitchIntensity;
            vhsEffect.rgbShiftAmount = capturedRGBShift;
            vhsEffect.noiseIntensity = capturedNoiseIntensity;
            vhsEffect.scanlineIntensity = capturedScanlineIntensity;
            vhsEffect.trackingNoise = capturedTrackingNoise;
            Debug.Log("CryptCheckpointManager: Restored VHS effects to captured values");
        }
    }

    private void RestoreLightingSettings()
    {
        if (!lightingSettingsCaptured)
        {
            Debug.LogWarning("CryptCheckpointManager: Lighting settings were not captured, cannot restore");
            return;
        }

        // Restore gradient ambient colors directly
        RenderSettings.ambientSkyColor = capturedSkyColor;
        RenderSettings.ambientEquatorColor = capturedEquatorColor;
        RenderSettings.ambientGroundColor = capturedGroundColor;

        // Also update LightingController if it exists so it knows the current state
        if (LightingController.Instance != null)
        {
            LightingSettings restoredSettings = new LightingSettings
            {
                skyColor = capturedSkyColor,
                equatorColor = capturedEquatorColor,
                groundColor = capturedGroundColor
            };
            // Apply without transition for immediate restoration
            LightingController.Instance.SetTransitionDuration(0f);
            LightingController.Instance.ApplySettings(restoredSettings, LightingPreset.Custom);
            LightingController.Instance.SetTransitionDuration(1f); // Reset to default
        }

        Debug.Log("CryptCheckpointManager: Restored lighting settings to captured values");
    }

    private void HideGameOverUI()
    {
        // Find any CanvasGroup with "GameOver" in name and hide it
        CanvasGroup[] canvasGroups = FindObjectsOfType<CanvasGroup>();
        foreach (var cg in canvasGroups)
        {
            if (cg.gameObject.name.ToLower().Contains("gameover") ||
                cg.gameObject.name.ToLower().Contains("game over"))
            {
                cg.alpha = 0f;
                cg.gameObject.SetActive(false);
                Debug.Log($"CryptCheckpointManager: Hid game over UI '{cg.gameObject.name}'");
            }
        }
    }

    private void SpawnCryptObjects()
    {
        // Spawn shovel (next to the hint note if the player had already found it)
        Transform shovelPoint = shovelFoundBefore && hintShovelSpawnPoint != null ? hintShovelSpawnPoint : shovelSpawnPoint;
        if (shovelPrefab != null && shovelPoint != null)
        {
            currentShovel = Instantiate(shovelPrefab, shovelPoint.position, shovelPoint.rotation);
            Debug.Log($"CryptCheckpointManager: Spawned shovel at {shovelPoint.name}");
        }

        // Spawn dirt piles
        if (dirtPilePrefab != null && dirtPileSpawnPoints != null)
        {
            foreach (var spawnData in dirtPileSpawnPoints)
            {
                if (spawnData.spawnPoint == null) continue;

                GameObject pile = Instantiate(dirtPilePrefab, spawnData.spawnPoint.position, spawnData.spawnPoint.rotation);

                // Configure the pile type
                DirtPileInteractable dirtPile = pile.GetComponent<DirtPileInteractable>();
                if (dirtPile != null)
                {
                    dirtPile.pileType = spawnData.pileType;
                }

                currentDirtPiles.Add(pile);
            }
            Debug.Log($"CryptCheckpointManager: Spawned {currentDirtPiles.Count} dirt piles");
        }

        // Spawn killer
        if (useEndingScenarioManager && EndingScenarioManager.Instance != null)
        {
            // Use EndingScenarioManager to respawn the correct killer based on player stats. It waits until
            // the player is back in the crypt and walks out of the arrival nook; HandleKillerSpawned picks it up.
            EndingScenarioManager.Instance.RespawnKiller();
            Debug.Log("CryptCheckpointManager: Killer respawn queued via EndingScenarioManager");
        }
        else if (killerPrefab != null && killerSpawnPoint != null)
        {
            // Manual killer spawning (fallback)
            currentKiller = Instantiate(killerPrefab, killerSpawnPoint.position, killerSpawnPoint.rotation);

            // Configure the killer
            CryptKiller cryptKiller = currentKiller.GetComponent<CryptKiller>();
            if (cryptKiller != null)
            {
                if (killerConfig != null)
                {
                    cryptKiller.Initialize(killerConfig);
                }

                if (killerRoamWaypoints != null && killerRoamWaypoints.Length > 0)
                {
                    cryptKiller.SetRoamWaypoints(killerRoamWaypoints);
                }
            }

            // Ensure KillerJumpscare uses checkpoint respawn
            KillerJumpscare jumpscare = currentKiller.GetComponent<KillerJumpscare>();
            if (jumpscare != null)
            {
                jumpscare.SetUseCheckpointRespawn(true);
            }

            ApplyKillerHelp(currentKiller);

            Debug.Log("CryptCheckpointManager: Spawned killer manually");
        }

        ApplyDeathHelp();
    }

    /// <summary>
    /// After a death the crypt eases up so players don't get stuck: the hint note appears, and
    /// after enough deaths red arrows on the floor point toward the correct graves' general areas.
    /// (The killer's own help is applied when it spawns, see ApplyKillerHelp.)
    /// </summary>
    private void ApplyDeathHelp()
    {
        if (deathCount < 1) return;

        if (hintNote != null && !hintNote.activeSelf)
        {
            hintNote.SetActive(true);
            Debug.Log("CryptCheckpointManager: Hint note revealed");
        }

        if (deathCount >= arrowsAfterDeaths && hintArrows != null && !hintArrows.activeSelf)
        {
            hintArrows.SetActive(true);
            Debug.Log($"CryptCheckpointManager: Hint arrows revealed after {deathCount} deaths");
        }
    }

    /// <summary>
    /// Once the player has died, every respawned killer is slower and sees less far.
    /// </summary>
    private void ApplyKillerHelp(GameObject killerObject)
    {
        if (deathCount < 1 || killerObject == null) return;

        CryptKiller killer = killerObject.GetComponent<CryptKiller>();
        if (killer != null)
        {
            killer.ApplyPlayerHelp(helpKillerSpeedMultiplier, helpKillerVisionMultiplier, helpStalkerFlashlightRange);
        }
    }

    /// <summary>
    /// Returns true if a respawn is currently in progress.
    /// </summary>
    public bool IsRespawning() => isRespawning;
}

/// <summary>
/// Data for spawning a dirt pile at a specific location with a specific type.
/// </summary>
[System.Serializable]
public class DirtPileSpawnData
{
    public Transform spawnPoint;
    public DirtPileInteractable.DirtPileType pileType = DirtPileInteractable.DirtPileType.Incorrect;
}
