using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Simple pause menu toggled with ESC. Pauses the game and shows resume / settings / quit buttons.
/// Settings opens the same Settings screen as the main menu; ESC or Accept/Cancel there returns
/// to the pause menu. Skips activation when dialogue or readable UI is open.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The pause menu panel to toggle on/off")]
    public GameObject pausePanel;

    [Header("Buttons")]
    public Button resumeButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Settings")]
    [Tooltip("The Settings screen (SettingsPanel prefab), hidden until Settings is pressed.")]
    public OptionsPanel settingsPanel;

    [Header("Scene Settings")]
    [Tooltip("Scene name to load when quitting to menu")]
    public string mainMenuSceneName = "MainMenu";

    private bool isPaused;
    private Movement playerMovement;
    // Last frame something else owned the screen (dialogue, a note, a cutscene...). Escape closes
    // those too, so the press that closes one must not also open the pause menu.
    private int lastBlockedFrame = -10;

    void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitToMainMenu);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (settingsPanel != null)
        {
            settingsPanel.gameObject.SetActive(false);
            settingsPanel.Closed += OnSettingsClosed;
        }

        playerMovement = FindFirstObjectByType<Movement>();
    }

    void Update()
    {
        if (!isPaused && !CanPause())
            lastBlockedFrame = Time.frameCount;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused && settingsPanel != null && settingsPanel.gameObject.activeSelf)
            {
                settingsPanel.Close();
            }
            else if (isPaused)
            {
                Resume();
            }
            else if (CanPause() && Time.frameCount - lastBlockedFrame > 1 && FindFirstObjectByType<EndingScreenUI>() == null)
            {
                Pause();
            }
        }
    }

    private bool CanPause()
    {
        // Don't pause if dialogue is playing
        if (DialogueManager.GetInstance() != null && DialogueManager.GetInstance().IsDialoguePlaying())
            return false;

        // Don't pause if a readable is open
        if (ReadableUI.Instance != null && ReadableUI.Instance.IsOpen)
            return false;

        // Don't pause while another system holds the player (cutscene, jumpscare, CCTV...):
        // Resume re-enables input, which would free the player in the middle of it.
        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<Movement>();
        if (playerMovement != null && (!playerMovement.canMove || !playerMovement.canControlCamera))
            return false;

        // Nor in slow motion (kills). (An ending on screen is checked when Escape is pressed.)
        if (Time.timeScale != 1f)
            return false;

        return true;
    }

    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        // Show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Disable player input
        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<Movement>();

        if (playerMovement != null)
            playerMovement.DisableAllInput();
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.gameObject.SetActive(false);

        // Re-lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Re-enable player input
        if (playerMovement != null)
            playerMovement.EnableAllInput();
    }

    public void OpenSettings()
    {
        if (!isPaused || settingsPanel == null) return;
        if (pausePanel != null)
            pausePanel.SetActive(false);
        settingsPanel.gameObject.SetActive(true);
    }

    private void OnSettingsClosed()
    {
        if (isPaused && pausePanel != null)
            pausePanel.SetActive(true);
    }

    void OnDestroy()
    {
        if (settingsPanel != null)
            settingsPanel.Closed -= OnSettingsClosed;
    }

    public void QuitToMainMenu()
    {
        // Unpause so the main menu works normally
        Time.timeScale = 1f;
        isPaused = false;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Returns true if the game is currently paused by this menu.
    /// </summary>
    public bool IsPaused => isPaused;
}
