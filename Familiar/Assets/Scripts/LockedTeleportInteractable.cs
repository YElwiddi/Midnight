using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LockedTeleportInteractable : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    [Tooltip("The item ID required to unlock (e.g., 'crypt_key', 'library_key')")]
    [SerializeField] private string requiredItemId = "crypt_key";
    [SerializeField] private bool consumeItemOnUse = true;

    [Header("Locked Dialogue")]
    [TextArea(3, 10)]
    [SerializeField] private string lockedDialogueText = "The door is locked...";
    [SerializeField] private string lockedSpeakerName = "";
    [SerializeField] private float lockedDialogueDuration = 3f;

    [Header("Typewriter Effect")]
    [SerializeField] private bool useTypewriterEffect = true;
    [Tooltip("Characters per second")]
    [SerializeField] private float typewriterSpeed = 30f;

    [Header("Teleport Settings")]
    [SerializeField] private Transform teleportDestination;
    [SerializeField] private string interactionPrompt = "Enter";
    [SerializeField] private bool setPlayerRotation = false;
    [SerializeField] private Vector3 targetRotation;

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 2f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("References")]
    [SerializeField] private DialogueUI dialogueUI;

    private static Image fadeOverlay;
    private static Canvas fadeCanvas;
    private static bool isTransitioning = false;
    private bool isShowingDialogue = false;

    private void Start()
    {
        if (dialogueUI == null)
        {
            dialogueUI = FindFirstObjectByType<DialogueUI>();
        }
    }

    public void Interact()
    {
        if (isTransitioning || isShowingDialogue) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || teleportDestination == null) return;

        HeldItem heldItem = player.GetComponent<HeldItem>();
        bool hasRequiredItem = heldItem != null && heldItem.HasItemWithId(requiredItemId);

        if (hasRequiredItem)
        {
            if (consumeItemOnUse)
            {
                heldItem.ClearItem();
            }
            StartCoroutine(TeleportSequence(player));
        }
        else
        {
            StartCoroutine(ShowLockedDialogue());
        }
    }

    private IEnumerator ShowLockedDialogue()
    {
        if (dialogueUI == null) yield break;

        isShowingDialogue = true;

        dialogueUI.Show();

        string speaker = string.IsNullOrEmpty(lockedSpeakerName) ? null : lockedSpeakerName;

        if (useTypewriterEffect && typewriterSpeed > 0)
        {
            float delay = 1f / typewriterSpeed;
            for (int i = 1; i <= lockedDialogueText.Length; i++)
            {
                dialogueUI.SetDialogueText(lockedDialogueText.Substring(0, i), speaker);
                yield return new WaitForSeconds(delay);
            }
        }
        else
        {
            dialogueUI.SetDialogueText(lockedDialogueText, speaker);
        }

        yield return new WaitForSeconds(lockedDialogueDuration);

        dialogueUI.Hide();

        isShowingDialogue = false;
    }

    private IEnumerator TeleportSequence(GameObject player)
    {
        isTransitioning = true;

        Movement movement = player.GetComponent<Movement>();
        CharacterController cc = player.GetComponent<CharacterController>();

        if (movement != null)
        {
            movement.DisableAllInput();
        }

        EnsureFadeOverlay();

        float halfDuration = transitionDuration / 2f;
        yield return StartCoroutine(Fade(0f, 1f, halfDuration));

        if (cc != null) cc.enabled = false;
        player.transform.position = teleportDestination.position;
        if (setPlayerRotation)
        {
            player.transform.rotation = Quaternion.Euler(targetRotation);
        }
        if (cc != null) cc.enabled = true;

        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(Fade(1f, 0f, halfDuration));

        if (movement != null)
        {
            movement.EnableAllInput();
        }

        isTransitioning = false;
    }

    private void EnsureFadeOverlay()
    {
        if (fadeCanvas == null)
        {
            GameObject canvasObj = new GameObject("TeleportFadeCanvas");
            fadeCanvas = canvasObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();

            GameObject imageObj = new GameObject("FadeOverlay");
            imageObj.transform.SetParent(canvasObj.transform, false);
            fadeOverlay = imageObj.AddComponent<Image>();
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

            RectTransform rt = fadeOverlay.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            DontDestroyOnLoad(canvasObj);
        }
        else
        {
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        }
    }

    private IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        Color color = fadeOverlay.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            color.a = Mathf.Lerp(startAlpha, endAlpha, t);
            fadeOverlay.color = color;
            yield return null;
        }

        color.a = endAlpha;
        fadeOverlay.color = color;
    }

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }
}
