using UnityEngine;

/// <summary>
/// ScriptableObject configuration for dig penalty system.
/// Controls speed multipliers, direct pursuit, and shovel breaking behavior.
/// </summary>
[CreateAssetMenu(fileName = "DigPenaltyConfig", menuName = "Familiar/Dig Penalty Config")]
public class DigPenaltyConfig : ScriptableObject
{
    [Header("Speed Penalties")]
    [Tooltip("Speed multiplier after 1st incorrect dig (1.2 = 20% faster)")]
    public float firstPenaltyMultiplier = 1.2f;

    [Tooltip("Speed multiplier after 2nd incorrect dig (1.3 = 30% faster)")]
    public float secondPenaltyMultiplier = 1.3f;

    [Tooltip("Speed multiplier after 3rd incorrect dig (1.5 = 50% faster)")]
    public float thirdPenaltyMultiplier = 1.5f;

    [Header("Direct Pursuit")]
    [Tooltip("Enable direct pursuit (killer always knows location) after this many incorrect digs")]
    public int directPursuitThreshold = 3;

    [Header("Shovel Breaking")]
    [Tooltip("If true, shovel breaks after reaching the max penalty threshold")]
    public bool breakShovelOnMaxPenalty = false;

    [Tooltip("Dialogue shown when shovel breaks")]
    [TextArea(2, 4)]
    public string shovelBrokenDialogue = "My shovel broke...";

    [Tooltip("Dialogue shown when trying to dig with broken shovel")]
    [TextArea(2, 4)]
    public string tryDigWithBrokenShovelDialogue = "The shovel is broken. I can't dig anymore.";
}
