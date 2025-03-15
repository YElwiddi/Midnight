using UnityEngine;
using System.Collections.Generic;

// This script sets up a dialogue for a mysterious stranger NPC
public class StrangerDialogue : MonoBehaviour
{
    private DialogueSystem dialogueSystem;
    
    void Start()
    {
        // Get reference to the DialogueSystem component
        dialogueSystem = GetComponent<DialogueSystem>();
        
        if (dialogueSystem == null)
        {
            Debug.LogError("DialogueSystem component not found on this game object!");
            return;
        }
        
        // Initialize the dialogue tree
        SetupStrangerDialogue();
    }
    
    void SetupStrangerDialogue()
    {
        // Create a dialogue tree for the stranger
        DialogueTree strangerDialogue = new DialogueTree();
        strangerDialogue.dialogueName = "Mysterious Stranger";
        
        // Node 0: Initial greeting
        DialogueNode greetingNode = new DialogueNode();
        greetingNode.id = 0;
        greetingNode.npcText = "Hello.";
        
        // Only one option for Node 0 - continue
        greetingNode.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 1 
        });
        
        // Node 1: Question from NPC
        DialogueNode questionNode = new DialogueNode();
        questionNode.id = 1;
        questionNode.npcText = "What brings you here?";
        
        // Options for Node 1
        questionNode.options.Add(new DialogueOption { 
            optionText = "Where am I?", 
            nextNodeId = 2 
        });
        
        questionNode.options.Add(new DialogueOption { 
            optionText = "Goodbye.", 
            nextNodeId = 3 
        });
        
        // Node 2: Response to "Where am I?"
        DialogueNode locationNode = new DialogueNode();
        locationNode.id = 2;
        locationNode.npcText = "Oh, you're not from here.";
        
        // Only one option for Node 2 - continue
        locationNode.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 4 
        });
        
        // Node 3: Response to "Goodbye"
        DialogueNode goodbyeNode = new DialogueNode();
        goodbyeNode.id = 3;
        goodbyeNode.npcText = "Farewell, traveler.";
        goodbyeNode.isEndNode = true; // This ends the dialogue
        
        // Node 4: Warning
        DialogueNode warningNode = new DialogueNode();
        warningNode.id = 4;
        warningNode.npcText = "You'd better be careful...";
        warningNode.isEndNode = true; // This ends the dialogue
        
        // Add all nodes to the dialogue tree
        strangerDialogue.nodes.Add(greetingNode);
        strangerDialogue.nodes.Add(questionNode);
        strangerDialogue.nodes.Add(locationNode);
        strangerDialogue.nodes.Add(goodbyeNode);
        strangerDialogue.nodes.Add(warningNode);
        
        // Add the dialogue tree to the dialogue system
        dialogueSystem.dialogueTrees.Add(strangerDialogue);
        
        // Set active dialogue tree
        dialogueSystem.activeDialogueTreeIndex = 0;
    }
    
    // Method to reset dialogue if needed
    public void ResetDialogue()
    {
        dialogueSystem.SetActiveDialogueTree(0); // Set active tree
        dialogueSystem.StartDialogue(); // Start from the beginning
    }
}