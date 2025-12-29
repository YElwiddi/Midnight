using UnityEngine;

public class TeleportInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform teleportDestination;
    [SerializeField] private string interactionPrompt = "Enter";

    public void Interact()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && teleportDestination != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                player.transform.position = teleportDestination.position;
                cc.enabled = true;
            }
            else
            {
                player.transform.position = teleportDestination.position;
            }
        }
    }

    public string GetInteractionPrompt()
    {
        return interactionPrompt;
    }
}
