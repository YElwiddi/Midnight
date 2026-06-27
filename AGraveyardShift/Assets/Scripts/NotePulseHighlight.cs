using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Makes this note gently pulse a white emissive glow while one of its associated
/// visitor events is the active event, hinting to the player that it's the relevant note.
/// Driven by the global GameFlow event signals.
///
/// Every GameEvent shares the display name "Start Event", so events are matched by
/// ScriptableObject reference via GameFlowManager.GetCurrentEvent() - never by name.
/// Killer/side events don't update that reference, so a last-pulsed guard prevents them
/// from re-triggering a note after its visitor has left.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class NotePulseHighlight : MonoBehaviour
{
    [Header("Events that make this note pulse")]
    [Tooltip("While any of these GameEvents is the active event, this note pulses its glow.")]
    [SerializeField] private GameEvent[] triggeringEvents;

    [Header("Glow")]
    [Tooltip("Glow color (kept subtle by Peak Intensity)")]
    [SerializeField] private Color glowColor = Color.white;

    [Tooltip("Peak emission intensity at the top of each pulse. Keep low for a subtle hint.")]
    [SerializeField] private float peakIntensity = 0.4f;

    [Tooltip("Seconds for one beat - the glow fading up and back down (the light coming on)")]
    [SerializeField] private float pulsePeriod = 1.2f;

    [Tooltip("Dark pause held between beats (seconds). Larger = more time between pulses.")]
    [SerializeField] private float gapBetweenPulses = 0.6f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private const string EmissionKeyword = "_EMISSION";

    private Renderer rend;
    private Material[] instancedMaterials;
    private Coroutine pulseRoutine;
    private bool subscribed;
    private GameEvent lastPulsedEvent;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // Managers set their singletons in Awake; Start guarantees they exist.
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopPulse();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;

        GameEventsManager gem = GameEventsManager.instance;
        if (gem == null || gem.gameFlowEvents == null) return;

        gem.gameFlowEvents.onEventStarted += HandleEventStarted;
        gem.gameFlowEvents.onEventCompleted += HandleEventCompleted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        GameEventsManager gem = GameEventsManager.instance;
        if (gem != null && gem.gameFlowEvents != null)
        {
            gem.gameFlowEvents.onEventStarted -= HandleEventStarted;
            gem.gameFlowEvents.onEventCompleted -= HandleEventCompleted;
        }
        subscribed = false;
    }

    private void HandleEventStarted(string _)
    {
        if (triggeringEvents == null || triggeringEvents.Length == 0) return;

        GameEvent current = GameFlowManager.Instance != null ? GameFlowManager.Instance.GetCurrentEvent() : null;
        if (current == null) return;

        // Stale re-fire (e.g. a killer event that didn't change the current visitor event).
        if (current == lastPulsedEvent) return;

        if (Array.IndexOf(triggeringEvents, current) < 0) return;

        lastPulsedEvent = current;
        StartPulse();
    }

    private void HandleEventCompleted(string _)
    {
        // Events run sequentially, so any completion while we're pulsing is our visitor leaving.
        if (pulseRoutine != null)
        {
            StopPulse();
        }
    }

    private void StartPulse()
    {
        if (rend == null || pulseRoutine != null) return;

        instancedMaterials = rend.materials; // per-renderer instances; shared materials untouched
        foreach (Material m in instancedMaterials)
        {
            if (m != null && m.HasProperty(EmissionColorId))
            {
                m.EnableKeyword(EmissionKeyword);
            }
        }

        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        while (true)
        {
            // One beat: smoothly fade 0 -> peak -> 0 over pulsePeriod seconds.
            float elapsed = 0f;
            while (elapsed < pulsePeriod)
            {
                float t = pulsePeriod > 0f
                    ? 0.5f * (1f - Mathf.Cos(elapsed * (Mathf.PI * 2f / pulsePeriod)))
                    : 1f;
                ApplyEmission(glowColor * (peakIntensity * t));
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Fully off, then hold dark for the gap between beats.
            ApplyEmission(Color.black);
            if (gapBetweenPulses > 0f)
            {
                yield return new WaitForSeconds(gapBetweenPulses);
            }
        }
    }

    private void ApplyEmission(Color emission)
    {
        if (instancedMaterials == null) return;
        foreach (Material m in instancedMaterials)
        {
            if (m != null && m.HasProperty(EmissionColorId))
            {
                m.SetColor(EmissionColorId, emission);
            }
        }
    }

    private void StopPulse()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        if (instancedMaterials != null)
        {
            foreach (Material m in instancedMaterials)
            {
                if (m == null) continue;
                if (m.HasProperty(EmissionColorId))
                {
                    m.SetColor(EmissionColorId, Color.black);
                }
                m.DisableKeyword(EmissionKeyword);
            }
        }
    }
}
