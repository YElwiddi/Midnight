using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages checkpoint respawning for the crypt area.
/// When the player dies to a crypt killer, they respawn outside the crypt
/// with all crypt objects (shovel, dirt piles, killer) reset to their initial state.
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

    // Tracking spawned objects for cleanup
    private GameObject currentShovel;
    private List<GameObject> currentDirtPiles = new List<GameObject>();
    private GameObject currentKiller;
    private AudioSource audioSource;

    // State
    private bool isRespawning = false;

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

        // Fade to black
        yield return StartCoroutine(FadeOut());

        // Clean up existing crypt objects
        CleanupCryptObjects();

        // Reset GameManager crypt-related stats
        ResetCryptStats();

        // Teleport player to checkpoint
        TeleportPlayerToCheckpoint();

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

        // Fade in
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
        // Spawn shovel
        if (shovelPrefab != null && shovelSpawnPoint != null)
        {
            currentShovel = Instantiate(shovelPrefab, shovelSpawnPoint.position, shovelSpawnPoint.rotation);
            Debug.Log("CryptCheckpointManager: Spawned shovel");
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
            // Use EndingScenarioManager to respawn the correct killer based on player stats
            EndingScenarioManager.Instance.RespawnKiller();
            currentKiller = EndingScenarioManager.Instance.GetSpawnedKiller();
            Debug.Log("CryptCheckpointManager: Respawned killer via EndingScenarioManager");
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

            Debug.Log("CryptCheckpointManager: Spawned killer manually");
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
