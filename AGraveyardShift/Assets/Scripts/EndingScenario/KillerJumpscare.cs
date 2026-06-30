using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("Procedurally rotate the face bone to stare straight at the player during the jumpscare (overrides wherever the animation points the head).")]
    [SerializeField] private bool aimHeadAtPlayer = false;
    [Tooltip("Which LOCAL axis of the face bone points out of the face (Maria/Tomino head faces -Z).")]
    [SerializeField] private Vector3 headFaceAxis = new Vector3(0f, 0f, -1f);
    [Tooltip("Crane/weave the neck left-right during the jumpscare (degrees of tilt, 0 = off). The head still re-locks onto the player on top of this.")]
    [SerializeField] private float neckCraneAngle = 0f;
    [SerializeField] private float neckCraneSpeed = 5f;
    [SerializeField] private string neckBoneName = "Neck";

    [Header("Killer Positioning")]
    [Tooltip("Distance to place killer in front of player during jumpscare")]
    [SerializeField] private float killStopDistance = 1.5f;
    [Tooltip("During the jumpscare, push the killer this much further from the camera (m) so a long neck doesn't crowd in front of the face. 0 = off.")]
    [SerializeField] private float jumpscareBodyPullback = 0f;

    [Header("Player Adjustment")]
    [Tooltip("Lower the player during jumpscare so they look UP at killer (negative = lower)")]
    [SerializeField] private float playerHeightOffset = -0.3f;
    [Tooltip("Raise the camera to just below the killerFace bone (instead of the fixed offset above) — keeps a tall/giant killer's face framed regardless of its height/scale.")]
    [SerializeField] private bool raiseCameraToFaceBone = false;
    [Tooltip("When raiseCameraToFaceBone is on: how far below the face bone to place the camera (m). Larger = more looking-up at the face.")]
    [SerializeField] private float raiseCameraFaceGap = 2f;

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

    [Header("Screen Flash")]
    [Tooltip("Strobe the screen to black and back during the jumpscare.")]
    [SerializeField] private bool screenFlash = false;
    [Tooltip("Seconds the screen stays black per strobe.")]
    [SerializeField] private float flashBlackDuration = 0.07f;
    [Tooltip("Seconds the screen is visible between strobes.")]
    [SerializeField] private float flashVisibleDuration = 0.18f;
    [Tooltip("How many times to flash. After the last flash the jumpscare ends.")]
    [SerializeField] private int flashCount = 4;
    [Tooltip("On the final flash, punch the camera this close to the face (m). 0 = no punch-in.")]
    [SerializeField] private float finalCloseupDistance = 1.8f;
    [Tooltip("FOV on the final close-up (0 = keep current).")]
    [SerializeField] private float finalCloseupFOV = 0f;
    [Tooltip("Seconds to hold the final close-up before the jumpscare ends.")]
    [SerializeField] private float finalCloseupHold = 1f;
    [Tooltip("Raise the final close-up's aim by this much (world units) so the FULL face is framed (the Head bone sits low, near the jaw).")]
    [SerializeField] private float finalCloseupAimUp = 0f;
    [Tooltip("Name of a GameObject whose audio to cut when the jumpscare ends (e.g. the looping jumpscare sound). Empty = none.")]
    [SerializeField] private string cutSoundObjectName = "";
    [Tooltip("Freeze-frame mode: between flashes everything holds still; each flash snaps the neck to a wildly different contorted pose, and the final flash zooms in on finalTargetBone. The camera holds a fixed point so the head visibly jumps around.")]
    [SerializeField] private bool freezeFramePoses = false;
    [Tooltip("Max random neck contortion per flash (degrees).")]
    [SerializeField] private float neckContortRange = 40f;
    [Tooltip("Bone the FINAL flash zooms in on (empty = the face bone). e.g. 'Neck'.")]
    [SerializeField] private string finalTargetBoneName = "";

    [Header("Camera Zoom")]
    [Tooltip("Field of view to zoom the camera to during the jumpscare (0 = keep the current FOV). Lower = tighter on the face. Useful for tall killers whose face is far from the camera.")]
    [SerializeField] private float jumpscareCameraFOV = 0f;

    [Tooltip("FOV pulse amplitude during the jumpscare for a zoom-in/zoom-out shake (0 = off). The pulse is irregular/jerky, not a smooth sine.")]
    [SerializeField] private float jumpscareZoomShakeAmount = 0f;
    [Tooltip("Speed of the zoom-in/zoom-out shake pulse.")]
    [SerializeField] private float jumpscareZoomShakeSpeed = 20f;

    [Tooltip("Camera roll (steering-wheel) shake amplitude in degrees during the jumpscare (0 = off). Keep small — only slightly noticeable.")]
    [SerializeField] private float jumpscareRollShakeAmount = 0f;
    [Tooltip("Speed of the camera-roll shake.")]
    [SerializeField] private float jumpscareRollShakeSpeed = 14f;

    [Header("Limb Shake")]
    [Tooltip("If > 0, violently jitters the killer's limb bones during the jumpscare — max random rotation per bone in degrees (0 = off).")]
    [SerializeField] private float jumpscareLimbShakeAngle = 0f;
    [Tooltip("How many times per second the limb jitter re-randomizes (movements/sec).")]
    [SerializeField] private float jumpscareLimbShakeFrequency = 20f;
    [Tooltip("Bone-name fragments to jitter (case-insensitive substring). Empty = Mixamo arms + legs.")]
    [SerializeField] private string[] jumpscareLimbShakeBones;

    [Header("Flashlight")]
    [Tooltip("Height on killer to point flashlight at")]
    [SerializeField] private float flashlightTargetHeight = 1.2f;
    [Tooltip("Aim the flashlight at the killerFace bone instead of a fixed height (for tall killers whose face is far up).")]
    [SerializeField] private bool aimFlashlightAtFaceBone = false;
    [SerializeField] private float jumpscareFlashlightIntensity = 3f;
    [SerializeField] private float jumpscareFlashlightRange = 15f;

    [Header("VHS Effect")]
    [SerializeField] private bool intensifyVHSOnKill = true;
    [Tooltip("Disable the VHS retro effect entirely during the jumpscare (kills the glitch/RGB 'double-image' for a clean face). Overrides intensifyVHSOnKill.")]
    [SerializeField] private bool disableVHSDuringJumpscare = false;
    [SerializeField] private float killGlitchIntensity = 0.7f;
    [SerializeField] private float killRGBShift = 0.04f;
    [SerializeField] private float killNoiseIntensity = 0.25f;
    [SerializeField] private float killScanlineIntensity = 0.8f;
    [SerializeField] private float killTrackingNoise = 0.1f;

    [Header("Lantern Override")]
    [Tooltip("If true, all lanterns turn on, change color, and become locked during jumpscare")]
    [SerializeField] private bool overrideLanternsOnJumpscare = false;
    [Tooltip("Color to set all lanterns to during jumpscare")]
    [SerializeField] private Color jumpscareLanternColor = Color.red;

    [Header("Screen Effect (Fallback)")]
    [SerializeField] private GameObject screenEffectPrefab;
    [SerializeField] private float screenEffectDelay = 0f;

    [Header("Game Over")]
    [SerializeField] private CanvasGroup gameOverUI;
    [SerializeField] private string gameOverSceneName = "";

    [Header("Endings Integration")]
    [Tooltip("If true, the kill unlocks an ending, shows the reveal screen, and returns to the main menu (instead of using gameOverSceneName).")]
    [SerializeField] private bool unlocksEnding = false;
    [Tooltip("Which ending to unlock when this jumpscare kills the player.")]
    [SerializeField] private Ending endingToUnlock = Ending.Father;

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
    private Transform neckBone;
    private GameObject flashOverlay;
    private Vector3 frozenLookTarget;
    private Quaternion neckFrozenRot = Quaternion.identity;
    private Transform finalTargetBone;

    // Shake state
    private float currentShakeAmount = 0f;
    private float currentShakeDuration = 0f;
    private bool isShaking = false;
    private bool jumpscareActive = false;
    private float baseJumpscareFOV;
    private float jumpscareZoomElapsed;

    // Limb-shake state
    private Transform[] limbBones;
    private Quaternion[] limbJitterOffsets;
    private float limbShakeTimer;
    private bool limbShakeReady;

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

        jumpscareZoomElapsed += Time.unscaledDeltaTime;

        if (playerCamera != null)
        {
            if (freezeFramePoses)
            {
                // Frozen: hold the camera on a fixed point. The head visibly jumps around via the per-flash
                // neck contortion; no shake/roll/zoom between flashes.
                playerCamera.transform.LookAt(frozenLookTarget);
            }
            else
            {
                if (isShaking && currentShakeDuration > 0)
                {
                    ApplyCameraShake();
                }
                else
                {
                    LockCameraOnKiller();
                }

                // Subtle, shaky roll around the view axis (steering-wheel wobble) — applied on top of the look.
                if (jumpscareRollShakeAmount > 0f)
                {
                    float roll = ShakeNoise(jumpscareZoomElapsed * jumpscareRollShakeSpeed, 50f) * jumpscareRollShakeAmount;
                    playerCamera.transform.Rotate(0f, 0f, roll, Space.Self);
                }

                // Violent, irregular zoom-in/zoom-out shake — pulse the FOV around its base.
                if (jumpscareZoomShakeAmount > 0f)
                {
                    float pulse = ShakeNoise(jumpscareZoomElapsed * jumpscareZoomShakeSpeed, 0f) * jumpscareZoomShakeAmount;
                    playerCamera.fieldOfView = baseJumpscareFOV + pulse;
                }
            }
        }

        UpdateSpotlightTarget();
    }

    /// <summary>
    /// Irregular, jerky shake value in roughly [-1.5, 1.5]. Sums incommensurate sines (so it never
    /// repeats — "inconsistent" patterns) with Perlin drift; the high-frequency term makes it violent.
    /// </summary>
    private float ShakeNoise(float t, float seed)
    {
        float s = Mathf.Sin(t + seed)
                + Mathf.Sin(t * 3.1f + seed * 1.7f) * 0.8f
                + Mathf.Sin(t * 5.3f + seed * 2.3f) * 0.6f;
        float perlin = Mathf.PerlinNoise(t * 0.8f + seed, seed * 0.5f) * 2f - 1f;
        return (s * 0.45f) + (perlin * 0.45f);
    }

    /// <summary>Collects the limb bones to jitter during the jumpscare (defaults to Mixamo arms + legs).</summary>
    private void SetupLimbShake()
    {
        string[] frags = (jumpscareLimbShakeBones != null && jumpscareLimbShakeBones.Length > 0)
            ? jumpscareLimbShakeBones
            : new[] { "arm", "leg" };

        var found = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLowerInvariant();
            foreach (string f in frags)
            {
                if (!string.IsNullOrEmpty(f) && n.Contains(f.ToLowerInvariant())) { found.Add(t); break; }
            }
        }
        limbBones = found.ToArray();
        limbJitterOffsets = new Quaternion[limbBones.Length];
        for (int i = 0; i < limbJitterOffsets.Length; i++) limbJitterOffsets[i] = Quaternion.identity;
        limbShakeTimer = 0f;
        limbShakeReady = false;
    }

    // Runs after the Animator so these are layered on top of the (animated) jumpscare pose.
    private void LateUpdate()
    {
        if (!jumpscareActive) return;

        // Neck: freeze-frame mode holds a per-flash random contortion; otherwise weave continuously.
        // Either runs before the head-aim so the face still re-locks onto the player on top of it.
        if (freezeFramePoses)
        {
            if (neckBone == null) neckBone = FindChildRecursive(transform, neckBoneName);
            if (neckBone != null) neckBone.localRotation = neckBone.localRotation * neckFrozenRot;
        }
        else if (neckCraneAngle != 0f && playerCamera != null)
        {
            if (neckBone == null) neckBone = FindChildRecursive(transform, neckBoneName);
            if (neckBone != null)
            {
                float a = neckCraneAngle * Mathf.Sin(Time.unscaledTime * neckCraneSpeed);
                Vector3 viewDir = playerCamera.transform.position - neckBone.position;
                if (viewDir.sqrMagnitude > 0.0001f)
                    neckBone.rotation = Quaternion.AngleAxis(a, viewDir.normalized) * neckBone.rotation;
            }
        }

        // Aim the face bone straight at the player so she stares directly at them (the kill animation
        // may point the head elsewhere). Rotates the head's face axis to the camera each frame.
        if (aimHeadAtPlayer && killerFace != null && playerCamera != null)
        {
            Vector3 faceDir = killerFace.rotation * headFaceAxis.normalized;
            Vector3 toCam = playerCamera.transform.position - killerFace.position;
            if (toCam.sqrMagnitude > 0.0001f && faceDir.sqrMagnitude > 0.0001f)
                killerFace.rotation = Quaternion.FromToRotation(faceDir, toCam.normalized) * killerFace.rotation;
        }

        if (jumpscareLimbShakeAngle <= 0f || limbBones == null) return;

        // Re-randomize the per-bone offsets at the configured rate (movements/sec), using real time
        // so slow-motion doesn't slow the shake.
        limbShakeTimer += Time.unscaledDeltaTime;
        float interval = jumpscareLimbShakeFrequency > 0f ? 1f / jumpscareLimbShakeFrequency : 0f;
        if (!limbShakeReady || limbShakeTimer >= interval)
        {
            limbShakeTimer = 0f;
            limbShakeReady = true;
            float a = jumpscareLimbShakeAngle;
            for (int i = 0; i < limbBones.Length; i++)
                limbJitterOffsets[i] = Quaternion.Euler(Random.Range(-a, a), Random.Range(-a, a), Random.Range(-a, a));
        }

        // Layer the held offset on top of this frame's animated pose (no accumulation — the Animator
        // overwrites localRotation again next frame).
        for (int i = 0; i < limbBones.Length; i++)
        {
            if (limbBones[i] != null)
                limbBones[i].localRotation = limbBones[i].localRotation * limbJitterOffsets[i];
        }
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

    /// <summary>
    /// Trigger the jumpscare using a specific kill animation trigger (overrides the configured one).
    /// Pass null/empty to leave whatever animation is currently playing (e.g. a run-in catch).
    /// </summary>
    public void TriggerJumpscare(string animTriggerOverride)
    {
        killAnimationTrigger = animTriggerOverride;
        TriggerJumpscare();
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

        // Override lanterns (turn red, lock them)
        if (overrideLanternsOnJumpscare)
        {
            LanternInteractable.LockAllLanterns(jumpscareLanternColor);
        }

        // Ground the player
        GroundPlayer();

        // Fixed-offset raise BEFORE positioning (ORIGINAL behavior — preserved exactly for every killer
        // except the opt-in face-bone mode below).
        if (playerTransform != null && !raiseCameraToFaceBone && playerHeightOffset != 0f)
        {
            CharacterController controller = playerTransform.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

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

            // Push the killer further from the camera so a long neck doesn't crowd in front of the face.
            if (jumpscareBodyPullback > 0f)
            {
                Vector3 away = transform.position - playerCamera.transform.position; away.y = 0f;
                if (away.sqrMagnitude > 0.0001f) transform.position += away.normalized * jumpscareBodyPullback;
            }

            // Opt-in face-bone mode (tall/giant killers): raise the camera to just below the face bone AFTER
            // positioning, so the framing stays consistent regardless of the killer's height/scale.
            if (raiseCameraToFaceBone && killerFace != null && playerTransform != null)
            {
                CharacterController controller = playerTransform.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;

                float raise = (killerFace.position.y - raiseCameraFaceGap) - playerCamera.transform.position.y;
                if (Mathf.Abs(raise) > 0.001f)
                {
                    Vector3 p = playerTransform.position;
                    p.y += raise;
                    playerTransform.position = p;
                }
            }

            // Make camera look at killer's face
            Vector3 lookTarget = GetKillerFacePosition();
            playerCamera.transform.LookAt(lookTarget);

            // Point flashlight at killer
            PointFlashlightAtKiller();

            // Optional zoom-in on the killer's face (good for tall killers whose face is far away).
            if (jumpscareCameraFOV > 0f) playerCamera.fieldOfView = jumpscareCameraFOV;
            baseJumpscareFOV = playerCamera.fieldOfView;   // base for the zoom-in/zoom-out shake
            jumpscareZoomElapsed = 0f;
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

        // Set up violent limb-shake (jittered in LateUpdate on top of the kill animation).
        if (jumpscareLimbShakeAngle > 0f) SetupLimbShake();

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

        // Start camera shake (skipped in freeze-frame mode, which holds still between flashes).
        if (freezeFramePoses)
        {
            frozenLookTarget = GetKillerFacePosition();
            if (!string.IsNullOrEmpty(finalTargetBoneName)) finalTargetBone = FindChildRecursive(transform, finalTargetBoneName);
        }
        else
        {
            StartCameraShake();
        }

        // Show game over UI
        if (gameOverUI != null)
        {
            StartCoroutine(FadeInUI());
        }

        // Wait for the sequence. The screen-flash path drives its own (finite) timing and ends on a
        // close-up; otherwise use the fixed slow-mo + game-over delay.
        if (screenFlash)
        {
            yield return StartCoroutine(ScreenFlashRoutine());
        }
        else
        {
            yield return new WaitForSecondsRealtime(slowMotionDuration);
            if (gameOverDelay > 0)
            {
                yield return new WaitForSecondsRealtime(gameOverDelay);
            }
        }

        // Tear down jumpscare-only FX (the flash overlay + the looping jumpscare sound) the instant the
        // jumpscare ends, so they don't bleed into the ending reveal.
        EndJumpscareFX();

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

        // Check if there's a wall blocking the killer placement
        Vector3 rayOrigin = playerCamera.transform.position;
        Vector3 bestDirection = cameraForward;

        if (Physics.Raycast(rayOrigin, cameraForward, killStopDistance + 0.3f))
        {
            // Wall in front - find an open direction to turn the player
            bestDirection = FindOpenDirection(rayOrigin, cameraForward);

            // Rotate the player camera to face the new direction
            Quaternion targetRotation = Quaternion.LookRotation(bestDirection);
            playerCamera.transform.rotation = targetRotation;
        }

        Vector3 targetPosition = playerCamera.transform.position + bestDirection * killStopDistance;

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

    private Vector3 FindOpenDirection(Vector3 origin, Vector3 blockedForward)
    {
        float checkDistance = killStopDistance + 0.3f;

        // Try directions in order of preference: behind, left, right, then diagonals
        Vector3[] directionsToTry = new Vector3[]
        {
            -blockedForward,                                                    // Behind (180°)
            Quaternion.Euler(0, 90, 0) * blockedForward,                       // Right (90°)
            Quaternion.Euler(0, -90, 0) * blockedForward,                      // Left (-90°)
            Quaternion.Euler(0, 135, 0) * blockedForward,                      // Back-right (135°)
            Quaternion.Euler(0, -135, 0) * blockedForward,                     // Back-left (-135°)
            Quaternion.Euler(0, 45, 0) * blockedForward,                       // Front-right (45°)
            Quaternion.Euler(0, -45, 0) * blockedForward,                      // Front-left (-45°)
        };

        foreach (Vector3 dir in directionsToTry)
        {
            if (!Physics.Raycast(origin, dir, checkDistance))
            {
                Debug.Log($"KillerJumpscare: Found open direction, rotating player");
                return dir;
            }
        }

        // No open direction found - return behind as last resort
        Debug.LogWarning("KillerJumpscare: No open direction found, defaulting to behind player");
        return -blockedForward;
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
        Vector3 targetPosition = (aimFlashlightAtFaceBone && killerFace != null)
            ? killerFace.position
            : transform.position + Vector3.up * flashlightTargetHeight;
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
            if (vhsEffect != null && disableVHSDuringJumpscare)
            {
                // Clean image — no glitch/RGB double-image on the face. Also stop the sanity controller
                // from re-driving it during the (brief, terminal) jumpscare.
                vhsEffect.enabled = false;
                SanityEffectsController sanity = FindObjectOfType<SanityEffectsController>();
                if (sanity != null) sanity.enabled = false;
                yield break;
            }
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

    // Flashes the screen black->visible flashCount times; on the LAST flash, punches the camera in close
    // to the face (hidden behind the black) and holds. Completing this routine drives the jumpscare end.
    private IEnumerator ScreenFlashRoutine()
    {
        flashOverlay = new GameObject("JumpscareFlashOverlay");
        Canvas canvas = flashOverlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760; // above everything
        Image img = flashOverlay.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // See her normally first, THEN start flashing into wildly different poses.
        img.enabled = false;
        yield return new WaitForSecondsRealtime(flashVisibleDuration);

        int count = Mathf.Max(1, flashCount);
        for (int i = 0; i < count; i++)
        {
            bool last = (i == count - 1);

            img.enabled = true; // black — change the pose / camera behind it
            if (last)
            {
                neckFrozenRot = Quaternion.identity;   // straighten for the clean close-up
                PunchCameraToFace();                   // zooms in on finalTargetBone (e.g. the neck)
            }
            else if (freezeFramePoses)
            {
                neckFrozenRot = Quaternion.Euler(
                    Random.Range(-neckContortRange, neckContortRange),
                    Random.Range(-neckContortRange, neckContortRange),
                    Random.Range(-neckContortRange, neckContortRange));
            }
            yield return new WaitForSecondsRealtime(flashBlackDuration);
            if (flashOverlay == null) yield break;

            img.enabled = false; // visible (frozen pose / final close-up)
            yield return new WaitForSecondsRealtime(last ? finalCloseupHold : flashVisibleDuration);
        }
    }

    // Tears down the jumpscare-only FX so nothing bleeds into the ending reveal.
    private void EndJumpscareFX()
    {
        if (flashOverlay != null) { Destroy(flashOverlay); flashOverlay = null; }

        if (!string.IsNullOrEmpty(cutSoundObjectName))
        {
            GameObject snd = GameObject.Find(cutSoundObjectName);
            if (snd != null) Destroy(snd);
        }
    }

    // Snaps the camera to finalCloseupDistance from the face along the current view direction (the LookAt
    // each frame keeps it aimed). Used for the final-flash punch-in.
    private void PunchCameraToFace()
    {
        if (playerCamera == null || playerTransform == null || finalCloseupDistance <= 0f) return;
        Vector3 target = (finalTargetBone != null) ? finalTargetBone.position : GetKillerFacePosition();
        target += Vector3.up * finalCloseupAimUp; // raise to the face center so the FULL face is framed
        Vector3 camPos = playerCamera.transform.position;
        Vector3 dir = camPos - target;
        // Flatten to horizontal so the close-up is FACE-ON (level with the face), not looking up at it from
        // below (which framed the chin/neck for the tall killer).
        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        if (flat.sqrMagnitude > 0.0001f) dir = flat;
        if (dir.sqrMagnitude < 0.0001f) return;
        Vector3 newCamPos = target + dir.normalized * finalCloseupDistance;
        playerTransform.position += (newCamPos - camPos);
        if (freezeFramePoses) frozenLookTarget = target; // frame the close-up on the face
        if (finalCloseupFOV > 0f)
        {
            playerCamera.fieldOfView = finalCloseupFOV;
            baseJumpscareFOV = finalCloseupFOV;
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

        // Endings: unlock it, show the reveal screen, then return to the main menu.
        if (unlocksEnding)
        {
            // Silence anything still playing so it doesn't bleed into the silent reveal.
            if (audioSource != null)
            {
                audioSource.Stop();
            }

            EndingFlow.Trigger(endingToUnlock);
            return;
        }

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
