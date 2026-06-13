using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a one-time self-dialogue the first time every maintained (sanity-registered)
/// lamp in the scene is lit at once - i.e. the player has turned on all the graveyard
/// lamps they were told to. Used for the opening payoff beat
/// "That should be it. I need to keep these on." after lighting both VicLamps.
///
/// Listens to the global SanityEvents.onLampStateChanged signal (fired by
/// LanternInteractable) and checks SanityManager's registered/unlit counts, so it needs
/// no per-lamp wiring and automatically tracks whichever lamps are registered for the
/// sanity "keep them lit" mechanic. Display is delegated to SimpleDialogueTrigger.ShowDialogue.
/// </summary>
public class MaintainedLampsLitDialogueCue : MonoBehaviour
{
    [Header("Dialogue")]
    [TextArea(2, 5)]
    [SerializeField] private string dialogueText = "That should be it. I need to keep these on.";
    [SerializeField] private string speakerName = "";

    [Tooltip("Seconds to wait after the last lamp is lit before the line shows")]
    [SerializeField] private float delayBeforeShow = 0f;

    [Tooltip("How long the line stays on screen after typing finishes")]
    [SerializeField] private float displayDuration = 4f;

    [Tooltip("Characters per second (0 = instant)")]
    [SerializeField] private float typewriterSpeed = 30f;

    [Header("Condition")]
    [Tooltip("Don't fire until at least this many maintained lamps exist (guards against firing before both have registered)")]
    [SerializeField] private int minimumLamps = 2;

    [Header("Behaviour")]
    [Tooltip("Only ever play this line once (the first time all lamps are lit)")]
    [SerializeField] private bool playOnce = true;

    private bool hasPlayed = false;
    private bool subscribed = false;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // Managers set their singletons in Awake; Start runs after all Awakes, so the
        // GameEventsManager is available here even if it wasn't during OnEnable.
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (subscribed && GameEventsManager.instance != null && GameEventsManager.instance.sanityEvents != null)
        {
            GameEventsManager.instance.sanityEvents.onLampStateChanged -= HandleLampStateChanged;
        }
        subscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        if (GameEventsManager.instance == null || GameEventsManager.instance.sanityEvents == null) return;

        GameEventsManager.instance.sanityEvents.onLampStateChanged += HandleLampStateChanged;
        subscribed = true;
    }

    private void HandleLampStateChanged(string lampName, bool isLit)
    {
        if (playOnce && hasPlayed) return;
        if (!isLit) return; // only relevant when a lamp has just been turned ON

        SanityManager sanity = SanityManager.Instance;
        if (sanity == null) return;

        // Require all maintained lamps to be lit (and that there are actually enough of them).
        if (sanity.GetRegisteredLanterns().Count < minimumLamps) return;
        if (sanity.GetUnlitLampCount() != 0) return;

        hasPlayed = true;

        if (delayBeforeShow > 0f)
        {
            StartCoroutine(ShowAfterDelay());
        }
        else
        {
            SimpleDialogueTrigger.ShowDialogue(dialogueText, speakerName, displayDuration, typewriterSpeed);
        }
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeShow);
        SimpleDialogueTrigger.ShowDialogue(dialogueText, speakerName, displayDuration, typewriterSpeed);
    }
}
