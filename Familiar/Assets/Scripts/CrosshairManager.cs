using UnityEngine;
using UnityEngine.UI;

public class CrosshairManager : MonoBehaviour
{
    [Header("Crosshair Settings")]
    public Color crosshairColor = Color.white;
    public Color highlightColor = Color.white;
    public float circleSize = 20f;
    public float ringThickness = 2f;
    public float fillGap = 3f;

    [Header("Sprites (Optional)")]
    [Tooltip("Leave empty to use procedurally generated circles")]
    public Sprite hollowCircleSprite;
    public Sprite filledCircleSprite;

    [Header("References")]
    public Canvas uiCanvas;

    // Crosshair elements
    private RectTransform crosshairParent;
    private Image outerRing;
    private Image innerFill;
    private bool isHighlighted;

    private void Start()
    {
        if (uiCanvas == null)
        {
            CreateUICanvas();
        }

        CreateCrosshair();
    }

    private void CreateUICanvas()
    {
        GameObject canvasObject = new GameObject("CrosshairCanvas");
        uiCanvas = canvasObject.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObject);
    }

    private void CreateCrosshair()
    {
        // Create parent object
        GameObject parentObj = new GameObject("Crosshair");
        parentObj.transform.SetParent(uiCanvas.transform, false);
        crosshairParent = parentObj.AddComponent<RectTransform>();
        crosshairParent.anchoredPosition = Vector2.zero;
        crosshairParent.sizeDelta = new Vector2(circleSize, circleSize);
        crosshairParent.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairParent.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairParent.pivot = new Vector2(0.5f, 0.5f);

        if (hollowCircleSprite != null && filledCircleSprite != null)
        {
            // Use provided sprites
            CreateSpriteBasedCrosshair();
        }
        else
        {
            // Create procedural circle crosshair
            CreateProceduralCrosshair();
        }

        // Start in default (hollow) state
        SetHighlighted(false);
    }

    private void CreateSpriteBasedCrosshair()
    {
        // Outer ring (hollow circle)
        GameObject ringObj = new GameObject("OuterRing");
        ringObj.transform.SetParent(crosshairParent, false);
        RectTransform ringRect = ringObj.AddComponent<RectTransform>();
        ringRect.anchoredPosition = Vector2.zero;
        ringRect.sizeDelta = new Vector2(circleSize, circleSize);
        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);

        outerRing = ringObj.AddComponent<Image>();
        outerRing.sprite = hollowCircleSprite;
        outerRing.color = crosshairColor;

        // Inner fill (solid circle) - shown when highlighted, with gap from ring
        GameObject fillObj = new GameObject("InnerFill");
        fillObj.transform.SetParent(crosshairParent, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchoredPosition = Vector2.zero;
        float fillSize = circleSize - (ringThickness * 2) - (fillGap * 2);
        fillRect.sizeDelta = new Vector2(fillSize, fillSize);
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);

        innerFill = fillObj.AddComponent<Image>();
        innerFill.sprite = filledCircleSprite;
        innerFill.color = highlightColor;
        innerFill.gameObject.SetActive(false);
    }

    private void CreateProceduralCrosshair()
    {
        // Outer ring (hollow circle)
        GameObject ringObj = new GameObject("OuterRing");
        ringObj.transform.SetParent(crosshairParent, false);
        RectTransform ringRect = ringObj.AddComponent<RectTransform>();
        ringRect.anchoredPosition = Vector2.zero;
        ringRect.sizeDelta = new Vector2(circleSize, circleSize);
        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);

        outerRing = ringObj.AddComponent<Image>();
        outerRing.color = crosshairColor;
        outerRing.sprite = CreateRingSprite(64, ringThickness / circleSize);

        // Inner fill (solid circle for highlighted state, smaller to show gap)
        GameObject fillObj = new GameObject("InnerFill");
        fillObj.transform.SetParent(crosshairParent, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchoredPosition = Vector2.zero;
        float fillSize = circleSize - (ringThickness * 2) - (fillGap * 2);
        fillRect.sizeDelta = new Vector2(fillSize, fillSize);
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);

        innerFill = fillObj.AddComponent<Image>();
        innerFill.sprite = CreateCircleSprite(64);
        innerFill.color = highlightColor;
        innerFill.gameObject.SetActive(false);
    }

    private Sprite CreateCircleSprite(int resolution)
    {
        Texture2D texture = new Texture2D(resolution, resolution);
        texture.filterMode = FilterMode.Bilinear;

        float radius = resolution / 2f;
        Vector2 center = new Vector2(radius, radius);

        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance < radius - 1)
                {
                    texture.SetPixel(x, y, Color.white);
                }
                else if (distance < radius)
                {
                    float alpha = radius - distance;
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateRingSprite(int resolution, float thicknessRatio)
    {
        Texture2D texture = new Texture2D(resolution, resolution);
        texture.filterMode = FilterMode.Bilinear;

        float outerRadius = resolution / 2f;
        float innerRadius = outerRadius * (1f - thicknessRatio * 2f);
        Vector2 center = new Vector2(outerRadius, outerRadius);

        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);

                // Outside outer edge
                if (distance >= outerRadius)
                {
                    texture.SetPixel(x, y, Color.clear);
                }
                // Anti-alias outer edge
                else if (distance > outerRadius - 1)
                {
                    float alpha = outerRadius - distance;
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                // Inside inner edge (hollow center)
                else if (distance < innerRadius - 1)
                {
                    texture.SetPixel(x, y, Color.clear);
                }
                // Anti-alias inner edge
                else if (distance < innerRadius)
                {
                    float alpha = distance - (innerRadius - 1);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                // Ring area
                else
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
    }

    public void SetHighlighted(bool highlighted)
    {
        isHighlighted = highlighted;

        if (innerFill != null)
        {
            innerFill.gameObject.SetActive(highlighted);
        }

        if (outerRing != null)
        {
            outerRing.color = highlighted ? highlightColor : crosshairColor;
        }
    }

    // Legacy method for compatibility - maps to SetHighlighted
    public void SetCrosshairColor(Color newColor)
    {
        // Check if this is the highlight color or default color
        if (newColor == highlightColor || newColor == Color.yellow)
        {
            SetHighlighted(true);
        }
        else
        {
            SetHighlighted(false);
        }
    }

    public void SetCrosshairStyle(bool showCenterDot, bool showLines)
    {
        // Legacy method - now controls visibility of the crosshair
        if (crosshairParent != null)
        {
            crosshairParent.gameObject.SetActive(showCenterDot || showLines);
        }
    }
}
