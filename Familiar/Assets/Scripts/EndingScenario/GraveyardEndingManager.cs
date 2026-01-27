using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Manages the graveyard ending sequence when the player exhumes 3 correct graves.
/// - Despawns the killer
/// - Unlocks the ladder
/// - Reduces fog
/// - Triggers an NPC escort event
/// </summary>
public class GraveyardEndingManager : MonoBehaviour
{
    [Header("Ending Trigger")]
    [Tooltip("Number of correct graves needed to trigger the ending")]
    public int correctGravesRequired = 3;

    [Header("Killer")]
    [Tooltip("Reference to the killer (if not set, will find CryptKiller in scene)")]
    public GameObject killerObject;
    [Tooltip("Fade out the killer over this duration (0 = instant disappear)")]
    public float killerFadeDuration = 2f;

    [Header("Ladder")]
    [Tooltip("The LockedTeleportInteractable should have requiredBoolFlag set to 'graveyardendtriggered'. It will automatically unlock when the ending triggers.")]
    [SerializeField] private string ladderUnlockNote = "Set ladder's requiredBoolFlag to 'graveyardendtriggered'";

    [Header("Fog Reduction")]
    [Tooltip("Enable fog reduction on ending")]
    public bool reduceFog = true;
    [Tooltip("Target fog density after ending (0 = no fog)")]
    public float targetFogDensity = 0.02f;
    [Tooltip("Time to transition fog")]
    public float fogTransitionDuration = 3f;

    [Header("Skybox")]
    [Tooltip("New skybox material to use when ending triggers (leave empty to keep current)")]
    public Material endingSkybox;
    [Tooltip("Time to blend to new skybox (0 = instant)")]
    public float skyboxTransitionDuration = 2f;

    [Header("Escort NPC Event (Triggered via Ladder)")]
    [Tooltip("The escort NPC is now triggered by the ladder's OnTeleportComplete event, not here. Configure the ladder's 'Event To Trigger After Teleport' field instead.")]
    [SerializeField] private string escortNPCNote = "Configure on ladder interactable";

    [Header("Audio")]
    [Tooltip("Sound to play when ending triggers")]
    public AudioClip endingTriggerSound;
    [Range(0f, 1f)]
    public float endingSoundVolume = 1f;

    [Header("Dialogue")]
    [Tooltip("Dialogue to show when ending triggers")]
    [TextArea(2, 4)]
    public string endingDialogue = "The spirits have been appeased...";
    public float endingDialogueDuration = 4f;

    // Private state
    private bool endingTriggered = false;
    private float originalFogDensity;
    private Material originalSkybox;
    private AudioSource audioSource;

    void Start()
    {
        // Subscribe to correct grave event
        DirtPileInteractable.OnCorrectGraveExhumed += OnCorrectGraveExhumed;

        // Cache original fog density and skybox
        originalFogDensity = RenderSettings.fogDensity;
        originalSkybox = RenderSettings.skybox;

        // Get or create audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void OnDestroy()
    {
        DirtPileInteractable.OnCorrectGraveExhumed -= OnCorrectGraveExhumed;
    }

    private void OnCorrectGraveExhumed(int correctCount)
    {
        if (endingTriggered) return;

        Debug.Log($"GraveyardEndingManager: {correctCount}/{correctGravesRequired} correct graves exhumed");

        if (correctCount >= correctGravesRequired)
        {
            TriggerEnding();
        }
    }

    /// <summary>
    /// Manually trigger the ending sequence.
    /// </summary>
    public void TriggerEnding()
    {
        if (endingTriggered) return;
        endingTriggered = true;

        // Set flag in GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GraveyardEndingTriggered = true;
        }

        Debug.Log("GraveyardEndingManager: Triggering graveyard ending sequence!");

        StartCoroutine(EndingSequence());
    }

    private IEnumerator EndingSequence()
    {
        // Play ending sound
        if (endingTriggerSound != null)
        {
            audioSource.PlayOneShot(endingTriggerSound, endingSoundVolume);
        }

        // Show ending dialogue
        if (!string.IsNullOrEmpty(endingDialogue))
        {
            SimpleDialogueTrigger.ShowDialogue(endingDialogue, "", endingDialogueDuration, 30f);
        }

        // Wait a moment for dramatic effect
        yield return new WaitForSeconds(1f);

        // Despawn killer
        StartCoroutine(DespawnKiller());

        // Note: Ladder automatically unlocks because we set GraveyardEndingTriggered = true
        // The LockedTeleportInteractable checks this flag via requiredBoolFlag = "graveyardendtriggered"
        Debug.Log("GraveyardEndingManager: Ladder unlocked via GraveyardEndingTriggered flag");

        // Reduce fog
        if (reduceFog)
        {
            StartCoroutine(ReduceFogOverTime());
        }

        // Change skybox
        if (endingSkybox != null)
        {
            StartCoroutine(TransitionSkybox());
        }

        // The escort NPC event is now triggered by the ladder's OnTeleportComplete event
        // when the player exits the crypt, not here
        Debug.Log("GraveyardEndingManager: Ending sequence complete. Player can now use the ladder to exit.");
    }

    private IEnumerator DespawnKiller()
    {
        // Find killer if not assigned
        if (killerObject == null)
        {
            CryptKiller killer = FindObjectOfType<CryptKiller>();
            if (killer != null)
            {
                killerObject = killer.gameObject;
            }
        }

        if (killerObject == null)
        {
            Debug.Log("GraveyardEndingManager: No killer found to despawn");
            yield break;
        }

        Debug.Log("GraveyardEndingManager: Despawning killer");

        if (killerFadeDuration > 0)
        {
            // Fade out the killer
            Renderer[] renderers = killerObject.GetComponentsInChildren<Renderer>();
            float elapsed = 0f;

            // Stop killer movement
            var navAgent = killerObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.isStopped = true;
            }

            // Disable killer AI
            var cryptKiller = killerObject.GetComponent<CryptKiller>();
            if (cryptKiller != null)
            {
                cryptKiller.enabled = false;
            }

            while (elapsed < killerFadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / killerFadeDuration);

                foreach (Renderer rend in renderers)
                {
                    foreach (Material mat in rend.materials)
                    {
                        if (mat.HasProperty("_Color"))
                        {
                            Color color = mat.color;
                            color.a = alpha;
                            mat.color = color;
                        }
                    }
                }

                yield return null;
            }
        }

        // Destroy the killer
        Destroy(killerObject);
        Debug.Log("GraveyardEndingManager: Killer destroyed");
    }

    private IEnumerator ReduceFogOverTime()
    {
        if (!RenderSettings.fog)
        {
            Debug.Log("GraveyardEndingManager: Fog is not enabled in render settings");
            yield break;
        }

        float startDensity = RenderSettings.fogDensity;
        float elapsed = 0f;

        Debug.Log($"GraveyardEndingManager: Reducing fog from {startDensity} to {targetFogDensity}");

        while (elapsed < fogTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fogTransitionDuration;
            RenderSettings.fogDensity = Mathf.Lerp(startDensity, targetFogDensity, t);
            yield return null;
        }

        RenderSettings.fogDensity = targetFogDensity;
        Debug.Log("GraveyardEndingManager: Fog reduction complete");
    }

    private IEnumerator TransitionSkybox()
    {
        Debug.Log("GraveyardEndingManager: Transitioning skybox");

        if (skyboxTransitionDuration <= 0)
        {
            // Instant swap
            RenderSettings.skybox = endingSkybox;
            DynamicGI.UpdateEnvironment();
            Debug.Log("GraveyardEndingManager: Skybox changed instantly");
            yield break;
        }

        // Gradual transition using exposure (if available)
        // IMPORTANT: Create material instances to avoid modifying the original assets
        float elapsed = 0f;
        float halfDuration = skyboxTransitionDuration / 2f;

        // Create instance of current skybox to avoid modifying the asset
        Material currentSkybox = RenderSettings.skybox;
        bool hasExposure = currentSkybox != null && currentSkybox.HasProperty("_Exposure");
        float originalExposure = hasExposure ? currentSkybox.GetFloat("_Exposure") : 1f;

        if (hasExposure)
        {
            // Create instance so we don't modify the original asset
            Material currentInstance = new Material(currentSkybox);
            RenderSettings.skybox = currentInstance;

            // Fade out current skybox
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                currentInstance.SetFloat("_Exposure", Mathf.Lerp(originalExposure, 0f, t));
                yield return null;
            }

            // Destroy the temporary instance
            Destroy(currentInstance);
        }
        else
        {
            yield return new WaitForSeconds(halfDuration);
        }

        // Swap to new skybox
        bool newHasExposure = endingSkybox != null && endingSkybox.HasProperty("_Exposure");
        float targetExposure = newHasExposure ? endingSkybox.GetFloat("_Exposure") : 1f;

        if (newHasExposure)
        {
            // Create instance of new skybox for fade-in
            Material newInstance = new Material(endingSkybox);
            newInstance.SetFloat("_Exposure", 0f);
            RenderSettings.skybox = newInstance;
            DynamicGI.UpdateEnvironment();

            elapsed = 0f;

            // Fade in new skybox
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                newInstance.SetFloat("_Exposure", Mathf.Lerp(0f, targetExposure, t));
                yield return null;
            }

            newInstance.SetFloat("_Exposure", targetExposure);
            // Keep the instance as the active skybox (it will be cleaned up on scene unload)
        }
        else
        {
            RenderSettings.skybox = endingSkybox;
            DynamicGI.UpdateEnvironment();
        }

        Debug.Log("GraveyardEndingManager: Skybox transition complete");
    }

    /// <summary>
    /// Reset the ending state (useful for testing).
    /// </summary>
    public void ResetEnding()
    {
        endingTriggered = false;
        RenderSettings.fogDensity = originalFogDensity;

        if (originalSkybox != null)
        {
            RenderSettings.skybox = originalSkybox;
            DynamicGI.UpdateEnvironment();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GraveyardEndingTriggered = false;
            GameManager.Instance.CorrectDigCount = 0;
        }

        Debug.Log("GraveyardEndingManager: Ending state reset");
    }
}
