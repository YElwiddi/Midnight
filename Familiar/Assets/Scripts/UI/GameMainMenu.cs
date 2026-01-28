using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Main menu controller. Handles Start, Options, and Exit buttons.
/// </summary>
public class GameMainMenu : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Name of the game scene to load when Start is pressed")]
    public string gameSceneName = "Graveyard";

    [Header("UI References")]
    public Button startButton;
    public Button optionsButton;
    public Button exitButton;
    public GameObject optionsPanel;

    [Header("Transition Settings")]
    [Tooltip("Fade duration when transitioning to game")]
    public float fadeOutDuration = 1f;
    public Image fadeOverlay;

    [Header("Audio")]
    public AudioSource menuMusic;
    public AudioClip buttonClickSound;
    private AudioSource sfxSource;

    private bool isTransitioning = false;

    void Start()
    {
        // Ensure cursor is visible and unlocked for menu
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Set up button listeners
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnOptionsClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        // Hide options panel initially
        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        // Set up SFX audio source
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        // Ensure fade overlay starts transparent
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.color = new Color(0, 0, 0, 0);
        }

        // Reset any game state from previous session
        ResetGameState();
    }

    private void ResetGameState()
    {
        // Reset GameManager state if it exists (for returning from game over)
        // GameManager uses DontDestroyOnLoad, so it persists when returning from game
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetAllFlags();
        }

        // Reset time scale in case it was paused
        Time.timeScale = 1f;

        // Clean up any other DontDestroyOnLoad objects from previous game session
        // (Optional: add cleanup for other persistent objects if needed)
    }

    public void OnStartClicked()
    {
        if (isTransitioning) return;

        PlayButtonSound();
        StartCoroutine(TransitionToGame());
    }

    public void OnOptionsClicked()
    {
        PlayButtonSound();

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(!optionsPanel.activeSelf);
        }
    }

    public void OnExitClicked()
    {
        PlayButtonSound();

        Debug.Log("GameMainMenu: Exiting game...");

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    public void CloseOptions()
    {
        PlayButtonSound();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private IEnumerator TransitionToGame()
    {
        isTransitioning = true;

        // Fade out music
        if (menuMusic != null)
        {
            float startVolume = menuMusic.volume;
            float elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                menuMusic.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);

                // Also fade the screen to black
                if (fadeOverlay != null)
                {
                    fadeOverlay.color = new Color(0, 0, 0, elapsed / fadeOutDuration);
                }

                yield return null;
            }
        }
        else if (fadeOverlay != null)
        {
            // Just fade screen if no music
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.color = new Color(0, 0, 0, elapsed / fadeOutDuration);
                yield return null;
            }
        }

        // Load game scene
        Debug.Log($"GameMainMenu: Loading scene '{gameSceneName}'");
        SceneManager.LoadScene(gameSceneName);
    }

    private void PlayButtonSound()
    {
        if (sfxSource != null && buttonClickSound != null)
        {
            sfxSource.PlayOneShot(buttonClickSound);
        }
    }
}
