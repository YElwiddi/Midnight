using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a menu modal from its list into a detail page on the same stone slab. While the detail is
/// open, every other child of the detail's parent (title, count, slots, BACK) is hidden, so the two
/// pages never draw over each other; closing it brings back exactly what it hid. Used by the
/// Crypt Fiends and Field Notes panels.
/// </summary>
public sealed class ModalDetailPage
{
    private readonly List<GameObject> hidden = new List<GameObject>();

    public bool IsOpen { get; private set; }

    public void Open(GameObject detail)
    {
        if (detail == null) return;
        if (!IsOpen && detail.transform.parent != null)
        {
            foreach (Transform sibling in detail.transform.parent)
            {
                var go = sibling.gameObject;
                if (go == detail || !go.activeSelf) continue;
                hidden.Add(go);
                go.SetActive(false);
            }
        }
        IsOpen = true;
        detail.SetActive(true);
    }

    public void Close(GameObject detail)
    {
        if (detail != null) detail.SetActive(false);
        foreach (var go in hidden)
            if (go != null) go.SetActive(true);
        hidden.Clear();
        IsOpen = false;
    }
}
