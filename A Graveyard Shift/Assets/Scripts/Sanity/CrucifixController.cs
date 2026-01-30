using System;
using UnityEngine;

/// <summary>
/// State of the crucifix.
/// </summary>
public enum CrucifixState
{
    AffixedOnDoor,      // Properly affixed to door
    FallenOnFloor,      // Has fallen and needs to be re-affixed
    HeldByPlayer        // Player is currently holding it (optional state)
}

/// <summary>
/// Controls the crucifix that protects the cabin.
/// Handles affixed/fallen state, visual flipping at low sanity, and player interaction.
/// </summary>
public class CrucifixController : MonoBehaviour, IInteractable
{
    #region Singleton
    public static CrucifixController Instance { get; private set; }
    #endregion

    #region Inspector Settings
    [Header("Crucifix State")]
    [Tooltip("Current state of the crucifix")]
    [SerializeField] private CrucifixState currentState = CrucifixState.AffixedOnDoor;

    [Tooltip("Is the crucifix visually flipped upside down?")]
    [SerializeField] private bool isFlipped = false;

    [Header("Position References")]
    [Tooltip("Transform where crucifix sits when affixed to door")]
    [SerializeField] private Transform doorPosition;

    [Tooltip("Transform where crucifix sits when fallen on floor")]
    [SerializeField] private Transform floorPosition;

    [Header("Rotation Settings")]
    [Tooltip("Normal rotation (right-side up)")]
    [SerializeField] private Vector3 normalRotation = Vector3.zero;

    [Tooltip("Flipped rotation (upside down)")]
    [SerializeField] private Vector3 flippedRotation = new Vector3(0f, 0f, 180f);

    [Tooltip("Speed of flip animation")]
    [SerializeField] private float flipSpeed = 2f;

    [Header("Fall Settings")]
    [Tooltip("If true, crucifix falls with physics. If false, teleports to floor position.")]
    [SerializeField] private bool usePhysicsForFall = false;

    [Tooltip("Sound when crucifix falls")]
    [SerializeField] private AudioClip fallSound;

    [Tooltip("Sound when crucifix is re-affixed")]
    [SerializeField] private AudioClip affixSound;

    [Tooltip("Volume for crucifix sounds")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.8f;

    [Header("Interaction")]
    [Tooltip("Interaction prompt text when crucifix is on floor")]
    [SerializeField] private string interactionPrompt = "Press E to pick up crucifix";

    [Tooltip("If true, player must be looking at floor crucifix to interact")]
    [SerializeField] private bool requireLookingToInteract = true;

    [Tooltip("Maximum interaction distance")]
    [SerializeField] private float interactionDistance = 2f;

    [Header("Visual Shake (for flipping)")]
    [Tooltip("If true, crucifix shakes before flipping")]
    [SerializeField] private bool shakeBeforeFlip = true;

    [Tooltip("Duration of shake before flip")]
    [SerializeField] private float shakeDuration = 0.5f;

    [Tooltip("Intensity of shake")]
    [SerializeField] private float shakeIntensity = 0.1f;
    #endregion

    #region Events
    /// <summary>Fired when crucifix state changes.</summary>
    public event Action<CrucifixState> OnStateChanged;

    /// <summary>Fired when crucifix is flipped/unflipped.</summary>
    public event Action<bool> OnFlippedChanged;

    /// <summary>Fired when crucifix falls from door.</summary>
    public event Action OnCrucifixFell;

    /// <summary>Fired when crucifix is re-affixed.</summary>
    public event Action OnCrucifixAffixed;
    #endregion

    #region Properties
    /// <summary>Is the crucifix properly affixed?</summary>
    public bool IsAffixed => currentState == CrucifixState.AffixedOnDoor;

    /// <summary>Is the crucifix on the floor?</summary>
    public bool IsOnFloor => currentState == CrucifixState.FallenOnFloor;

    /// <summary>Is the crucifix visually flipped upside down?</summary>
    public bool IsFlipped => isFlipped;

    /// <summary>Current crucifix state.</summary>
    public CrucifixState State => currentState;
    #endregion

    #region Private Fields
    private AudioSource audioSource;
    private Quaternion targetRotation;
    private bool isAnimatingFlip = false;
    private bool isShaking = false;
    private Vector3 originalLocalPosition;
    private Rigidbody rigidBody;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }

        // Get rigidbody if using physics
        rigidBody = GetComponent<Rigidbody>();

        originalLocalPosition = transform.localPosition;
        targetRotation = transform.rotation;
    }

    private void Start()
    {
        // Set initial position based on state
        ApplyStatePosition();
        ApplyFlipRotation(false);
    }

    private void Update()
    {
        // Animate flip rotation
        if (isAnimatingFlip)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, flipSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, targetRotation) < 0.5f)
            {
                transform.rotation = targetRotation;
                isAnimatingFlip = false;
            }
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Make the crucifix fall from the door to the floor.
    /// </summary>
    public void Fall()
    {
        if (currentState != CrucifixState.AffixedOnDoor) return;

        Debug.Log("CrucifixController: Crucifix falling from door!");

        currentState = CrucifixState.FallenOnFloor;

        if (usePhysicsForFall && rigidBody != null)
        {
            // Enable physics and let it fall
            rigidBody.isKinematic = false;
            rigidBody.useGravity = true;
        }
        else
        {
            // Teleport to floor position
            ApplyStatePosition();
        }

        // Play fall sound
        if (fallSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fallSound, soundVolume);
        }

        OnCrucifixFell?.Invoke();
        OnStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// Re-affix the crucifix to the door.
    /// </summary>
    public void Affix()
    {
        if (currentState == CrucifixState.AffixedOnDoor) return;

        Debug.Log("CrucifixController: Crucifix re-affixed to door!");

        currentState = CrucifixState.AffixedOnDoor;

        // Reset flipped state when re-affixing
        isFlipped = false;

        // Disable physics if used
        if (rigidBody != null)
        {
            rigidBody.isKinematic = true;
            rigidBody.useGravity = false;
        }

        // Apply position and rotation with normalRotation offset
        ApplyStatePosition();
        ApplyFlipRotation(false); // Apply normalRotation immediately (not animated)

        // Play affix sound
        if (affixSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(affixSound, soundVolume);
        }

        OnCrucifixAffixed?.Invoke();
        OnStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// Set the flipped state (upside down or right-side up).
    /// Called by SanityEffectsController at sanity 50.
    /// </summary>
    public void SetFlipped(bool flipped)
    {
        if (isFlipped == flipped) return;

        Debug.Log($"CrucifixController: Setting flipped = {flipped}");

        if (shakeBeforeFlip && !isFlipped && flipped)
        {
            // Shake before flipping upside down
            StartCoroutine(ShakeThenFlip(flipped));
        }
        else
        {
            ApplyFlip(flipped);
        }
    }

    /// <summary>
    /// IInteractable implementation - called by the interaction system.
    /// Only allows interaction when crucifix is on the floor.
    /// </summary>
    public void Interact()
    {
        if (currentState == CrucifixState.FallenOnFloor)
        {
            Affix();
        }
    }

    /// <summary>
    /// Called when player interacts with the crucifix (when on floor).
    /// </summary>
    public void OnPlayerInteract()
    {
        Interact();
    }

    /// <summary>
    /// Check if player can interact with crucifix (is on floor and in range).
    /// </summary>
    public bool CanPlayerInteract(Transform playerTransform, Camera playerCamera)
    {
        if (currentState != CrucifixState.FallenOnFloor) return false;

        if (playerTransform == null) return false;

        // Check distance
        float distance = Vector3.Distance(playerTransform.position, transform.position);
        if (distance > interactionDistance) return false;

        // Check if looking at crucifix
        if (requireLookingToInteract && playerCamera != null)
        {
            Vector3 dirToCrucifix = (transform.position - playerCamera.transform.position).normalized;
            float angle = Vector3.Angle(playerCamera.transform.forward, dirToCrucifix);
            if (angle > 45f) return false;
        }

        return true;
    }

    /// <summary>
    /// Get the interaction prompt text.
    /// Only shows prompt when crucifix is on the floor.
    /// </summary>
    public string GetInteractionPrompt()
    {
        if (currentState == CrucifixState.FallenOnFloor)
        {
            return interactionPrompt;
        }
        return ""; // No prompt when affixed
    }
    #endregion

    #region Private Methods
    private void ApplyStatePosition()
    {
        Transform targetTransform = currentState == CrucifixState.AffixedOnDoor ? doorPosition : floorPosition;

        if (targetTransform != null)
        {
            transform.position = targetTransform.position;

            // Only apply rotation from position if not flipped
            if (!isFlipped)
            {
                transform.rotation = targetTransform.rotation;
                targetRotation = transform.rotation;
            }
        }
    }

    private void ApplyFlipRotation(bool animate)
    {
        Quaternion baseRotation = Quaternion.identity;

        // Get base rotation from current position
        Transform posTransform = currentState == CrucifixState.AffixedOnDoor ? doorPosition : floorPosition;
        if (posTransform != null)
        {
            baseRotation = posTransform.rotation;
        }

        // Apply flip offset
        Vector3 flipOffset = isFlipped ? flippedRotation : normalRotation;
        targetRotation = baseRotation * Quaternion.Euler(flipOffset);

        if (animate)
        {
            isAnimatingFlip = true;
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }

    private void ApplyFlip(bool flipped)
    {
        isFlipped = flipped;
        ApplyFlipRotation(true);
        OnFlippedChanged?.Invoke(isFlipped);
    }

    private System.Collections.IEnumerator ShakeThenFlip(bool flipped)
    {
        isShaking = true;
        float elapsed = 0f;
        Vector3 originalPos = transform.localPosition;

        while (elapsed < shakeDuration)
        {
            float x = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);
            float y = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);
            transform.localPosition = originalPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;
        isShaking = false;

        ApplyFlip(flipped);
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        // Draw door position
        if (doorPosition != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(doorPosition.position, 0.2f);
            Gizmos.DrawLine(doorPosition.position, doorPosition.position + doorPosition.forward * 0.5f);
        }

        // Draw floor position
        if (floorPosition != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(floorPosition.position, 0.2f);
        }

        // Draw interaction range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
    #endregion
}
