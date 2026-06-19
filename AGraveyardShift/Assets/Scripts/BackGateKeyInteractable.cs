using UnityEngine;

/// <summary>
/// The back-gate key resting in the cabin. Interacting routes to
/// <see cref="BackGateController"/>, which shows a Yes/No confirm. The controller
/// calls <see cref="Hide"/> to make the key vanish — on pickup, or automatically
/// when GraveyardProtection drops too low and the key was never taken.
/// </summary>
public class BackGateKeyInteractable : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("Controller that owns the back-gate logic. Auto-found if left empty.")]
    [SerializeField] private BackGateController controller;

    [Tooltip("Root object to deactivate when the key disappears (the whole key model). Defaults to this object.")]
    [SerializeField] private GameObject visualRoot;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "Pick up";

    /// <summary>True while the key is still present in the world.</summary>
    public bool IsVisible => visualRoot != null && visualRoot.activeSelf;

    private void Awake()
    {
        if (controller == null) controller = FindObjectOfType<BackGateController>();
        if (visualRoot == null) visualRoot = gameObject;
    }

    // IInteractable
    public void Interact()
    {
        if (controller != null) controller.OnKeyInteracted();
    }

    public string GetInteractionPrompt() => interactionPrompt;

    /// <summary>Removes the key from the world by deactivating its visual root.</summary>
    public void Hide()
    {
        if (visualRoot != null) visualRoot.SetActive(false);
    }
}
