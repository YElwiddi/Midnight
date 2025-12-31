using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for displaying readable content (books, notes, tombstones, etc.)
/// Place this on a Canvas in your scene. Configure the UI elements in the Inspector.
/// </summary>
public class ReadableUI : MonoBehaviour
{
    #region Singleton
    private static ReadableUI instance;
    public static ReadableUI Instance => instance;
    #endregion

    #region UI References
    [Header("UI Panel References")]
    [Tooltip("The root panel that contains the readable UI. Will be shown/hidden.")]
    [SerializeField] private GameObject readablePanel;

    [Tooltip("Background image that displays the readable's appearance (book, scroll, etc.)")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("TextMeshPro component for displaying page content")]
    [SerializeField] private TextMeshProUGUI contentText;

    [Tooltip("Optional page indicator text (e.g., 'Page 1/3')")]
    [SerializeField] private TextMeshProUGUI pageIndicatorText;

    #endregion

    #region Private Fields
    private string[] currentPages;
    private int currentPageIndex;
    private bool isOpen;
    private Movement playerMovement;
    private CrosshairManager crosshairManager;
    private GameObject crosshairObject;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Find references early like DialogueManager does
        playerMovement = FindFirstObjectByType<Movement>();
        crosshairManager = FindFirstObjectByType<CrosshairManager>();

        if (readablePanel != null)
        {
            readablePanel.SetActive(false);
        }
    }

    private void Start()
    {
        // Find crosshair object (created by CrosshairManager)
        GameObject crosshairCanvas = GameObject.Find("CrosshairCanvas");
        if (crosshairCanvas != null)
        {
            Transform crosshair = crosshairCanvas.transform.Find("Crosshair");
            if (crosshair != null)
            {
                crosshairObject = crosshair.gameObject;
            }
        }
    }

    private void Update()
    {
        if (!isOpen) return;

        // Allow closing with Escape or E key
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            Close();
            return;
        }

        // Click anywhere to advance/close
        if (Input.GetMouseButtonDown(0))
        {
            AdvanceOrClose();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Opens the readable UI with the specified content.
    /// </summary>
    /// <param name="pages">Array of page text content</param>
    /// <param name="background">Optional background sprite (book, scroll, etc.)</param>
    public void Open(string[] pages, Sprite background = null)
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning("ReadableUI: No pages to display");
            return;
        }

        currentPages = pages;
        currentPageIndex = 0;
        isOpen = true;

        // Set background image
        if (backgroundImage != null && background != null)
        {
            backgroundImage.sprite = background;
            backgroundImage.enabled = true;
        }
        else if (backgroundImage != null)
        {
            backgroundImage.enabled = false;
        }

        // Show panel
        if (readablePanel != null)
        {
            readablePanel.SetActive(true);
        }

        // Display first page
        DisplayCurrentPage();

        // Pause player (also handles cursor and crosshair)
        PausePlayer();

        Debug.Log($"ReadableUI: Opened with {pages.Length} page(s)");
    }

    /// <summary>
    /// Closes the readable UI.
    /// </summary>
    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        currentPages = null;
        currentPageIndex = 0;

        // Hide panel
        if (readablePanel != null)
        {
            readablePanel.SetActive(false);
        }

        // Resume player (also handles cursor and crosshair)
        ResumePlayer();

        Debug.Log("ReadableUI: Closed");
    }

    /// <summary>
    /// Returns true if the readable UI is currently open.
    /// </summary>
    public bool IsOpen => isOpen;
    #endregion

    #region Private Methods
    private void AdvanceOrClose()
    {
        if (!isOpen) return;

        // If there are more pages, advance
        if (currentPageIndex < currentPages.Length - 1)
        {
            currentPageIndex++;
            DisplayCurrentPage();
            Debug.Log($"ReadableUI: Advanced to page {currentPageIndex + 1}/{currentPages.Length}");
        }
        else
        {
            // Last page, close
            Close();
        }
    }

    private void DisplayCurrentPage()
    {
        if (currentPages == null || currentPageIndex >= currentPages.Length) return;

        // Set content text
        if (contentText != null)
        {
            contentText.text = currentPages[currentPageIndex];
        }

        // Update page indicator
        if (pageIndicatorText != null)
        {
            if (currentPages.Length > 1)
            {
                pageIndicatorText.text = $"Page {currentPageIndex + 1}/{currentPages.Length}";
                pageIndicatorText.gameObject.SetActive(true);
            }
            else
            {
                // Hide indicator for single-page readables
                pageIndicatorText.gameObject.SetActive(false);
            }
        }
    }

    private void PausePlayer()
    {
        // Disable player movement and camera
        if (playerMovement != null)
        {
            playerMovement.DisableAllInput();
        }

        // Hide crosshair
        if (crosshairObject != null)
        {
            crosshairObject.SetActive(false);
        }

        // Show cursor and unlock it
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResumePlayer()
    {
        // Enable player movement and camera
        if (playerMovement != null)
        {
            playerMovement.EnableAllInput();
        }

        // Show crosshair
        if (crosshairObject != null)
        {
            crosshairObject.SetActive(true);
        }

        // Hide cursor and lock it
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    #endregion
}
