using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a brief, inhuman neck-bend "snap" (with a bone-crack sound) when this NPC
/// reaches a specific dialogue choice screen: her neck bends ~90 degrees to the side
/// (the head follows it over), held for about half a second, then snaps back to normal
/// as if nothing happened.
///
/// Built for Maribel (TabithaSister): when the player reaches her "May I come in?"
/// decision screen (let her in / refuse / a few more questions / be right back), the snap
/// is armed. A fixed delay later (default 12s) her neck cracks sideways — regardless of
/// which choice the player picks or which dialogue node they move to next, and even if the
/// conversation has ended by then. The countdown is not cancelled by progressing the dialogue.
///
/// Place this on the NPC root (the transform DialogueManager treats as the current
/// speaker - for event NPCs that is the spawned prefab root). The neck bone is rotated
/// in LateUpdate so the bend wins over the Animator each frame while it is active. The
/// bend pivots at the base of the neck, so the head tips down-and-out to the side; it
/// does NOT lift the head up.
/// </summary>
public class CreepyNeckSnap : MonoBehaviour
{
    [Header("Dialogue Trigger")]
    [Tooltip("The snap arms when THIS NPC is the active speaker and the presented choices contain this exact text. " +
             "Maribel's 'May I come in?' decision screen is uniquely identified by 'Fine. Go in. But be quick.'.")]
    [SerializeField] private string triggerChoiceText = "Fine. Go in. But be quick.";

    [Tooltip("Also require the choice list to have exactly this many options. Set 0 to ignore the count.")]
    [SerializeField] private int requiredChoiceCount = 4;

    [Tooltip("Seconds to wait after the trigger screen is reached before the neck snaps. " +
             "The countdown keeps running no matter which node the player moves to next — and even if the dialogue ends.")]
    [SerializeField] private float delayBeforeSnap = 12f;

    [Tooltip("Only arm once per conversation (re-arms when the dialogue ends). A snap already counting down is never cancelled.")]
    [SerializeField] private bool oncePerConversation = true;

    [Header("Neck Bend")]
    [Tooltip("Neck bone to bend. Auto-found by name in children if left unset.")]
    [SerializeField] private Transform neckBone;

    [Tooltip("Bone name to search for when 'Neck Bone' is not assigned.")]
    [SerializeField] private string neckBoneName = "mixamorig:Neck";

    [Tooltip("How far the neck bends, in degrees, to the side (rotation about the character's forward axis, so the head " +
             "tips sideways from the player's view). 90 = head fully horizontal onto the shoulder. Negative bends the other way.")]
    [SerializeField] private float bendAngleDegrees = 90f;

    [Header("Timing (total hold ~0.5s)")]
    [SerializeField] private float snapInDuration = 0.06f;
    [SerializeField] private float holdDuration = 0.4f;
    [SerializeField] private float snapOutDuration = 0.12f;

    [Header("Sound")]
    [SerializeField] private AudioClip boneCrackSound;
    [Range(0f, 1f)][SerializeField] private float volume = 1f;
    [Tooltip("0 = 2D (always clearly audible during the close-up), 1 = fully 3D/positional.")]
    [Range(0f, 1f)][SerializeField] private float spatialBlend = 0f;

    [Tooltip("Play the bone-crack this many seconds BEFORE the neck snaps, as an anticipatory cue. " +
             "0 = plays at the exact instant of the snap. Capped at 'Delay Before Snap' (it can't lead earlier than the " +
             "trigger screen appearing). The snap itself still fires at 'Delay Before Snap'.")]
    [SerializeField] private float soundLeadTime = 0f;

    private DialogueManager dialogueManager;
    private AudioSource audioSource;
    private Coroutine pendingSnap;
    private bool isBending;
    private bool firedThisConversation;
    private float bendWeight; // 0..1 strength currently applied in LateUpdate

    private void Awake()
    {
        if (neckBone == null) neckBone = FindBone(transform, neckBoneName);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = spatialBlend;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // DialogueManager may not have existed yet at OnEnable (NPC spawned early).
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (dialogueManager != null)
        {
            dialogueManager.OnChoicesPresented -= HandleChoicesPresented;
            dialogueManager.OnDialogueTextChanged -= HandleDialogueTextChanged;
            dialogueManager.OnDialogueEnded -= HandleDialogueEnded;
        }
        dialogueManager = null;
    }

    private void TrySubscribe()
    {
        if (dialogueManager != null) return;
        dialogueManager = DialogueManager.GetInstance();
        if (dialogueManager == null) return;

        dialogueManager.OnChoicesPresented += HandleChoicesPresented;
        dialogueManager.OnDialogueTextChanged += HandleDialogueTextChanged;
        dialogueManager.OnDialogueEnded += HandleDialogueEnded;
    }

    private void HandleDialogueTextChanged(string _)
    {
        // Intentionally empty: once the snap is armed at the trigger screen, progressing to
        // another dialogue node must NOT cancel it — it still fires delayBeforeSnap later.
    }

    private void HandleDialogueEnded()
    {
        // Re-arm for a future conversation, but DON'T cancel a snap already counting down:
        // it should still fire delayBeforeSnap after the trigger node even if the player
        // ends the conversation first.
        firedThisConversation = false;
    }

    private void HandleChoicesPresented(List<string> choices)
    {
        // Only arm when THIS NPC is the one being talked to and the trigger screen is shown.
        if (dialogueManager == null || dialogueManager.CurrentNPC != transform) return;

        bool matches = choices != null
            && (requiredChoiceCount <= 0 || choices.Count == requiredChoiceCount)
            && (string.IsNullOrEmpty(triggerChoiceText) || choices.Contains(triggerChoiceText));
        if (!matches) return;

        // Arm once. After this the countdown owns the snap and fires regardless of where the
        // player navigates next (or whether the conversation ends).
        if (oncePerConversation && firedThisConversation) return;
        if (pendingSnap != null) return;

        firedThisConversation = true;
        pendingSnap = StartCoroutine(DelayedSnap());
    }

    private IEnumerator DelayedSnap()
    {
        float t = 0f;
        bool crackPlayed = false;
        // Moment within the countdown to fire the crack, so it leads the snap by soundLeadTime.
        float crackAt = Mathf.Clamp(delayBeforeSnap - soundLeadTime, 0f, delayBeforeSnap);

        // Count down independently of the dialogue: the snap fires delayBeforeSnap after the
        // trigger node was reached, no matter which node the player moves to (or if it ends).
        while (t < delayBeforeSnap)
        {
            // Anticipatory crack: play it ahead of the snap when a lead time is set.
            if (!crackPlayed && soundLeadTime > 0f && t >= crackAt)
            {
                PlayCrack();
                crackPlayed = true;
            }

            t += Time.deltaTime;
            yield return null;
        }

        pendingSnap = null;
        // Don't replay the crack in the snap if the lead time already played it.
        yield return SnapRoutine(!crackPlayed);
    }

    /// <summary>Trigger the neck snap immediately (for testing or other scripted moments).</summary>
    public void TriggerNeckSnap()
    {
        if (!isBending) StartCoroutine(SnapRoutine());
    }

    private void PlayCrack()
    {
        if (boneCrackSound == null) return;
        audioSource.spatialBlend = spatialBlend;
        audioSource.PlayOneShot(boneCrackSound, volume);
    }

    private IEnumerator SnapRoutine(bool playSound = true)
    {
        if (neckBone == null) neckBone = FindBone(transform, neckBoneName);
        if (neckBone == null)
        {
            Debug.LogWarning($"CreepyNeckSnap: neck bone '{neckBoneName}' not found on '{name}'.");
            yield break;
        }

        isBending = true;

        // The crack lands as the neck snaps - unless a lead time already played it.
        if (playSound) PlayCrack();

        // Fast snap in.
        float t = 0f;
        while (t < snapInDuration)
        {
            t += Time.deltaTime;
            bendWeight = Mathf.SmoothStep(0f, 1f, t / snapInDuration);
            yield return null;
        }
        bendWeight = 1f;

        // Hold the inhuman angle.
        if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

        // Ease back to normal.
        t = 0f;
        while (t < snapOutDuration)
        {
            t += Time.deltaTime;
            bendWeight = Mathf.SmoothStep(1f, 0f, t / snapOutDuration);
            yield return null;
        }

        bendWeight = 0f;
        isBending = false;
    }

    private void LateUpdate()
    {
        // Apply the bend AFTER the Animator has written the neck pose this frame, so the
        // snap visibly wins over the playing animation. Rotating about the character's
        // forward axis tips the head sideways (from the player's view) while pivoting at
        // the base of the neck - the head goes down-and-out, never up.
        if (bendWeight <= 0f || neckBone == null) return;
        neckBone.rotation = Quaternion.AngleAxis(bendAngleDegrees * bendWeight, transform.forward) * neckBone.rotation;
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == boneName) return t;
        }
        return null;
    }
}
