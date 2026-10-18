using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Main menu controller. Handles Start, Options, and Exit buttons, the "Return to the ... Watch"
/// offer after a death, and the gate backdrop's moments: the camera push when the night starts and
/// the lamp going out on Quit.
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

    [Header("Crypt Fiends")]
    [Tooltip("Panel listing encountered crypt fiends (opened from a teaser button inside the Endings modal)")]
    public GameObject cryptFiendsPanel;

    [Header("Field Notes")]
    [Tooltip("Panel with the full creature field notes the player has read (opened from a teaser button inside the Endings modal)")]
    public GameObject fieldNotesPanel;

    [Header("Phase Select")]
    [Tooltip("Panel that lets the player start at a later phase of the night. Shown when Play is pressed once a later watch has been reached.")]
    public GameObject phaseSelectPanel;

    [Header("After a death")]
    [Tooltip("'Return to the Second Watch': shown above Play after a keeper dies in the second or third watch.")]
    public Button returnButton;
    public TMP_Text returnLabel;
    [Tooltip("Who took the keeper, under the return button.")]
    public TMP_Text returnCauseLabel;
    [Tooltip("Play's label: reads NEW NIGHT while the return button is offered.")]
    public TMP_Text playLabel;

    [Header("Gate backdrop")]
    [Tooltip("The menu UI root (fades for the camera moments).")]
    public CanvasGroup menuGroup;
    public MenuIntroFX menuIntro;
    public MenuCameraDirector cameraDirector;
    public MenuLamp lamp;
    [Tooltip("Seconds the camera pushes toward the cabin before the screen is fully black.")]
    public float pushDuration = 1.8f;

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

        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnClicked);

        // Hide panels initially
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
        if (endingsPanel != null)
            endingsPanel.SetActive(false);
        if (cryptFiendsPanel != null)
            cryptFiendsPanel.SetActive(false);
        if (fieldNotesPanel != null)
            fieldNotesPanel.SetActive(false);
        if (phaseSelectPanel != null)
            phaseSelectPanel.SetActive(false);

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

        SetUpReturnOffer();
        Loc.LocaleChanged += SetUpReturnOffer;
        StartCoroutine(Entrance());
    }

    void OnDestroy()
    {
        Loc.LocaleChanged -= SetUpReturnOffer;
    }

    // After a death in the second or third watch, the menu leads with a way straight back to it.
    private void SetUpReturnOffer()
    {
        int watch = LastDeathSave.ReturnWatch;
        bool offer = watch >= 2 && returnButton != null;
        if (returnButton != null)
            returnButton.gameObject.SetActive(offer);
        if (offer)
        {
            if (returnLabel != null) { Loc.EnsureFontComponent(returnLabel); returnLabel.text = DeathCauses.ReturnLabel(watch); }
            if (returnCauseLabel != null) { Loc.EnsureFontComponent(returnCauseLabel); returnCauseLabel.text = DeathCauses.Label(LastDeathSave.ReturnCause); }
        }
        if (playLabel != null)
            Loc.BindKey(playLabel, offer ? "ui.menu.newNight" : "ui.menu.play");
    }

    // On launch the menu UI waits for the splash to finish before it comes in.
    private IEnumerator Entrance()
    {
        if (DisclaimerScreen.HasShown) yield break;
        if (menuIntro != null) menuIntro.Hold();
        while (!DisclaimerScreen.HasShown) yield return null;
        if (menuIntro != null) menuIntro.Replay();
    }

    public void OnReturnClicked()
    {
        if (isTransitioning) return;
        int watch = LastDeathSave.ReturnWatch;
        StartAtPhase(watch >= 2 ? watch : PhaseSelect.FirstPhase);
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

        // A new run starts from the beginning unless the player picks a later phase
        PhaseSelect.Reset();

        // Clean up any other DontDestroyOnLoad objects from previous game session
        // (Optional: add cleanup for other persistent objects if needed)
    }

    public void OnStartClicked()
    {
        if (isTransitioning) return;

        // Once the game has been beaten, Play asks where in the night to start
        if (phaseSelectPanel != null && PhaseSelect.IsUnlocked)
        {
            PlayButtonSound();
            if (optionsPanel != null)
                optionsPanel.SetActive(false);
            if (endingsPanel != null)
                endingsPanel.SetActive(false);
            if (cryptFiendsPanel != null)
                cryptFiendsPanel.SetActive(false);
            if (fieldNotesPanel != null)
                fieldNotesPanel.SetActive(false);
            phaseSelectPanel.SetActive(true);
            return;
        }

        StartAtPhase(PhaseSelect.FirstPhase);
    }

    /// <summary>Starts a run at the given phase (1 = the whole night). Wired to the phase select slots.</summary>
    public void StartAtPhase(int phase)
    {
        if (isTransitioning) return;

        PhaseSelect.StartPhase = phase;
        // A new run: the return offer from the last death no longer applies.
        LastDeathSave.ClearReturnOffer();
        if (phaseSelectPanel != null)
            phaseSelectPanel.SetActive(false);

        PlayButtonSound();
        StopMenuAudio();
        // A later start is for returning players: skip the story intro text
        StartCoroutine(TransitionToGame(phase > PhaseSelect.FirstPhase));
    }

    public void ClosePhaseSelect()
    {
        PlayButtonSound();

        if (phaseSelectPanel != null)
            phaseSelectPanel.SetActive(false);
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
        if (cryptFiendsPanel != null)
            cryptFiendsPanel.SetActive(false);
        if (fieldNotesPanel != null)
            fieldNotesPanel.SetActive(false);
        if (phaseSelectPanel != null)
            phaseSelectPanel.SetActive(false);

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
        if (cryptFiendsPanel != null)
            cryptFiendsPanel.SetActive(false);
        if (fieldNotesPanel != null)
            fieldNotesPanel.SetActive(false);
        if (phaseSelectPanel != null)
            phaseSelectPanel.SetActive(false);

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

    public void OpenCryptFiends()
    {
        PlayButtonSound();

        // Hide the Endings modal while the gallery is open; CloseCryptFiends restores it.
        if (endingsPanel != null)
            endingsPanel.SetActive(false);
        if (cryptFiendsPanel != null)
            cryptFiendsPanel.SetActive(true);
    }

    public void CloseCryptFiends()
    {
        PlayButtonSound();

        if (cryptFiendsPanel != null)
            cryptFiendsPanel.SetActive(false);
        // Return to the Endings modal it was opened from.
        if (endingsPanel != null)
            endingsPanel.SetActive(true);
    }

    public void OpenFieldNotes()
    {
        PlayButtonSound();

        // Hide the Endings modal while the archive is open; CloseFieldNotes restores it.
        if (endingsPanel != null)
            endingsPanel.SetActive(false);
        if (fieldNotesPanel != null)
            fieldNotesPanel.SetActive(true);
    }

    public void CloseFieldNotes()
    {
        PlayButtonSound();

        if (fieldNotesPanel != null)
            fieldNotesPanel.SetActive(false);
        // Return to the Endings modal it was opened from.
        if (endingsPanel != null)
            endingsPanel.SetActive(true);
    }

    public void OnExitClicked()
    {
        if (isTransitioning) return;
        PlayButtonSound();
        StartCoroutine(QuitRoutine());
    }

    // Quit puts the gate lamp out, then the screen goes black.
    private IEnumerator QuitRoutine()
    {
        isTransitioning = true;
        if (menuGroup != null) StartCoroutine(FadeGroup(menuGroup, 0f, 0.35f));
        if (lamp != null) yield return lamp.PutOut();

        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            for (float t = 0f; t < 1f; )
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / 0.6f);
                fadeOverlay.color = new Color(0, 0, 0, t);
                yield return null;
            }
        }

        Debug.Log("GameMainMenu: Exiting game...");

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float to, float duration)
    {
        float from = group.alpha;
        group.interactable = false;
        for (float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
    }

    public void CloseOptions()
    {
        PlayButtonSound();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private IEnumerator TransitionToGame(bool skipIntroText = false)
    {
        isTransitioning = true;

        // The camera pushes toward the cabin while the menu falls away, then the screen fades.
        if (cameraDirector != null)
        {
            cameraDirector.Push(pushDuration);
            if (menuGroup != null) StartCoroutine(FadeGroup(menuGroup, 0f, 0.4f));
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, pushDuration - fadeOutDuration));
        }

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
        if (introTextUI != null && !skipIntroText)
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
