using UnityEngine;

/// <summary>
/// Now and then someone is standing behind the menu's front gate. Each time the menu opens there
/// is a small chance the visitor (the Spirit, its face caught by the lamp) is there; the next time
/// it usually isn't. Never on the first few visits.
/// </summary>
public class MenuGateVisitor : MonoBehaviour
{
    [Tooltip("The visitor (model + face light). Inactive by default.")]
    public GameObject visitor;
    [Range(0f, 1f)] public float chance = 0.2f;
    [Tooltip("Menu visits before the visitor can appear at all.")]
    public int quietVisits = 2;
    [Tooltip("Animator state to hold (the model's idle).")]
    public string idleState = "Idle";

    private const string VisitsKey = "MenuVisits";

    private void Start()
    {
        int visits = PlayerPrefs.GetInt(VisitsKey, 0) + 1;
        PlayerPrefs.SetInt(VisitsKey, visits);

        bool show = visits > quietVisits && Random.value < chance;
        if (visitor == null) return;
        visitor.SetActive(show);
        if (!show) return;

        var anim = visitor.GetComponentInChildren<Animator>();
        if (anim != null && anim.HasState(0, Animator.StringToHash(idleState)))
            anim.Play(idleState, 0, Random.value);
    }
}
