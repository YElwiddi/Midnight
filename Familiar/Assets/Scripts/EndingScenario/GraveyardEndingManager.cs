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

    [Header("Escort NPC Event")]
    [Tooltip("GameEvent asset for the escort NPC")]
    public GameEvent escortNPCEvent;
    [Tooltip("Delay before spawning escort NPC")]
    public float escortNPCDelay = 2f;

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
    private AudioSource audioSource;

    void Start()
    {
        // Subscribe to correct grave event
        DirtPileInteractable.OnCorrectGraveExhumed += OnCorrectGraveExhumed;

        // Cache original fog density
        originalFogDensity = RenderSettings.fogDensity;

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

        // Wait for fog transition and dialogue
        yield return new WaitForSeconds(Mathf.Max(fogTransitionDuration, endingDialogueDuration));

        // Spawn escort NPC after delay
        yield return new WaitForSeconds(escortNPCDelay);
        SpawnEscortNPC();
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

    private void SpawnEscortNPC()
    {
        if (escortNPCEvent == null)
        {
            Debug.LogWarning("GraveyardEndingManager: No escort NPC event assigned!");
            return;
        }

        // Find the GameFlowManager to trigger the event
        GameFlowManager flowManager = FindObjectOfType<GameFlowManager>();
        if (flowManager != null)
        {
            flowManager.TriggerEvent(escortNPCEvent);
            Debug.Log("GraveyardEndingManager: Escort NPC event triggered");
        }
        else
        {
            Debug.LogWarning("GraveyardEndingManager: No GameFlowManager found to trigger escort event!");
        }
    }

    /// <summary>
    /// Reset the ending state (useful for testing).
    /// </summary>
    public void ResetEnding()
    {
        endingTriggered = false;
        RenderSettings.fogDensity = originalFogDensity;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GraveyardEndingTriggered = false;
            GameManager.Instance.CorrectDigCount = 0;
        }

        Debug.Log("GraveyardEndingManager: Ending state reset");
    }
}
