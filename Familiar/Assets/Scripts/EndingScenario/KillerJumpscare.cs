using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Standalone jumpscare component that can be added to any killer.
/// Handles the full game over sequence: freeze player, position killer,
/// camera lock, flashlight, VHS effects, and scene transition.
/// </summary>
public class KillerJumpscare : MonoBehaviour
{
    [Header("Killer Face Target")]
    [Tooltip("Transform to look at during jumpscare. If not set, uses faceHeightOffset.")]
    [SerializeField] private Transform killerFace;

    [Tooltip("If no face transform, camera looks at killer position + this height")]
    [SerializeField] private float faceHeightOffset = 1.6f;

    [Header("Killer Positioning")]
    [Tooltip("Distance to place killer in front of player during jumpscare")]
    [SerializeField] private float killStopDistance = 1.5f;

    [Header("Player Adjustment")]
    [Tooltip("Lower the player during jumpscare so they look UP at killer (negative = lower)")]
    [SerializeField] private float playerHeightOffset = -0.3f;

    [Header("Animation")]
    [Tooltip("Animation trigger to play during kill")]
    [SerializeField] private string killAnimationTrigger = "Kill";

    [Header("Audio")]
    [Tooltip("Sound to play during jumpscare")]
    [SerializeField] private AudioClip jumpscareSound;
    [SerializeField] private float jumpscareSoundVolume = 1f;

    [Header("Timing")]
    [Tooltip("Delay before loading game over scene (real seconds)")]
    [SerializeField] private float gameOverDelay = 2f;

    [Header("Slow Motion")]
    [SerializeField] private bool useSlowMotion = true;
    [SerializeField] private float slowMotionTimeScale = 0.3f;
    [SerializeField] private float slowMotionDuration = 2f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float shakeDuration = 2f;

    [Header("Flashlight")]
    [Tooltip("Height on killer to point flashlight at")]
    [SerializeField] private float flashlightTargetHeight = 1.2f;
    [SerializeField] private float jumpscareFlashlightIntensity = 3f;
    [SerializeField] private float jumpscareFlashlightRange = 15f;

    [Header("VHS Effect")]
    [SerializeField] private bool intensifyVHSOnKill = true;
    [SerializeField] private float killGlitchIntensity = 0.7f;
    [SerializeField] private float killRGBShift = 0.04f;
    [SerializeField] private float killNoiseIntensity = 0.25f;
    [SerializeField] private float killScanlineIntensity = 0.8f;
    [SerializeField] private float killTrackingNoise = 0.1f;

    [Header("Screen Effect (Fallback)")]
    [SerializeField] private GameObject screenEffectPrefab;
    [SerializeField] private float screenEffectDelay = 0f;

    [Header("Game Over")]
    [SerializeField] private CanvasGroup gameOverUI;
    [SerializeField] private string gameOverSceneName = "";

    [Header("Checkpoint Respawn")]
    [Tooltip("If true, respawns at checkpoint instead of loading a scene. Requires CryptCheckpointManager in scene.")]
    [SerializeField] private bool useCheckpointRespawn = false;

    // Runtime references
    private Transform playerTransform;
    private Camera playerCamera;
    private AudioSource audioSource;
    private Animator animator;
    private NavMeshAgent navAgent;
    private Light jumpscareSpotlight;

    // Shake state
    private float currentShakeAmount = 0f;
    private float currentShakeDuration = 0f;
    private bool isShaking = false;
    private bool jumpscareActive = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        navAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        FindPlayer();
        FindKillerFace();
    }

    private void FindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private void FindKillerFace()
    {
        if (killerFace != null) return;

        // Try to find common face/head bone names
        string[] faceNames = { "Head", "head", "Face", "face", "Skull", "skull", "Bip01 Head", "mixamorig:Head" };
        foreach (string faceName in faceNames)
        {
            Transform found = FindChildRecursive(transform, faceName);
            if (found != null)
            {
                killerFace = found;
                Debug.Log($"KillerJumpscare: Auto-found face transform '{faceName}'");
                return;
            }
        }

        Debug.Log($"KillerJumpscare: No face transform found, using height offset {faceHeightOffset}");
    }

    private void Update()
    {
        if (!jumpscareActive) return;

        if (playerCamera != null)
        {
            if (isShaking && currentShakeDuration > 0)
            {
                ApplyCameraShake();
            }
            else
            {
                LockCameraOnKiller();
            }
        }

        UpdateSpotlightTarget();
    }

    /// <summary>
    /// Trigger the jumpscare sequence. Call this when the killer catches the player.
    /// </summary>
    public void TriggerJumpscare()
    {
        if (jumpscareActive) return;

        FindPlayer();
        jumpscareActive = true;
        StartCoroutine(JumpscareSequence());
    }

    private IEnumerator JumpscareSequence()
    {
        Debug.Log("KillerJumpscare: Starting jumpscare sequence");

        // Close any open ReadableUI (notes, books, etc.)
        CloseReadableUI();

        // Close any active dialogues
        CloseAllDialogues();

        // Stop killer movement
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        // Freeze player FIRST
        FreezePlayer(true);

        // Ground the player
        GroundPlayer();

        // Lower player position for dramatic upward angle
        if (playerTransform != null && playerHeightOffset != 0f)
        {
            CharacterController controller = playerTransform.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            Vector3 loweredPosition = playerTransform.position;
            loweredPosition.y += playerHeightOffset;
            playerTransform.position = loweredPosition;
        }

        // Lock flashlight on
        LockFlashlightOn();

        // Position killer in front of player camera
        if (playerCamera != null)
        {
            PositionKillerInFrontOfPlayer();

            // Make camera look at killer's face
            Vector3 lookTarget = GetKillerFacePosition();
            playerCamera.transform.LookAt(lookTarget);

            // Point flashlight at killer
            PointFlashlightAtKiller();
        }

        // Play jumpscare sound
        if (jumpscareSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpscareSound, jumpscareSoundVolume);
        }

        // Play kill animation
        if (animator != null && !string.IsNullOrEmpty(killAnimationTrigger))
        {
            animator.SetTrigger(killAnimationTrigger);
        }

        // Spawn screen effect / VHS intensify
        StartCoroutine(ApplyScreenEffect());

        // Start slow motion
        float originalTimeScale = Time.timeScale;
        float originalFixedDeltaTime = Time.fixedDeltaTime;

        if (useSlowMotion)
        {
            Time.timeScale = slowMotionTimeScale;
            Time.fixedDeltaTime = 0.02f * slowMotionTimeScale;
        }

        // Start camera shake
        StartCameraShake();

        // Show game over UI
        if (gameOverUI != null)
        {
            StartCoroutine(FadeInUI());
        }

        // Wait for sequence
        yield return new WaitForSecondsRealtime(slowMotionDuration);

        if (gameOverDelay > 0)
        {
            yield return new WaitForSecondsRealtime(gameOverDelay);
        }

        // Restore time scale
        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;

        // Execute game over
        ExecuteGameOver();
    }

    private void PositionKillerInFrontOfPlayer()
    {
        Vector3 cameraForward = playerCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 targetPosition = playerCamera.transform.position + cameraForward * killStopDistance;

        // Ground the killer
        if (Physics.Raycast(targetPosition + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
        {
            targetPosition.y = hit.point.y;
        }
        else if (playerTransform != null)
        {
            targetPosition.y = playerTransform.position.y;
        }

        transform.position = targetPosition;

        // Face the player
        Vector3 directionToPlayer = playerCamera.transform.position - transform.position;
        directionToPlayer.y = 0;
        if (directionToPlayer.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(directionToPlayer);
        }
    }

    private Vector3 GetKillerFacePosition()
    {
        if (killerFace != null)
        {
            return killerFace.position;
        }
        return transform.position + Vector3.up * faceHeightOffset;
    }

    private void LockCameraOnKiller()
    {
        Vector3 facePosition = GetKillerFacePosition();
        playerCamera.transform.LookAt(facePosition);
    }

    private void StartCameraShake()
    {
        currentShakeAmount = shakeIntensity;
        currentShakeDuration = shakeDuration;
        isShaking = true;
    }

    private void ApplyCameraShake()
    {
        if (playerCamera == null) return;

        Vector3 facePosition = GetKillerFacePosition();
        Vector3 lookDir = (facePosition - playerCamera.transform.position).normalized;
        Quaternion baseRotation = Quaternion.LookRotation(lookDir);

        Vector3 shakeOffset = new Vector3(
            Random.Range(-currentShakeAmount, currentShakeAmount),
            Random.Range(-currentShakeAmount, currentShakeAmount),
            0
        );

        playerCamera.transform.rotation = baseRotation * Quaternion.Euler(shakeOffset);

        // Decrease shake over time (use unscaled time for slow-mo)
        currentShakeDuration -= Time.unscaledDeltaTime;
        currentShakeAmount = Mathf.Lerp(0, shakeIntensity, currentShakeDuration / shakeDuration);

        if (currentShakeDuration <= 0)
        {
            isShaking = false;
            currentShakeDuration = 0f;
            currentShakeAmount = 0f;
        }
    }

    private void FreezePlayer(bool freeze)
    {
        if (playerTransform == null) return;

        // Disable CharacterController
        CharacterController controller = playerTransform.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = !freeze;
        }

        // Disable movement/controller scripts
        MonoBehaviour[] scripts = playerTransform.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            string typeName = script.GetType().Name;
            if (typeName.Contains("Controller") || typeName.Contains("Movement") ||
                typeName.Contains("Player") || typeName.Contains("Input"))
            {
                script.enabled = !freeze;
            }
        }

        // Check parent too
        if (playerTransform.parent != null)
        {
            scripts = playerTransform.parent.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                string typeName = script.GetType().Name;
                if (typeName.Contains("Controller") || typeName.Contains("Movement") ||
                    typeName.Contains("Player") || typeName.Contains("Input"))
                {
                    script.enabled = !freeze;
                }
            }
        }

        // Also check children (camera rigs, etc.)
        scripts = playerTransform.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            string typeName = script.GetType().Name;
            if (typeName.Contains("MouseLook") || typeName.Contains("CameraController") ||
                typeName.Contains("Look") || typeName.Contains("Input"))
            {
                script.enabled = !freeze;
            }
        }

        Debug.Log($"KillerJumpscare: Player {(freeze ? "frozen" : "unfrozen")}");
    }

    private void GroundPlayer()
    {
        if (playerTransform == null) return;

        Vector3 rayStart = playerTransform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f))
        {
            CharacterController controller = playerTransform.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                playerTransform.position = hit.point;
                // Keep disabled - FreezePlayer handles this
            }
            else
            {
                playerTransform.position = hit.point;
            }
        }
    }

    private void CloseReadableUI()
    {
        if (ReadableUI.Instance != null && ReadableUI.Instance.IsOpen)
        {
            ReadableUI.Instance.Close();
            Debug.Log("KillerJumpscare: Closed ReadableUI");
        }
    }

    private void CloseAllDialogues()
    {
        // Cancel any active SimpleDialogueTrigger dialogues
        SimpleDialogueTrigger.CancelActiveSimpleDialogue();

        // Close DialogueUI (used for choice dialogues like dirt pile confirmation)
        DialogueUI dialogueUI = FindObjectOfType<DialogueUI>();
        if (dialogueUI != null && dialogueUI.IsVisible)
        {
            dialogueUI.ClearChoices();
            dialogueUI.Hide();
            Debug.Log("KillerJumpscare: Closed DialogueUI");
        }

        // Exit any active Ink dialogue
        DialogueManager dialogueManager = DialogueManager.GetInstance();
        if (dialogueManager != null && dialogueManager.IsDialoguePlaying())
        {
            dialogueManager.ExitDialogueMode();
            Debug.Log("KillerJumpscare: Closed DialogueManager");
        }
    }

    private void LockFlashlightOn()
    {
        SimpleFlashlight flashlight = FindObjectOfType<SimpleFlashlight>();
        if (flashlight != null)
        {
            flashlight.enabled = false;
            if (flashlight.spotLight != null)
            {
                flashlight.spotLight.enabled = true;
            }
        }

        // Hide crosshair
        CrosshairManager crosshair = FindObjectOfType<CrosshairManager>();
        if (crosshair != null)
        {
            crosshair.Hide();
        }
    }

    private void PointFlashlightAtKiller()
    {
        SimpleFlashlight flashlight = FindObjectOfType<SimpleFlashlight>();
        if (flashlight != null && flashlight.spotLight != null)
        {
            jumpscareSpotlight = flashlight.spotLight;
            jumpscareSpotlight.enabled = true;
            jumpscareSpotlight.intensity = jumpscareFlashlightIntensity;
            jumpscareSpotlight.range = jumpscareFlashlightRange;
            jumpscareSpotlight.spotAngle = 60f;
            UpdateSpotlightTarget();
        }
    }

    private void UpdateSpotlightTarget()
    {
        if (jumpscareSpotlight == null || playerCamera == null) return;

        jumpscareSpotlight.transform.position = playerCamera.transform.position;
        Vector3 targetPosition = transform.position + Vector3.up * flashlightTargetHeight;
        jumpscareSpotlight.transform.LookAt(targetPosition);
    }

    private IEnumerator ApplyScreenEffect()
    {
        if (screenEffectDelay > 0)
        {
            yield return new WaitForSecondsRealtime(screenEffectDelay);
        }

        // Try VHS effect first
        if (playerCamera != null)
        {
            VHSRetroFeature vhsEffect = playerCamera.GetComponent<VHSRetroFeature>();
            if (vhsEffect != null && intensifyVHSOnKill)
            {
                vhsEffect.enabled = true;
                vhsEffect.glitchIntensity = killGlitchIntensity;
                vhsEffect.rgbShiftAmount = killRGBShift;
                vhsEffect.noiseIntensity = killNoiseIntensity;
                vhsEffect.scanlineIntensity = killScanlineIntensity;
                vhsEffect.trackingNoise = killTrackingNoise;
                yield break;
            }
        }

        // Fallback to prefab
        if (screenEffectPrefab != null)
        {
            Instantiate(screenEffectPrefab);
        }
    }

    private IEnumerator FadeInUI()
    {
        if (gameOverUI == null) yield break;

        gameOverUI.gameObject.SetActive(true);
        float elapsed = 0f;
        float duration = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            gameOverUI.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        gameOverUI.alpha = 1f;
    }

    private void ExecuteGameOver()
    {
        Debug.Log("KillerJumpscare: Game Over!");

        // Check for checkpoint respawn first
        if (useCheckpointRespawn && CryptCheckpointManager.Instance != null)
        {
            Debug.Log("KillerJumpscare: Using checkpoint respawn");
            CryptCheckpointManager.Instance.RespawnAtCheckpoint();
            return;
        }

        if (gameOverUI != null)
        {
            gameOverUI.alpha = 1f;
            gameOverUI.gameObject.SetActive(true);
        }

        // Reset game state before loading new scene
        // GameManager persists across scenes (DontDestroyOnLoad), so reset it here
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetAllFlags();
            Debug.Log("KillerJumpscare: GameManager stats reset");
        }

        // Reset time scale in case slow motion was active
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName);
        }
    }

    /// <summary>
    /// Set whether this jumpscare should use checkpoint respawn instead of scene loading.
    /// </summary>
    public void SetUseCheckpointRespawn(bool value)
    {
        useCheckpointRespawn = value;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
