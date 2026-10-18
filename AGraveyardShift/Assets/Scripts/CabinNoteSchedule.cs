using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drip-feeds the cabin notes over the night so the player isn't facing every file at once.
/// Each note appears when its phase begins and then stays (notes never disappear); a phase-select
/// start shows everything up to the chosen phase. When new notes appear mid-run the player gets a
/// short thought line. If the player is standing near the notes at that moment, the reveal waits
/// until they walk away so nothing pops in on screen.
/// </summary>
public class CabinNoteSchedule : MonoBehaviour
{
    [System.Serializable]
    public class ScheduledNote
    {
        [Tooltip("The note object (with its ReadableTrigger)")]
        public GameObject note;

        [Tooltip("Phase in which the note appears (1 = there from the start of the night)")]
        public int appearsInPhase = 1;
    }

    [SerializeField] private List<ScheduledNote> notes = new List<ScheduledNote>();

    [Header("New Notes Cue")]
    [Tooltip("Thought line shown when new notes appear mid-run")]
    [TextArea(1, 3)]
    [SerializeField] private string newNotesText = "Someone left more notes in the cabin.";

    [SerializeField] private float newNotesDisplayDuration = 4f;
    [SerializeField] private float newNotesTypewriterSpeed = 30f;

    [Tooltip("If the player is this close to a note when it should appear, the reveal waits until they move away")]
    [SerializeField] private float revealDeferDistance = 15f;

    private Transform player;
    private int shownPhase;
    private Coroutine revealRoutine;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        int currentPhase = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentPhase : 0;
        shownPhase = Mathf.Max(PhaseSelect.StartPhase, currentPhase);

        foreach (ScheduledNote n in notes)
        {
            if (n != null && n.note != null) n.note.SetActive(n.appearsInPhase <= shownPhase);
        }

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnDestroy()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void HandlePhaseChanged(int phase)
    {
        if (phase <= shownPhase) return;
        shownPhase = phase;

        if (revealRoutine == null && HasPendingNotes())
        {
            revealRoutine = StartCoroutine(RevealWhenUnseen());
        }
    }

    private bool HasPendingNotes()
    {
        foreach (ScheduledNote n in notes)
        {
            if (IsPending(n)) return true;
        }
        return false;
    }

    private bool IsPending(ScheduledNote n)
    {
        return n != null && n.note != null && n.appearsInPhase <= shownPhase && !n.note.activeSelf;
    }

    private IEnumerator RevealWhenUnseen()
    {
        while (PlayerNearPendingNote())
        {
            yield return new WaitForSeconds(0.5f);
        }

        var revealed = new List<GameObject>();
        foreach (ScheduledNote n in notes)
        {
            if (!IsPending(n)) continue;
            n.note.SetActive(true);
            revealed.Add(n.note);
        }

        // A visitor whose event already started while the reveal waited should still light up its note
        foreach (GameObject go in revealed)
        {
            NotePulseHighlight pulse = go.GetComponent<NotePulseHighlight>();
            if (pulse != null) pulse.SyncWithCurrentEvent();
        }

        revealRoutine = null;
        if (revealed.Count > 0)
        {
            yield return ShowNewNotesCue();
        }
    }

    private bool PlayerNearPendingNote()
    {
        if (player == null) return false;

        foreach (ScheduledNote n in notes)
        {
            if (IsPending(n) && Vector3.Distance(player.position, n.note.transform.position) < revealDeferDistance)
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerator ShowNewNotesCue()
    {
        if (string.IsNullOrEmpty(newNotesText)) yield break;

        // The thought line shares the dialogue box: wait out conversations, notes and other thoughts
        float giveUpAt = Time.time + 30f;
        while (Time.time < giveUpAt)
        {
            DialogueManager dialogueManager = DialogueManager.GetInstance();
            bool busy = (dialogueManager != null && dialogueManager.IsDialoguePlaying())
                        || (ReadableUI.Instance != null && ReadableUI.Instance.IsOpen);

            if (!busy && SimpleDialogueTrigger.ShowDialogue(newNotesText, "", newNotesDisplayDuration, newNotesTypewriterSpeed))
            {
                yield break;
            }
            yield return new WaitForSeconds(1f);
        }
    }
}
