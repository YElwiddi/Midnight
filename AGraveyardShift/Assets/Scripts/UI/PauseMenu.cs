using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Simple pause menu toggled with ESC. Pauses the game and shows resume/quit buttons.
/// Skips activation when dialogue or readable UI is open.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The pause menu panel to toggle on/off")]
    public GameObject pausePanel;

    [Header("Buttons")]
    public Button resumeButton;
    public Button quitButton;

    [Header("Scene Settings")]
    [Tooltip("Scene name to load when quitting to menu")]
    public string mainMenuSceneName = "MainMenu";

    private bool isPaused;
    private Movement playerMovement;

    void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitToMainMenu);

        playerMovement = FindFirstObjectByType<Movement>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                Resume();
            }
            else if (CanPause())
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

        // Re-lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Re-enable player input
        if (playerMovement != null)
            playerMovement.EnableAllInput();
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
