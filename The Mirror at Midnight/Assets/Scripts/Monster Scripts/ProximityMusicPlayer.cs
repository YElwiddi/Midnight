using UnityEngine;

public class ProximityMusicPlayer : MonoBehaviour
{
    // Reference to the player
    public Transform player;
    
    // Audio settings
    [Header("Audio Settings")]
    public AudioClip musicClip;
    public bool playOnAwake = true;
    public bool loop = true;
    
    // Volume settings
    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float maxVolume = 1.0f;
    [Range(0f, 1f)]
    public float minVolume = 0.0f;
    
    // Distance settings
    [Header("Distance Settings")]
    public float maxDistance = 20.0f;    // Distance at which volume is at minimum
    public float minDistance = 2.0f;     // Distance at which volume is at maximum
    
    // Transition settings
    [Header("Transition Settings")]
    public bool useLogarithmicFalloff = true;  // Logarithmic falloff sounds more natural
    public float volumeSmoothTime = 0.5f;      // Smooth transitions between volume levels
    
    // Private variables
    private AudioSource audioSource;
    private float currentVelocity;  // Used for SmoothDamp
    
    private void Awake()
    {
        // Get or add an AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Configure the audio source
        audioSource.clip = musicClip;
        audioSource.loop = loop;
        audioSource.playOnAwake = false;  // We'll control play manually
        audioSource.spatialBlend = 0f;    // Set to 0 for 2D sound (non-positional)
        
        // If no player is assigned, try to find one with the "Player" tag
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
            else
            {
                Debug.LogWarning("No player found with 'Player' tag. Please assign the player manually.");
            }
        }
    }
    
    private void Start()
    {
        // Start playing if set to play on awake
        if (playOnAwake && musicClip != null)
        {
            audioSource.Play();
        }
    }
    
    private void Update()
    {
        if (player == null || audioSource.clip == null)
            return;
        
        // Calculate distance to player
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        // Calculate target volume based on distance
        float targetVolume = CalculateVolumeByDistance(distanceToPlayer);
        
        // Smoothly adjust volume
        audioSource.volume = Mathf.SmoothDamp(
            audioSource.volume, 
            targetVolume, 
            ref currentVelocity, 
            volumeSmoothTime
        );
        
        // If the audio source is not playing but should be, start it
        if (!audioSource.isPlaying && audioSource.clip != null)
        {
            audioSource.Play();
        }
    }
    
    private float CalculateVolumeByDistance(float distance)
    {
        // Clamp the distance between min and max
        float clampedDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        
        // Calculate how far we are between min and max distance (0 to 1)
        float distanceRatio = (clampedDistance - minDistance) / (maxDistance - minDistance);
        
        // Invert the ratio so that closer = higher volume
        float invertedRatio = 1.0f - distanceRatio;
        
        // Apply logarithmic curve if enabled (more natural sound falloff)
        if (useLogarithmicFalloff)
        {
            invertedRatio = Mathf.Log10(invertedRatio * 9f + 1f);
        }
        
        // Calculate the actual volume by interpolating between min and max volume
        return Mathf.Lerp(minVolume, maxVolume, invertedRatio);
    }
    
    // Public methods to control the audio
    public void PlayMusic()
    {
        if (audioSource.clip != null)
        {
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("No audio clip assigned to ProximityMusicPlayer.");
        }
    }
    
    public void StopMusic()
    {
        audioSource.Stop();
    }
    
    public void PauseMusic()
    {
        audioSource.Pause();
    }
    
    public void SetMusicClip(AudioClip clip)
    {
        audioSource.clip = clip;
        
        // If already playing, restart with new clip
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.Play();
        }
    }
    
    // Visualize the distance ranges in the Scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, minDistance);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }
}