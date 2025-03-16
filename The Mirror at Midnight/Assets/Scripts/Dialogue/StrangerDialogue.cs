using UnityEngine;
using System.Collections.Generic;

// This script sets up a dialogue for a mysterious stranger NPC
public class StrangerDialogue : MonoBehaviour
{
    private DialogueSystem dialogueSystem;
    private PlayerInventory playerInventory;
    
    [Header("Required Items")]
    public string requiredItemName = "Cellar Key";
    
    // Index references for our dialogue trees
    private int regularDialogueIndex = 0;
    private int keyDialogueIndex = 1;
    
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
        
        // Initialize both dialogue trees
        SetupStrangerDialogue();
        SetupKeyDialogue();
        
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
        // Check inventory for Cellar Key and set appropriate dialogue tree
        bool hasKey = CheckForItem(requiredItemName);
        
        if (hasKey)
        {
            Debug.Log($"Player has the {requiredItemName} - Using Key Dialogue Tree");
            dialogueSystem.SetActiveDialogueTree(keyDialogueIndex);
        }
        else
        {
            Debug.Log($"Player does not have the {requiredItemName} - Using Regular Dialogue Tree");
            dialogueSystem.SetActiveDialogueTree(regularDialogueIndex);
        }
    }
    
    void SetupStrangerDialogue()
    {
        // Create a dialogue tree for the stranger
        DialogueTree strangerDialogue = new DialogueTree();
        //strangerDialogue.dialogueName = "Mysterious Stranger";
        
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
        warningNode.npcText = "You'd better be careful... I've heard there are strange things hidden in this town.";
        warningNode.isEndNode = true; // This ends the dialogue
        
        // Add all nodes to the dialogue tree
        strangerDialogue.nodes.Add(greetingNode);
        strangerDialogue.nodes.Add(questionNode);
        strangerDialogue.nodes.Add(locationNode);
        strangerDialogue.nodes.Add(goodbyeNode);
        strangerDialogue.nodes.Add(warningNode);
        
        // Add the dialogue tree to the dialogue system
        dialogueSystem.dialogueTrees.Add(strangerDialogue);
    }
    
    void SetupKeyDialogue()
    {
        // Create a dialogue tree for when player has the key
        DialogueTree keyDialogue = new DialogueTree();
        //keyDialogue.dialogueName = "Mysterious Stranger";
        
        // Node 0: Initial reaction to the key
        DialogueNode keyReactionNode = new DialogueNode();
        keyReactionNode.id = 0;
        keyReactionNode.npcText = "Wait... that looks familiar. Is that the Cellar Key you're carrying?";
        
        // Options for key reaction
        keyReactionNode.options.Add(new DialogueOption { 
            optionText = "Yes, I found it. What do you know about it?", 
            nextNodeId = 1 
        });
        
        keyReactionNode.options.Add(new DialogueOption { 
            optionText = "That's none of your business.", 
            nextNodeId = 2 
        });
        
        // Node 1: Information about the key
        DialogueNode keyInfoNode = new DialogueNode();
        keyInfoNode.id = 1;
        keyInfoNode.npcText = "That key opens the cellar beneath the old mansion. They say there are valuable treasures hidden down there, but also great danger.";
        
        keyInfoNode.options.Add(new DialogueOption { 
            optionText = "Tell me more about this danger.", 
            nextNodeId = 3 
        });
        
        keyInfoNode.options.Add(new DialogueOption { 
            optionText = "Where is this mansion?", 
            nextNodeId = 4 
        });
        
        // Node 2: Negative response
        DialogueNode negativeNode = new DialogueNode();
        negativeNode.id = 2;
        negativeNode.npcText = "Very well. Keep your secrets. But be warned - some doors are better left locked.";
        negativeNode.isEndNode = true;
        
        // Node 3: Danger information
        DialogueNode dangerInfoNode = new DialogueNode();
        dangerInfoNode.id = 3;
        dangerInfoNode.npcText = "They say the previous owner of the mansion conducted strange experiments in that cellar. Some nights, people claim to hear unnatural sounds coming from beneath the ground...";
        
        dangerInfoNode.options.Add(new DialogueOption { 
            optionText = "Where is this mansion?", 
            nextNodeId = 4 
        });
        
        dangerInfoNode.options.Add(new DialogueOption { 
            optionText = "That's enough information. Goodbye.", 
            nextNodeId = 5 
        });
        
        // Node 4: Location information
        DialogueNode locationInfoNode = new DialogueNode();
        locationInfoNode.id = 4;
        locationInfoNode.npcText = "The old Darkwood Mansion stands at the end of the northern path, just beyond the cemetery. You can't miss it - it's the only building still standing in that area.";
        
        locationInfoNode.options.Add(new DialogueOption { 
            optionText = "Thank you for the information.", 
            nextNodeId = 5 
        });
        
        // Node 5: Ending
        DialogueNode endingNode = new DialogueNode();
        endingNode.id = 5;
        endingNode.npcText = "Be careful. If you use that key... well, just remember I warned you.";
        endingNode.isEndNode = true;
        
        // Add all nodes to the dialogue tree
        keyDialogue.nodes.Add(keyReactionNode);
        keyDialogue.nodes.Add(keyInfoNode);
        keyDialogue.nodes.Add(negativeNode);
        keyDialogue.nodes.Add(dangerInfoNode);
        keyDialogue.nodes.Add(locationInfoNode);
        keyDialogue.nodes.Add(endingNode);
        
        // Add the dialogue tree to the dialogue system
        dialogueSystem.dialogueTrees.Add(keyDialogue);
    }
    
    // Method to check for specific items in inventory
    private bool CheckForItem(string itemName)
    {
        if (playerInventory != null)
        {
            bool hasItem = playerInventory.HasItem(itemName);
            Debug.Log($"Checking for item: {itemName}, result: {hasItem}");
            return hasItem;
        }
        Debug.Log("PlayerInventory is null, cannot check for item");
        return false;
    }
}