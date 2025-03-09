using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class InteractionSystem : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactionDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    public LayerMask interactableMask;
    
    [Header("UI References")]
    public Canvas uiCanvas;
    public Color highlightCrosshairColor = Color.yellow;
    private Color defaultCrosshairColor;
    
    [Header("Interaction Prompt")]
    public bool showInteractionPrompt = true;
    private TextMeshProUGUI promptText;
    
    // References
    private Camera playerCamera;
    private CrosshairManager crosshairManager;
    private IInteractable currentInteractable;
    
    void Start()
    {
        // Get references
        playerCamera = GetComponent<Camera>();
        if (playerCamera == null)
            playerCamera = Camera.main;
            
        crosshairManager = GetComponent<CrosshairManager>();
        if (crosshairManager == null)
            crosshairManager = FindObjectOfType<CrosshairManager>();
            
        // Cache default crosshair color
        if (crosshairManager != null)
            defaultCrosshairColor = crosshairManager.crosshairColor;
            
        // Set up interaction prompt if enabled
        if (showInteractionPrompt)
            SetupInteractionPrompt();
    }
    
    void Update()
    {
        // Check for interactable objects in front of the player
        CheckForInteractables();
        
        // Process interaction input
        if (currentInteractable != null && Input.GetKeyDown(interactKey))
        {
            currentInteractable.Interact();
        }
    }
    
    void CheckForInteractables()
    {
        // Cast a ray from the center of the screen
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        
        // Check if ray hits an interactable object
        if (Physics.Raycast(ray, out hit, interactionDistance, interactableMask))
        {
            // Check if the hit object implements IInteractable
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                
                // New interactable found
                if (currentInteractable != interactable)
                {
                    // Change crosshair color to indicate interactable
                    if (crosshairManager != null)
                        crosshairManager.SetCrosshairColor(highlightCrosshairColor);
                    
                    // Update prompt text
                    if (promptText != null)
                    {
                        promptText.text = $"Press {interactKey} to {interactable.GetInteractionPrompt()}";
                        promptText.gameObject.SetActive(true);
                    }
                    
                    // Set current interactable
                    currentInteractable = interactable;
                }
            }
            else
            {
                ClearInteractable();
            }
        }
        else
        {
            ClearInteractable();
        }
    }
    
    void ClearInteractable()
    {
        if (currentInteractable != null)
        {
            // Reset crosshair color
            if (crosshairManager != null)
                crosshairManager.SetCrosshairColor(defaultCrosshairColor);
            
            // Hide prompt text
            if (promptText != null)
                promptText.gameObject.SetActive(false);
            
            // Clear current interactable
            currentInteractable = null;
        }
    }
    
    void SetupInteractionPrompt()
    {
        // Create a canvas if none exists
        if (uiCanvas == null)
        {
            // Check if one already exists from the CrosshairManager
            if (crosshairManager != null && crosshairManager.uiCanvas != null)
            {
                uiCanvas = crosshairManager.uiCanvas;
            }
            else
            {
                // Create a new canvas
                GameObject canvasObj = new GameObject("InteractionCanvas");
                uiCanvas = canvasObj.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }
        
        // Create prompt text
        GameObject textObj = new GameObject("InteractionPrompt");
        textObj.transform.SetParent(uiCanvas.transform, false);
        
        // Set up the RectTransform
        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(0, -50f); // Position below crosshair
        rectTransform.sizeDelta = new Vector2(300f, 50f);
        
        // Add TextMeshProUGUI component
        promptText = textObj.AddComponent<TextMeshProUGUI>();
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontSize = 24;
        promptText.color = Color.white;
        promptText.text = "";
        promptText.gameObject.SetActive(false);
    }
}

// Interface for interactable objects
public interface IInteractable
{
    void Interact();
    string GetInteractionPrompt();
}