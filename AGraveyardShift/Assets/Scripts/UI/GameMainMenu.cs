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

    [Header("Endings")]
    [Tooltip("Button that opens the Endings gallery")]
    public Button endingsButton;
    [Tooltip("Panel listing unlocked endings")]
    public GameObject endingsPanel;

    [Header("Transition Settings")]
    [Tooltip("Fade duration when transitioning to game")]
    public float fadeOutDuration = 1f;
    [Tooltip("Fade-in duration when the menu opens")]
    public float fadeInDuration = 1f;
    [Tooltip("If true, the screen fades up from black when the menu loads")]
    public bool fadeInOnStart = true;
    public Image fadeOverlay;

    [Header("Intro Text")]
    [Tooltip("Optional intro text UI to display after fade, before loading game")]
    public IntroTextUI introTextUI;

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

        if (endingsButton != null)
            endingsButton.onClick.AddListener(OnEndingsClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        // Hide panels initially
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
        if (endingsPanel != null)
            endingsPanel.SetActive(false);

        // Set up SFX audio source
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        // Fade up from black when the menu opens (or just start transparent)
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            if (fadeInOnStart)
            {
                fadeOverlay.color = new Color(0, 0, 0, 1f);
                StartCoroutine(FadeInFromBlack());
            }
            else
            {
                fadeOverlay.color = new Color(0, 0, 0, 0f);
            }
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
        StopMenuAudio();
        StartCoroutine(TransitionToGame());
    }

    /// <summary>
    /// Immediately silences the menu background sound when the player hits Play,
    /// so it doesn't bleed into the intro dialogue.
    /// </summary>
    private void StopMenuAudio()
    {
        if (menuMusic != null)
            menuMusic.Stop();

        BackgroundMusic bg = FindFirstObjectByType<BackgroundMusic>();
        if (bg != null)
            bg.StopMusic();
    }

    public void OnOptionsClicked()
    {
        PlayButtonSound();

        if (endingsPanel != null)
            endingsPanel.SetActive(false);

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(!optionsPanel.activeSelf);
        }
    }

    public void OnEndingsClicked()
    {
        PlayButtonSound();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (endingsPanel != null)
        {
            endingsPanel.SetActive(!endingsPanel.activeSelf);
        }
    }

    public void CloseEndings()
    {
        PlayButtonSound();

        if (endingsPanel != null)
            endingsPanel.SetActive(false);
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

        // Show intro text if configured, otherwise load scene directly
        if (introTextUI != null)
        {
            Debug.Log("GameMainMenu: Showing intro text");
            introTextUI.Show(LoadGameScene);
        }
        else
        {
            LoadGameScene();
        }
    }

    private void LoadGameScene()
    {
        Debug.Log($"GameMainMenu: Loading scene '{gameSceneName}'");
        SceneManager.LoadScene(gameSceneName);
    }

    private IEnumerator FadeInFromBlack()
    {
        if (fadeOverlay == null) yield break;

        fadeOverlay.color = new Color(0, 0, 0, 1f);
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(elapsed / fadeInDuration);
            fadeOverlay.color = new Color(0, 0, 0, a);
            yield return null;
        }
        fadeOverlay.color = new Color(0, 0, 0, 0f);
    }

    private void PlayButtonSound()
    {
        if (sfxSource != null && buttonClickSound != null)
        {
            sfxSource.PlayOneShot(buttonClickSound);
        }
    }
}
