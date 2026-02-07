using UnityEngine;

public class HeldItem : MonoBehaviour
{
    [Tooltip("The ID of the currently held item (empty = no item)")]
    [SerializeField] private string currentItemId = "";

    public string CurrentItemId => currentItemId;
    public bool HasItem => !string.IsNullOrEmpty(currentItemId);

    public void SetItem(string itemId)
    {
        currentItemId = itemId;
    }

    public void ClearItem()
    {
        currentItemId = "";
    }

    public bool HasItemWithId(string itemId)
    {
        return currentItemId == itemId;
    }
}
