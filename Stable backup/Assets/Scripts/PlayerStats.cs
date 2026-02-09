using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Serializable]
    public class JournalEntry
    {
        public string id;
        public string title;
        public string description;
        public bool isRead = false;
        public DateTime timeAdded;
    }

    // Primary Stats
    [Header("Primary Stats")]
    [SerializeField] [Range(0, 100)] private float health = 100f;
    [SerializeField] [Range(0, 100)] private float sanity = 100f;
    [SerializeField] [Range(0, 100)] private float stamina = 100f;
    
    // Mental States
    [Header("Mental States")]
    [SerializeField] [Range(0, 100)] private float fear = 0f;
    [SerializeField] [Range(0, 100)] private float trust = 50f;
    [SerializeField] [Range(0, 100)] private float suspicion = 0f;
    
    // Knowledge
    [Header("Knowledge")]
    [SerializeField] private List<JournalEntry> journalEntries = new List<JournalEntry>();
    [SerializeField] private List<string> discoveredLocations = new List<string>();
    [SerializeField] private List<string> discoveredSecrets = new List<string>();
    
    // Stat Modifiers
    [Header("Stat Modifiers")]
    [SerializeField] private float fearHealthPenalty = 0.1f; // Health penalty per fear level when > 50
    [SerializeField] private float fearSanityPenalty = 0.2f; // Sanity penalty per fear level when > 50
    [SerializeField] private float staminaRecoveryRate = 5f; // Stamina recovery per second
    [SerializeField] private bool isStaminaRecovering = true;
    
    // Events
    // Cannot use Header attribute here because it's not a field declaration
    public event Action<float> OnHealthChanged;
    public event Action<float> OnSanityChanged;
    public event Action<float> OnStaminaChanged;
    public event Action<float> OnFearChanged;
    public event Action<float> OnTrustChanged;
    public event Action<float> OnSuspicionChanged;
    public event Action<JournalEntry> OnJournalEntryAdded;
    public event Action<string> OnLocationDiscovered;
    public event Action<string> OnSecretDiscovered;
    
    // UI References
    [Header("UI References")]
    [SerializeField] private GameObject fearWarningUI;
    [SerializeField] private GameObject lowHealthWarningUI;
    [SerializeField] private GameObject lowSanityWarningUI;
    
    // Cache the player AudioSource for playing stat change sounds
    private AudioSource audioSource;
    
    [Header("Audio")]
    [SerializeField] private AudioClip healthDownSound;
    [SerializeField] private AudioClip healthUpSound;
    [SerializeField] private AudioClip fearIncreaseSound;
    [SerializeField] private AudioClip journalUpdateSound;
    [SerializeField] private AudioClip heartbeatSound;
    
    // Heartbeat effect
    private bool isHeartbeatPlaying = false;
    private Coroutine heartbeatCoroutine;
    
    // Public properties
    public float Health => health;
    public float Sanity => sanity;
    public float Stamina => stamina;
    public float Fear => fear;
    public float Trust => trust;
    public float Suspicion => suspicion;
    public List<JournalEntry> JournalEntries => journalEntries;
    public List<string> DiscoveredLocations => discoveredLocations;
    public List<string> DiscoveredSecrets => discoveredSecrets;
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Initialize UI elements
        if (fearWarningUI != null) fearWarningUI.SetActive(false);
        if (lowHealthWarningUI != null) lowHealthWarningUI.SetActive(false);
        if (lowSanityWarningUI != null) lowSanityWarningUI.SetActive(false);
    }
    
    private void Start()
    {
        // Initial stat check for UI
        UpdateStatWarnings();
    }
    
    private void Update()
    {
        // Recover stamina over time if allowed
        if (isStaminaRecovering && stamina < 100f)
        {
            ModifyStamina(staminaRecoveryRate * Time.deltaTime);
        }
        
        // Apply fear effects over time
        if (fear > 50f)
        {
            float fearFactor = (fear - 50f) / 50f; // 0 to 1 based on fear from 50-100
            
            // Apply small health/sanity drain over time based on fear level
            float healthPenalty = fearHealthPenalty * fearFactor * Time.deltaTime;
            float sanityPenalty = fearSanityPenalty * fearFactor * Time.deltaTime;
            
            if (healthPenalty > 0)
                ModifyHealth(-healthPenalty, false);
                
            if (sanityPenalty > 0)
                ModifySanity(-sanityPenalty, false);
        }
    }
    
    #region Public Stat Modification Methods
    
    /// <summary>
    /// Modify the player's health
    /// </summary>
    /// <param name="amount">Amount to change (positive = heal, negative = damage)</param>
    /// <param name="playSound">Whether to play a sound effect</param>
    public void ModifyHealth(float amount, bool playSound = true)
    {
        float previousHealth = health;
        health += amount;
        health = Mathf.Clamp(health, 0f, 100f);
        
        if (health != previousHealth)
        {
            OnHealthChanged?.Invoke(health);
            
            if (playSound && audioSource != null)
            {
                // Play appropriate sound
                if (amount > 0 && healthUpSound != null)
                {
                    audioSource.PlayOneShot(healthUpSound);
                }
                else if (amount < 0 && healthDownSound != null)
                {
                    audioSource.PlayOneShot(healthDownSound);
                }
            }
            
            UpdateStatWarnings();
            
            // Check for death
            if (health <= 0f)
            {
                OnPlayerDeath();
            }
        }
    }
    
    /// <summary>
    /// Modify the player's sanity
    /// </summary>
    /// <param name="amount">Amount to change (positive = increase, negative = decrease)</param>
    /// <param name="playSound">Whether to play a sound effect</param>
    public void ModifySanity(float amount, bool playSound = true)
    {
        float previousSanity = sanity;
        sanity += amount;
        sanity = Mathf.Clamp(sanity, 0f, 100f);
        
        if (sanity != previousSanity)
        {
            OnSanityChanged?.Invoke(sanity);
            
            // You could add specific sound effects for sanity changes here
            
            UpdateStatWarnings();
        }
    }
    
    /// <summary>
    /// Modify the player's stamina
    /// </summary>
    /// <param name="amount">Amount to change (positive = increase, negative = decrease)</param>
    public void ModifyStamina(float amount)
    {
        float previousStamina = stamina;
        stamina += amount;
        stamina = Mathf.Clamp(stamina, 0f, 100f);
        
        if (stamina != previousStamina)
        {
            OnStaminaChanged?.Invoke(stamina);
        }
    }
    
    /// <summary>
    /// Modify the player's fear level
    /// </summary>
    /// <param name="amount">Amount to change (positive = increase fear, negative = decrease fear)</param>
    public void ModifyFear(float amount)
    {
        float previousFear = fear;
        fear += amount;
        fear = Mathf.Clamp(fear, 0f, 100f);
        
        if (fear != previousFear)
        {
            OnFearChanged?.Invoke(fear);
            
            if (amount > 0 && audioSource != null && fearIncreaseSound != null)
            {
                audioSource.PlayOneShot(fearIncreaseSound);
            }
            
            UpdateStatWarnings();
            
            // Handle heartbeat effect
            ManageHeartbeatEffect();
        }
    }
    
    /// <summary>
    /// Modify the player's trust towards NPCs
    /// </summary>
    /// <param name="amount">Amount to change (positive = increase trust, negative = decrease trust)</param>
    public void ModifyTrust(float amount)
    {
        float previousTrust = trust;
        trust += amount;
        trust = Mathf.Clamp(trust, 0f, 100f);
        
        if (trust != previousTrust)
        {
            OnTrustChanged?.Invoke(trust);
            
            // Could add a UI element to show trust changes
            Debug.Log($"Trust changed by {amount:F1}. New value: {trust:F1}");
        }
    }
    
    /// <summary>
    /// Modify the player's suspicion level
    /// </summary>
    /// <param name="amount">Amount to change (positive = increase suspicion, negative = decrease suspicion)</param>
    public void ModifySuspicion(float amount)
    {
        float previousSuspicion = suspicion;
        suspicion += amount;
        suspicion = Mathf.Clamp(suspicion, 0f, 100f);
        
        if (suspicion != previousSuspicion)
        {
            OnSuspicionChanged?.Invoke(suspicion);
            
            // Could add a UI element to show suspicion changes
            Debug.Log($"Suspicion changed by {amount:F1}. New value: {suspicion:F1}");
        }
    }
    
    #endregion
    
    #region Journal and Knowledge Methods
    
    /// <summary>
    /// Add an entry to the player's journal
    /// </summary>
    /// <param name="entryId">Unique identifier for the entry</param>
    /// <param name="title">Title of the journal entry</param>
    /// <param name="description">Description/content of the journal entry</param>
    public void AddJournalEntry(string entryId, string title = "", string description = "")
    {
        // Check if entry already exists
        if (journalEntries.Exists(entry => entry.id == entryId))
        {
            Debug.Log($"Journal entry '{entryId}' already exists!");
            return;
        }
        
        // Create new entry
        JournalEntry newEntry = new JournalEntry
        {
            id = entryId,
            title = string.IsNullOrEmpty(title) ? entryId : title,
            description = description,
            isRead = false,
            timeAdded = DateTime.Now
        };
        
        // Add to journal
        journalEntries.Add(newEntry);
        
        // Trigger event
        OnJournalEntryAdded?.Invoke(newEntry);
        
        // Play sound effect
        if (audioSource != null && journalUpdateSound != null)
        {
            audioSource.PlayOneShot(journalUpdateSound);
        }
        
        Debug.Log($"Added journal entry: {newEntry.title}");
    }
    
    /// <summary>
    /// Simplified version that just takes an ID
    /// </summary>
    public void AddJournalEntry(string entryId)
    {
        AddJournalEntry(entryId, entryId, "");
    }
    
    /// <summary>
    /// Mark a journal entry as read
    /// </summary>
    /// <param name="entryId">ID of the entry to mark</param>
    public void MarkJournalEntryAsRead(string entryId)
    {
        JournalEntry entry = journalEntries.Find(e => e.id == entryId);
        if (entry != null)
        {
            entry.isRead = true;
        }
    }
    
    /// <summary>
    /// Check if player has a specific journal entry
    /// </summary>
    public bool HasJournalEntry(string entryId)
    {
        return journalEntries.Exists(entry => entry.id == entryId);
    }
    
    /// <summary>
    /// Add a location to the discovered locations list
    /// </summary>
    public void DiscoverLocation(string locationId)
    {
        if (!discoveredLocations.Contains(locationId))
        {
            discoveredLocations.Add(locationId);
            OnLocationDiscovered?.Invoke(locationId);
            Debug.Log($"Discovered location: {locationId}");
        }
    }
    
    /// <summary>
    /// Add a secret to the discovered secrets list
    /// </summary>
    public void DiscoverSecret(string secretId)
    {
        if (!discoveredSecrets.Contains(secretId))
        {
            discoveredSecrets.Add(secretId);
            OnSecretDiscovered?.Invoke(secretId);
            Debug.Log($"Discovered secret: {secretId}");
        }
    }
    
    /// <summary>
    /// Check if player has discovered a location
    /// </summary>
    public bool HasDiscoveredLocation(string locationId)
    {
        return discoveredLocations.Contains(locationId);
    }
    
    /// <summary>
    /// Check if player has discovered a secret
    /// </summary>
    public bool HasDiscoveredSecret(string secretId)
    {
        return discoveredSecrets.Contains(secretId);
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Update UI warnings based on current stats
    /// </summary>
    private void UpdateStatWarnings()
    {
        // Fear warning
        if (fearWarningUI != null)
        {
            fearWarningUI.SetActive(fear >= 75f);
        }
        
        // Health warning
        if (lowHealthWarningUI != null)
        {
            lowHealthWarningUI.SetActive(health <= 25f);
        }
        
        // Sanity warning
        if (lowSanityWarningUI != null)
        {
            lowSanityWarningUI.SetActive(sanity <= 25f);
        }
    }
    
    /// <summary>
    /// Manages the heartbeat effect based on fear level
    /// </summary>
    private void ManageHeartbeatEffect()
    {
        bool shouldPlayHeartbeat = fear >= 80f || health <= 20f;
        
        // Start heartbeat if not already playing and should be
        if (shouldPlayHeartbeat && !isHeartbeatPlaying && heartbeatSound != null)
        {
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
            }
            
            heartbeatCoroutine = StartCoroutine(PlayHeartbeatEffect());
        }
        // Stop heartbeat if playing and shouldn't be
        else if (!shouldPlayHeartbeat && isHeartbeatPlaying)
        {
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = null;
                isHeartbeatPlaying = false;
            }
        }
    }
    
    /// <summary>
    /// Coroutine for playing the heartbeat effect
    /// </summary>
    private IEnumerator PlayHeartbeatEffect()
    {
        isHeartbeatPlaying = true;
        
        while (fear >= 80f || health <= 20f)
        {
            if (audioSource != null && heartbeatSound != null)
            {
                audioSource.PlayOneShot(heartbeatSound);
                
                // Calculate heartbeat interval based on fear level (faster when more afraid)
                float fearLevel = Mathf.Max(fear / 100f, (100f - health) / 100f);
                float interval = Mathf.Lerp(1.2f, 0.6f, fearLevel);
                
                yield return new WaitForSeconds(interval);
            }
            else
            {
                yield return null;
            }
        }
        
        isHeartbeatPlaying = false;
        heartbeatCoroutine = null;
    }
    
    /// <summary>
    /// Called when player's health reaches zero
    /// </summary>
    private void OnPlayerDeath()
    {
        Debug.Log("Player has died!");
        
        // Implement death logic here
        // e.g., trigger death animation, show game over screen, etc.
    }
    
    /// <summary>
    /// Disable stamina recovery (e.g., when running)
    /// </summary>
    public void DisableStaminaRecovery()
    {
        isStaminaRecovering = false;
    }
    
    /// <summary>
    /// Enable stamina recovery
    /// </summary>
    public void EnableStaminaRecovery()
    {
        isStaminaRecovering = true;
    }
    
    #endregion
    
    #region Save/Load Methods
    
    /// <summary>
    /// Get a dictionary of player stats for saving
    /// </summary>
    public Dictionary<string, object> GetStatsForSaving()
    {
        Dictionary<string, object> stats = new Dictionary<string, object>
        {
            { "health", health },
            { "sanity", sanity },
            { "stamina", stamina },
            { "fear", fear },
            { "trust", trust },
            { "suspicion", suspicion },
            { "discoveredLocations", discoveredLocations },
            { "discoveredSecrets", discoveredSecrets }
            // Journal entries would need more complex serialization
        };
        
        return stats;
    }
    
    /// <summary>
    /// Load player stats from saved data
    /// </summary>
    public void LoadStats(Dictionary<string, object> savedStats)
    {
        // Implementation depends on your save system
        // This is just a placeholder
        Debug.Log("LoadStats method called - implement with your save system");
    }
    
    #endregion
}