using UnityEngine;

/// <summary>
/// The chain and padlock round the back gate. While both leaves are shut the chain is wrapped round the meeting bars;
/// the moment either leaf opens (the player's key, or the gate creaking open at low protection) it drops to the ground
/// with the padlock sprung, and stays there.
/// </summary>
public class GateChainLock : MonoBehaviour
{
    [Tooltip("The back gate's leaves")]
    public SwingingGate[] leaves;

    [Tooltip("Chain wrapped round the shut leaves, with the padlock")]
    public GameObject lockedChain;

    [Tooltip("Chain lying on the ground with the padlock open")]
    public GameObject droppedChain;

    private bool dropped;

    private void Start()
    {
        Show(false);
    }

    private void Update()
    {
        if (dropped || leaves == null) return;
        foreach (var leaf in leaves)
        {
            if (leaf != null && leaf.IsOpen)
            {
                dropped = true;
                Show(true);
                return;
            }
        }
    }

    private void Show(bool isDropped)
    {
        if (lockedChain != null) lockedChain.SetActive(!isDropped);
        if (droppedChain != null) droppedChain.SetActive(isDropped);
    }
}
