using UnityEngine;
using System.Collections.Generic;

// This script sets up a dialogue for a mysterious stranger NPC
public class CarDialogue : MonoBehaviour
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
        
        
        // Initialize both dialogue trees
        SetUpCarWontStartDialogue();
        
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
    
    // This method will be called when the player interacts with the NPC
    private void OnInteractionStarted()
    {

        dialogueSystem.SetActiveDialogueTree(regularDialogueIndex);
        
    }
    
    void SetUpCarWontStartDialogue()
    {
        // Create a dialogue tree for the stranger
        DialogueTree carDialogue = new DialogueTree();
        //strangerDialogue.dialogueName = "Mysterious Stranger";
        
        // Node 0: Initial greeting
        DialogueNode greetingNode = new DialogueNode();
        greetingNode.id = 0;
        greetingNode.npcText = "Huh?";
        
        greetingNode.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 1 
        });
        
        DialogueNode questionNode = new DialogueNode();
        questionNode.id = 1;
        questionNode.npcText = "My car won't start...";
        
        questionNode.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 2 
        });
        
        DialogueNode locationNode = new DialogueNode();
        locationNode.id = 2;
        locationNode.npcText = "Was that a sound under the hood?";
        
        // Only one option for Node 2 - continue
        locationNode.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 3 
        });
        
        // Node 3: Response to "Goodbye"
        DialogueNode goodbyeNode = new DialogueNode();
        goodbyeNode.id = 3;
        goodbyeNode.npcText = "I think I see a trailer nearby. Maybe they can help.";
        goodbyeNode.isEndNode = true; // This ends the dialogue
        

        // Add all nodes to the dialogue tree
        carDialogue.nodes.Add(greetingNode);
        carDialogue.nodes.Add(questionNode);
        carDialogue.nodes.Add(locationNode);
        carDialogue.nodes.Add(goodbyeNode);
        
        // Add the dialogue tree to the dialogue system
        dialogueSystem.dialogueTrees.Add(carDialogue);
    }
}