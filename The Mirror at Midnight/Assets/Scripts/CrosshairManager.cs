using UnityEngine;
using UnityEngine.UI;

public class CrosshairManager : MonoBehaviour
{
    [Header("Crosshair Settings")]
    public Color crosshairColor = Color.white;
    public float crosshairSize = 10f;
    public float crosshairThickness = 2f;
    public float crosshairGap = 5f;
    
    [Header("References")]
    public Canvas uiCanvas;
    
    // Crosshair elements
    private RectTransform topLine;
    private RectTransform bottomLine;
    private RectTransform leftLine;
    private RectTransform rightLine;
    private RectTransform centerDot;
    
    private void Start()
    {
        if (uiCanvas == null)
        {
            // Create a canvas if none is provided
            CreateUICanvas();
        }
        
        // Create the crosshair elements
        CreateCrosshair();
        
        // Show cursor if it was hidden by your Movement script
        if (Cursor.visible == false)
        {
            // You might want to keep your original cursor settings,
            // but the crosshair will now function as your cursor
            // If you still want to hide the system cursor, leave these lines active
            // Cursor.lockState = CursorLockMode.Locked;
            // Cursor.visible = false;
        }
    }
    
    private void CreateUICanvas()
    {
        // Create a new GameObject for the canvas
        GameObject canvasObject = new GameObject("CrosshairCanvas");
        uiCanvas = canvasObject.AddComponent<Canvas>();
        
        // Set the canvas to be screen-space overlay
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        // Add a CanvasScaler component
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Add a GraphicRaycaster component
        canvasObject.AddComponent<GraphicRaycaster>();
        
        // Make sure the canvas persists between scenes if needed
        DontDestroyOnLoad(canvasObject);
    }
    
    private void CreateCrosshair()
    {
        // Create parent object to hold all crosshair elements
        GameObject crosshairParent = new GameObject("Crosshair");
        crosshairParent.transform.SetParent(uiCanvas.transform, false);
        RectTransform crosshairRect = crosshairParent.AddComponent<RectTransform>();
        crosshairRect.anchoredPosition = Vector2.zero;
        crosshairRect.sizeDelta = new Vector2(crosshairSize * 2, crosshairSize * 2);
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
        
        // Create center dot
        centerDot = CreateCrosshairElement(crosshairRect, "CenterDot");
        centerDot.sizeDelta = new Vector2(crosshairThickness, crosshairThickness);
        Image centerImage = centerDot.GetComponent<Image>();
        centerImage.color = crosshairColor;
        
        // Create top line
        topLine = CreateCrosshairElement(crosshairRect, "TopLine");
        topLine.sizeDelta = new Vector2(crosshairThickness, crosshairSize / 2);
        topLine.anchoredPosition = new Vector2(0, crosshairGap + crosshairSize / 4);
        
        // Create bottom line
        bottomLine = CreateCrosshairElement(crosshairRect, "BottomLine");
        bottomLine.sizeDelta = new Vector2(crosshairThickness, crosshairSize / 2);
        bottomLine.anchoredPosition = new Vector2(0, -crosshairGap - crosshairSize / 4);
        
        // Create left line
        leftLine = CreateCrosshairElement(crosshairRect, "LeftLine");
        leftLine.sizeDelta = new Vector2(crosshairSize / 2, crosshairThickness);
        leftLine.anchoredPosition = new Vector2(-crosshairGap - crosshairSize / 4, 0);
        
        // Create right line
        rightLine = CreateCrosshairElement(crosshairRect, "RightLine");
        rightLine.sizeDelta = new Vector2(crosshairSize / 2, crosshairThickness);
        rightLine.anchoredPosition = new Vector2(crosshairGap + crosshairSize / 4, 0);
    }
    
    private RectTransform CreateCrosshairElement(RectTransform parent, string name)
    {
        GameObject element = new GameObject(name);
        element.transform.SetParent(parent, false);
        
        RectTransform rect = element.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        
        Image image = element.AddComponent<Image>();
        image.color = crosshairColor;
        
        return rect;
    }
    
    // Optional: Method to dynamically change crosshair color
    public void SetCrosshairColor(Color newColor)
    {
        crosshairColor = newColor;
        
        // Update all crosshair elements with the new color
        centerDot.GetComponent<Image>().color = newColor;
        topLine.GetComponent<Image>().color = newColor;
        bottomLine.GetComponent<Image>().color = newColor;
        leftLine.GetComponent<Image>().color = newColor;
        rightLine.GetComponent<Image>().color = newColor;
    }
    
    // Optional: Method to show/hide parts of the crosshair
    public void SetCrosshairStyle(bool showCenterDot, bool showLines)
    {
        centerDot.gameObject.SetActive(showCenterDot);
        topLine.gameObject.SetActive(showLines);
        bottomLine.gameObject.SetActive(showLines);
        leftLine.gameObject.SetActive(showLines);
        rightLine.gameObject.SetActive(showLines);
    }
}