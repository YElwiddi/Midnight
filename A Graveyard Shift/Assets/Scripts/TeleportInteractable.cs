using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class TeleportInteractable : MonoBehaviour, IInteractable
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform teleportDestination;
    [SerializeField] private string interactionPrompt = "Enter";
    [SerializeField] private bool setPlayerRotation = false;
    [SerializeField] private Vector3 targetRotation;

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration = 2f;
    [SerializeField] private AudioClip teleportSound;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("Ambient Sound")]
    [Tooltip("Set to true if the destination is indoors (cabin, house, etc.)")]
    [SerializeField] private bool destinationIsIndoor = false;

    [Header("Events")]
    [Tooltip("Fired after the teleport sequence completes (after fade back in)")]
    public UnityEvent onTeleportComplete;

    private static Image fadeOverlay;
    private static Canvas fadeCanvas;
    private static bool isTransitioning = false;

    public void Interact()
    {
        if (isTransitioning) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && teleportDestination != null)
        {
            StartCoroutine(TeleportSequence(player));
        }
    }

    private IEnumerator TeleportSequence(GameObject player)
    {
        isTransitioning = true;

        // Disable player movement and camera
        Movement movement = player.GetComponent<Movement>();
        CharacterController cc = player.GetComponent<CharacterController>();

        if (movement != null)
        {
            movement.DisableAllInput();
        }

        // Create fade overlay if it doesn't exist
        EnsureFadeOverlay();

        // Play sound as 2D (non-positional) so it doesn't cut off when teleporting
        if (teleportSound != null)
        {
            PlaySound2D(teleportSound);
        }

        // Fade to black
        float halfDuration = transitionDuration / 2f;
        yield return StartCoroutine(Fade(0f, 1f, halfDuration));

        // Teleport player while screen is black
        // Temporarily disable CharacterController for position change
        if (cc != null) cc.enabled = false;
        player.transform.position = teleportDestination.position;
        if (setPlayerRotation)
        {
            player.transform.rotation = Quaternion.Euler(targetRotation);
        }
        if (cc != null) cc.enabled = true;

        // Update ambient sound for indoor/outdoor transition
        if (destinationIsIndoor)
        {
            AmbientSoundManager.Instance?.EnterIndoor();
        }
        else
        {
            AmbientSoundManager.Instance?.ExitIndoor();
        }

        // Small delay at full black
        yield return new WaitForSeconds(0.1f);

        // Fade back to normal
        yield return StartCoroutine(Fade(1f, 0f, halfDuration));

        // Re-enable player movement and camera
        if (movement != null)
        {
            movement.EnableAllInput();
        }

        isTransitioning = false;

        // Fire completion event
        onTeleportComplete?.Invoke();
    }

    private void EnsureFadeOverlay()
    {
        if (fadeCanvas == null)
        {
            // Create canvas
            GameObject canvasObj = new GameObject("TeleportFadeCanvas");
            fadeCanvas = canvasObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();

            // Create image
            GameObject imageObj = new GameObject("FadeOverlay");
            imageObj.transform.SetParent(canvasObj.transform, false);
            fadeOverlay = imageObj.AddComponent<Image>();
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

            // Make it cover the whole screen
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

    private void PlaySound2D(AudioClip clip)
    {
        GameObject tempAudio = new GameObject("TempAudio");
        AudioSource audioSource = tempAudio.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.spatialBlend = 0f; // 2D sound
        audioSource.Play();
        Destroy(tempAudio, clip.length);
    }
}
