using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum StaircaseDialogueTriggerType
{
    TimeBased,
    LoopBased
}

[Serializable]
public class StaircaseDialogueEntry
{
    [TextArea(2, 5)]
    public string text;

    [Header("Screen Position (Anchor)")]
    [Tooltip("Randomize position each time this entry appears")]
    public bool randomizePosition = false;
    public float anchorMinX = 0.1f;
    public float anchorMinY = 0.4f;
    public float anchorMaxX = 0.9f;
    public float anchorMaxY = 0.6f;

    [Header("Timing")]
    public float displayDuration = 3f;

    [Header("Trigger")]
    public StaircaseDialogueTriggerType triggerType = StaircaseDialogueTriggerType.TimeBased;

    [Tooltip("Seconds after sequence start (TimeBased only)")]
    public float triggerTime = 0f;

    [Tooltip("Loop count target (LoopBased only)")]
    public int triggerLoopCount = 1;

    [Header("Optional Overrides")]
    [Tooltip("0 = use default font size")]
    public float fontSize = 0f;

    [Tooltip("Alpha 0 = use default color")]
    public Color textColor = new Color(1f, 1f, 1f, 0f);

    [Tooltip("Leave empty to use default font")]
    public TMP_FontAsset font;

    public TextAlignmentOptions alignment = TextAlignmentOptions.Center;
}

public class StaircaseDialogueController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InfiniteStaircase infiniteStaircase;

    [Header("Dialogue Entries")]
    [SerializeField] private StaircaseDialogueEntry[] dialogueEntries;

    [Header("Final Text")]
    [TextArea(2, 5)]
    [SerializeField] private string finalText = "";
    [SerializeField] private float finalTextFontSize = 72f;
    [SerializeField] private Color finalTextColor = Color.white;
    [SerializeField] private float finalTextDisplayDuration = 5f;

    [Header("Typewriter & Fade")]
    [Tooltip("Characters per second")]
    [SerializeField] private float typewriterSpeed = 40f;
    [SerializeField] private float textFadeInDuration = 0.5f;
    [SerializeField] private float textFadeOutDuration = 0.5f;

    [Header("Defaults")]
    [SerializeField] private float defaultFontSize = 36f;
    [SerializeField] private Color defaultTextColor = Color.white;
    [SerializeField] private TMP_FontAsset font;

    [Header("Canvas")]
    [SerializeField] private int canvasSortingOrder = 100;

    [Header("Trigger")]
    [SerializeField] private bool triggerOnce = true;

    // Runtime state
    private bool hasTriggered;
    private bool sequenceActive;
    private float elapsedTime;
    private int completedEntryCount;
    private int totalEntryCount;

    private Canvas canvas;
    private GameObject canvasObject;
    private List<int> pendingTimeEntries;
    private Dictionary<int, List<int>> pendingLoopEntries;
    private List<GameObject> spawnedObjects = new List<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"StaircaseDialogue: OnTriggerEnter hit by '{other.name}' (tag: {other.tag})");

        if (!other.CompareTag("Player")) return;
        if (triggerOnce && hasTriggered)
        {
            Debug.Log("StaircaseDialogue: Already triggered, skipping");
            return;
        }

        Debug.Log("StaircaseDialogue: Player entered trigger, starting sequence");
        hasTriggered = true;
        StartSequence();
    }

    private void StartSequence()
    {
        if (sequenceActive) return;
        sequenceActive = true;
        elapsedTime = 0f;
        completedEntryCount = 0;

        int entryCount = dialogueEntries != null ? dialogueEntries.Length : 0;
        Debug.Log($"StaircaseDialogue: StartSequence - {entryCount} entries, finalText='{finalText}'");

        CreateCanvas();
        Debug.Log($"StaircaseDialogue: Canvas created (sortingOrder={canvasSortingOrder})");

        CategorizeEntries();
        Debug.Log($"StaircaseDialogue: Categorized - {pendingTimeEntries.Count} time-based, {pendingLoopEntries.Count} loop-based groups, totalEntryCount={totalEntryCount}");

        if (infiniteStaircase != null)
        {
            infiniteStaircase.onPlayerLooped.AddListener(OnPlayerLooped);
            Debug.Log("StaircaseDialogue: Subscribed to onPlayerLooped");
        }
        else
        {
            Debug.LogWarning("StaircaseDialogue: infiniteStaircase reference is null!");
        }
    }

    private void Update()
    {
        if (!sequenceActive) return;

        elapsedTime += Time.deltaTime;

        // Check time-based entries
        for (int i = pendingTimeEntries.Count - 1; i >= 0; i--)
        {
            int entryIndex = pendingTimeEntries[i];
            StaircaseDialogueEntry entry = dialogueEntries[entryIndex];

            if (elapsedTime >= entry.triggerTime)
            {
                Debug.Log($"StaircaseDialogue: Time-based entry fired at {elapsedTime:F1}s (target: {entry.triggerTime}s) - '{entry.text}'");
                pendingTimeEntries.RemoveAt(i);
                StartCoroutine(PlayEntry(entry));
            }
        }
    }

    private void OnPlayerLooped()
    {
        if (!sequenceActive) return;
        if (infiniteStaircase == null) return;

        int currentLoop = infiniteStaircase.LoopCount;
        Debug.Log($"StaircaseDialogue: OnPlayerLooped - loop #{currentLoop}");

        if (pendingLoopEntries.TryGetValue(currentLoop, out List<int> indices))
        {
            Debug.Log($"StaircaseDialogue: Firing {indices.Count} loop-based entries for loop #{currentLoop}");
            foreach (int entryIndex in indices)
            {
                StartCoroutine(PlayEntry(dialogueEntries[entryIndex]));
            }
            pendingLoopEntries.Remove(currentLoop);
        }
    }

    private void CategorizeEntries()
    {
        pendingTimeEntries = new List<int>();
        pendingLoopEntries = new Dictionary<int, List<int>>();
        totalEntryCount = dialogueEntries != null ? dialogueEntries.Length : 0;

        if (dialogueEntries == null) return;

        for (int i = 0; i < dialogueEntries.Length; i++)
        {
            StaircaseDialogueEntry entry = dialogueEntries[i];

            if (entry.triggerType == StaircaseDialogueTriggerType.TimeBased)
            {
                pendingTimeEntries.Add(i);
            }
            else
            {
                int loopTarget = entry.triggerLoopCount;
                if (!pendingLoopEntries.ContainsKey(loopTarget))
                {
                    pendingLoopEntries[loopTarget] = new List<int>();
                }
                pendingLoopEntries[loopTarget].Add(i);
            }
        }
    }

    private void CreateCanvas()
    {
        canvasObject = new GameObject("StaircaseDialogueCanvas");
        canvasObject.transform.SetParent(transform);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        CanvasGroup cg = canvasObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    private IEnumerator PlayEntry(StaircaseDialogueEntry entry)
    {
        // Resolve font size and color
        float size = entry.fontSize > 0f ? entry.fontSize : defaultFontSize;
        Color color = entry.textColor.a > 0f ? entry.textColor : defaultTextColor;

        // Create text GameObject
        GameObject textObj = new GameObject("StaircaseDialogueText");
        textObj.transform.SetParent(canvasObject.transform, false);
        spawnedObjects.Add(textObj);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.enableWordWrapping = true;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = entry.alignment;
        TMP_FontAsset entryFont = entry.font != null ? entry.font : font;
        if (entryFont != null) tmp.font = entryFont;
        tmp.text = "";
        tmp.maxVisibleCharacters = 0;

        // Wait a frame so Unity's layout processes the canvas
        yield return null;

        // Now configure rect and set text
        RectTransform rect = tmp.rectTransform;

        float minX = entry.anchorMinX;
        float minY = entry.anchorMinY;
        float maxX = entry.anchorMaxX;
        float maxY = entry.anchorMaxY;

        if (entry.randomizePosition)
        {
            // Random readable box: 40-70% width, 12-20% height
            float boxW = UnityEngine.Random.Range(0.4f, 0.7f);
            float boxH = UnityEngine.Random.Range(0.12f, 0.20f);
            // Keep within safe margins (5% from edges)
            minX = UnityEngine.Random.Range(0.05f, 0.95f - boxW);
            minY = UnityEngine.Random.Range(0.05f, 0.95f - boxH);
            maxX = minX + boxW;
            maxY = minY + boxH;
        }

        float w = (maxX - minX) * 1920f;
        float h = (maxY - minY) * 1080f;
        float px = ((minX + maxX) / 2f - 0.5f) * 1920f;
        float py = ((minY + maxY) / 2f - 0.5f) * 1080f;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(px, py);

        tmp.text = entry.text;
        tmp.ForceMeshUpdate();

        Debug.Log($"StaircaseDialogue: Rect actual size = {rect.rect.width}x{rect.rect.height}, sizeDelta = {rect.sizeDelta}");

        CanvasGroup cg = textObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        Debug.Log($"StaircaseDialogue: PlayEntry starting - '{entry.text}' (size={size}, anchor=[{entry.anchorMinX},{entry.anchorMinY}]-[{entry.anchorMaxX},{entry.anchorMaxY}])");

        // Fade in
        yield return FadeCanvasGroup(cg, 0f, 1f, textFadeInDuration);
        Debug.Log($"StaircaseDialogue: Fade in complete - '{entry.text}'");

        // Typewriter
        tmp.ForceMeshUpdate();
        int totalChars = tmp.textInfo.characterCount;
        float charDelay = 1f / typewriterSpeed;
        Debug.Log($"StaircaseDialogue: Typewriter starting - {totalChars} chars at {typewriterSpeed} cps");

        for (int i = 0; i <= totalChars; i++)
        {
            tmp.maxVisibleCharacters = i;
            yield return new WaitForSeconds(charDelay);
        }

        Debug.Log($"StaircaseDialogue: Typewriter done, holding for {entry.displayDuration}s - '{entry.text}'");

        // Hold
        yield return new WaitForSeconds(entry.displayDuration);

        // Fade out
        yield return FadeCanvasGroup(cg, 1f, 0f, textFadeOutDuration);
        Debug.Log($"StaircaseDialogue: Fade out complete - '{entry.text}'");

        // Cleanup
        spawnedObjects.Remove(textObj);
        Destroy(textObj);

        completedEntryCount++;
        Debug.Log($"StaircaseDialogue: Entry complete ({completedEntryCount}/{totalEntryCount})");

        // Check if all regular entries are done
        if (completedEntryCount >= totalEntryCount && !string.IsNullOrEmpty(finalText))
        {
            Debug.Log("StaircaseDialogue: All entries complete, starting final text");
            StartCoroutine(PlayFinalText());
        }
    }

    private IEnumerator PlayFinalText()
    {
        // Create final text GameObject - use center anchor with explicit pixel size
        GameObject textObj = new GameObject("StaircaseFinalText");
        textObj.transform.SetParent(canvasObject.transform, false);
        spawnedObjects.Add(textObj);

        RectTransform rect = textObj.GetComponent<RectTransform>();
        if (rect == null) rect = textObj.AddComponent<RectTransform>();

        // Final text position: bottom-center (0.1,0.05) -> (0.9,0.25)
        float width = 0.8f * 1920f;  // 1536
        float height = 0.2f * 1080f;  // 216
        float posX = 0f;  // centered
        float posY = (0.15f - 0.5f) * 1080f;  // -378

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(posX, posY);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.enableWordWrapping = true;
        tmp.fontSize = finalTextFontSize;
        tmp.color = finalTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
        tmp.text = finalText;
        tmp.maxVisibleCharacters = 0;

        CanvasGroup cg = textObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        Debug.Log($"StaircaseDialogue: PlayFinalText starting - '{finalText}' (size={finalTextFontSize})");

        // Fade in
        yield return FadeCanvasGroup(cg, 0f, 1f, textFadeInDuration);

        // Typewriter
        tmp.ForceMeshUpdate();
        int totalChars = tmp.textInfo.characterCount;
        float charDelay = 1f / typewriterSpeed;

        for (int i = 0; i <= totalChars; i++)
        {
            tmp.maxVisibleCharacters = i;
            yield return new WaitForSeconds(charDelay);
        }

        Debug.Log($"StaircaseDialogue: Final text typewriter done, holding for {finalTextDisplayDuration}s");

        // Hold
        yield return new WaitForSeconds(finalTextDisplayDuration);

        // Fade out
        yield return FadeCanvasGroup(cg, 1f, 0f, textFadeOutDuration);

        // Cleanup
        spawnedObjects.Remove(textObj);
        Destroy(textObj);

        Debug.Log("StaircaseDialogue: Final text complete, ending sequence");
        EndSequence();
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (cg != null)
            {
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            }
            yield return null;
        }

        if (cg != null)
        {
            cg.alpha = to;
        }
    }

    private void EndSequence()
    {
        Debug.Log("StaircaseDialogue: EndSequence called");
        sequenceActive = false;

        if (infiniteStaircase != null)
        {
            infiniteStaircase.onPlayerLooped.RemoveListener(OnPlayerLooped);
        }

        StopAllCoroutines();

        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();

        if (canvasObject != null)
        {
            Destroy(canvasObject);
            canvasObject = null;
            canvas = null;
        }
    }

    private void OnDisable()
    {
        if (sequenceActive)
        {
            EndSequence();
        }
    }

    private void OnDestroy()
    {
        if (sequenceActive)
        {
            EndSequence();
        }
    }
}
