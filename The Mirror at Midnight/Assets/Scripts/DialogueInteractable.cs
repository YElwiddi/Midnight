using UnityEngine;

public class DialogueInteractable : MonoBehaviour, IInteractable
{
    [Header("Dialogue Settings")]
    [Tooltip("The name of the Ink knot to start when interacting")]
    [SerializeField] private string knotName;
    
    [Tooltip("The text prompt shown when looking at this object")]
    [SerializeField] private string interactionPrompt = "Talk";
    
    public void Interact()
    {
        Debug.Log("Interact successful");
        // Trigger the dialogue event with the specified knot name
        if (!string.IsNullOrEmpty(knotName))
        {
            GameEventsManager.instance.dialogueEvents.EnterDialogue(knotName);
        }
        else
        {
            Debug.LogWarning("No knot name specified for dialogue interactable on " + gameObject.name);
        }
    }

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }
}