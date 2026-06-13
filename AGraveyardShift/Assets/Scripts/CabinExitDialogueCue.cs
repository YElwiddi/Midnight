using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a one-time self-dialogue a short delay after the player exits through this
/// object's <see cref="TeleportInteractable"/>. Used for the opening cabin-exit beat:
/// after the intro cinematic the player leaves the cabin and, ~2s later, the line
/// "I should get these lights on right away..." appears for a few seconds.
///
/// Subscribes to TeleportInteractable.onTeleportComplete in code so no UnityEvent
/// wiring is required, and delegates the actual display to SimpleDialogueTrigger.ShowDialogue.
/// </summary>
[RequireComponent(typeof(TeleportInteractable))]
public class CabinExitDialogueCue : MonoBehaviour
{
    [Header("Dialogue")]
    [TextArea(2, 5)]
    [SerializeField] private string dialogueText = "I should get these lights on right away...";
    [SerializeField] private string speakerName = "";

    [Tooltip("Seconds to wait after the exit teleport completes before the line shows")]
    [SerializeField] private float delayAfterExit = 2f;

    [Tooltip("How long the line stays on screen after typing finishes")]
    [SerializeField] private float displayDuration = 5f;

    [Tooltip("Characters per second (0 = instant)")]
    [SerializeField] private float typewriterSpeed = 30f;

    [Header("Behaviour")]
    [Tooltip("Only ever play this line once (the first time the player exits)")]
    [SerializeField] private bool playOnce = true;

    private TeleportInteractable door;
    private bool hasPlayed = false;

    private void Awake()
    {
        door = GetComponent<TeleportInteractable>();
    }

    private void OnEnable()
    {
        if (door != null && door.onTeleportComplete != null)
        {
            door.onTeleportComplete.AddListener(HandleExit);
        }
    }

    private void OnDisable()
    {
        if (door != null && door.onTeleportComplete != null)
        {
            door.onTeleportComplete.RemoveListener(HandleExit);
        }
    }

    private void HandleExit()
    {
        if (playOnce && hasPlayed) return;
        hasPlayed = true;
        StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        if (delayAfterExit > 0f)
        {
            yield return new WaitForSeconds(delayAfterExit);
        }

        SimpleDialogueTrigger.ShowDialogue(dialogueText, speakerName, displayDuration, typewriterSpeed);
    }
}
