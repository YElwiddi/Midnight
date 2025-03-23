using UnityEngine;
using System.Collections.Generic;

// This script sets up a dialogue for the player talking to themselves in a mansion
public class TalkToSelfMansion : MonoBehaviour
{
    private DialogueSystem dialogueSystem;
    private PlayerInventory playerInventory;
    
    // Index references for our dialogue trees
    private int regularDialogueIndex = 0;
    
    void Start()
    {
        // Get reference to the DialogueSystem component
        dialogueSystem = GetComponent<DialogueSystem>();
        
        // Get the PlayerInventory component from the player
        playerInventory = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerInventory>();
        
        if (dialogueSystem == null)
        {
            Debug.LogError("DialogueSystem component not found on this game object!");
            return;
        }
        
        
        // Initialize the dialogue tree
        SetUpMansionSelfTalkDialogue();
        
        // Subscribe to the DialogueSystem's interact event
        dialogueSystem.onCustomInteract += OnInteractionStarted;
    }
    
    private void OnDestroy()
    {
        // Clean up event subscription when this object is destroyed
        if (dialogueSystem != null)
        {
            dialogueSystem.onCustomInteract -= OnInteractionStarted;
        }
    }
    
    // This method will be called when the player interacts with the trigger
    private void OnInteractionStarted()
    {
        dialogueSystem.SetActiveDialogueTree(regularDialogueIndex);
    }
    
    void SetUpMansionSelfTalkDialogue()
    {
        // Create a dialogue tree for self-talk
        DialogueTree mansionDialogue = new DialogueTree();
        
        // Node 0: Initial thought
        DialogueNode firstThoughtNode = new DialogueNode();
        firstThoughtNode.id = 0;
        firstThoughtNode.npcText = "This house looks abandoned.";
        
        firstThoughtNode.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 1 
        });
        
        // Node 1: Second thought
        DialogueNode secondThoughtNode = new DialogueNode();
        secondThoughtNode.id = 1;
        secondThoughtNode.npcText = "I think I heard someone inside... Is that Jane's voice? It sounded muffled.";
        
        secondThoughtNode.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 2 
        });
        
        // Node 2: Final thought
        DialogueNode finalThoughtNode = new DialogueNode();
        finalThoughtNode.id = 2;
        finalThoughtNode.npcText = "I don't see her...";
        finalThoughtNode.isEndNode = true; // This ends the dialogue
        
        // Add all nodes to the dialogue tree
        mansionDialogue.nodes.Add(firstThoughtNode);
        mansionDialogue.nodes.Add(secondThoughtNode);
        mansionDialogue.nodes.Add(finalThoughtNode);
        
        // Add the dialogue tree to the dialogue system
        dialogueSystem.dialogueTrees.Add(mansionDialogue);
    }
}