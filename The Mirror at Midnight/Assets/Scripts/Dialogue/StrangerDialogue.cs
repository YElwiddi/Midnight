using UnityEngine;
using System.Collections.Generic;

// This script sets up a dialogue for a mysterious stranger NPC
public class StrangerDialogue : MonoBehaviour
{
    private DialogueSystem dialogueSystem;
    private PlayerInventory playerInventory;
    
    // You can specify required items for specific dialogue branches
    [Header("Required Items")]
    public string requiredItemName;
    
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
        
        if (playerInventory == null)
        {
            Debug.LogWarning("PlayerInventory not found! Item-based dialogue will not work.");
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
       /* if (CheckForItem(requiredItemName)){
            nextNodeId = 5;
        }
        else{
            goodbyeNode.isEndNode = true;
        }*/
        //goodbyeNode.isEndNode = true; // This ends the dialogue
        
        // Node 4: Warning
        DialogueNode warningNode = new DialogueNode();
        warningNode.id = 4;
        warningNode.npcText = "You'd better be careful...";
       // warningNode.isEndNode = true; // This ends the dialogue
        
        // Node 5: Response to showing the coin (only appears if player has the coin)
        DialogueNode coinNode = new DialogueNode();
        coinNode.id = 5;
        coinNode.npcText = "Ah, you've found the cellar key. I've been looking for that.";
        
        coinNode.options.Add(new DialogueOption { 
            optionText = "What can you tell me about it?", 
            nextNodeId = 6 
        });
        
        // Node 6: Information about the coin
        DialogueNode coinInfoNode = new DialogueNode();
        coinInfoNode.id = 6;
        coinInfoNode.npcText = "That key opens the cellar beneath the old mansion. They say there are valuable treasures hidden down there, but also great danger.";
        coinInfoNode.isEndNode = true; // This ends the dialogue
        
        // Add all nodes to the dialogue tree
        strangerDialogue.nodes.Add(greetingNode);
        strangerDialogue.nodes.Add(questionNode);
        strangerDialogue.nodes.Add(locationNode);
        strangerDialogue.nodes.Add(goodbyeNode);
        strangerDialogue.nodes.Add(warningNode);
        strangerDialogue.nodes.Add(coinNode);
        strangerDialogue.nodes.Add(coinInfoNode);
        
        // Add the dialogue tree to the dialogue system
        dialogueSystem.dialogueTrees.Add(strangerDialogue);
        
        // Set active dialogue tree
        dialogueSystem.activeDialogueTreeIndex = 0;
    }
    
    // Method to refresh dialogue based on current inventory
    public void RefreshDialogue()
    {
        // Remove existing dialogue tree
        if (dialogueSystem.dialogueTrees.Count > 0)
        {
            dialogueSystem.dialogueTrees.RemoveAt(0);
        }
        
        // Set up a new dialogue tree with current inventory state
        SetupStrangerDialogue();
        
        // Reset dialogue to start
        ResetDialogue();
    }
    
    // Method to reset dialogue if needed
    public void ResetDialogue()
    {
        dialogueSystem.SetActiveDialogueTree(0); // Set active tree
        dialogueSystem.StartDialogue(); // Start from the beginning
    }
    
    // You can also add a method to check for specific items and change dialogue dynamically
    public bool CheckForItem(string itemName)
    {
        if (playerInventory != null)
        {
            return playerInventory.HasItem(itemName);
        }
        return false;
    }
}