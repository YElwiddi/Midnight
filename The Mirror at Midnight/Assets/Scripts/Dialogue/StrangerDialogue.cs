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
    
    // This will track if we've completed a conversation already
    private bool isAnnoyed = false;
    private bool hasCompletedDialogue = false;
    
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
        
        // Initialize all dialogue trees
        SetupStrangerDialogue();
        SetupKeyDialogue();
        SetupAnnoyedDialogue();
        
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
        else if (hasCompletedDialogue)
        {
            Debug.Log("Player has already completed a conversation - Using Annoyed Dialogue Tree");
            isAnnoyed = true;
            dialogueSystem.SetActiveDialogueTree(2); // Index 2 will be the annoyed dialogue
        }
        else
        {
            Debug.Log($"Player does not have the {requiredItemName} - Using Regular Dialogue Tree");
            dialogueSystem.SetActiveDialogueTree(regularDialogueIndex);
        }
    }
    
    // We'll check this in Update to see if dialogue ended
    private bool wasInDialogue = false;
    
    private void Update()
    {
        // Check if we're currently in dialogue
        bool currentlyInDialogue = dialogueSystem != null && dialogueSystem.IsInDialogue();
        
        // If we WERE in dialogue, but now we're NOT (dialogue just ended)
        if (wasInDialogue && !currentlyInDialogue && !hasCompletedDialogue && 
            dialogueSystem.activeDialogueTreeIndex == regularDialogueIndex)
        {
            // Mark that we've completed a dialogue
            hasCompletedDialogue = true;
            Debug.Log("Dialogue completed - NPC will be annoyed next time");
        }
        
        // Update our tracking of dialogue state for the next frame
        wasInDialogue = currentlyInDialogue;
    }
    
    void SetupStrangerDialogue()
    {
        // Create a dialogue tree for the stranger
        DialogueTree strangerDialogue = new DialogueTree();
        strangerDialogue.dialogueName = "Thomas";
        
        // Node 0: Initial greeting
        DialogueNode greetingNode = new DialogueNode();
        greetingNode.id = 0;
        greetingNode.npcText = "...";
        
        // Only one option for Node 0 - continue
        greetingNode.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 1 
        });
        
        // Node 1: Question from NPC
        DialogueNode questionNode = new DialogueNode();
        questionNode.id = 1;
        questionNode.npcText = "What do you want?";
        
        // Options for Node 1
        questionNode.options.Add(new DialogueOption { 
            optionText = "My car broke down up the road... would you be able to give me a hand?", 
            nextNodeId = 2 
        });
        
        questionNode.options.Add(new DialogueOption { 
            optionText = "I'm looking for two friends, have you seen anyone pass by here?", 
            nextNodeId = 3 
        });
        
        DialogueNode cantHelpNode = new DialogueNode();
        cantHelpNode.id = 2;
        cantHelpNode.npcText = "Can't help ya.";
        
        cantHelpNode.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 8
        });
        
        DialogueNode dotdotdotNode1 = new DialogueNode();
        dotdotdotNode1.id = 3;
        dotdotdotNode1.npcText = "...";

        dotdotdotNode1.options.Add(new DialogueOption { 
            optionText = "[CONTINUE]", 
            nextNodeId = 4
        });

        // Node 4: Continuation
        DialogueNode whereTheyGoNode = new DialogueNode();
        whereTheyGoNode.id = 4;
        whereTheyGoNode.npcText = "Yeah. Think I saw a car pass by here. Stopped down the road. One of 'em started yelling a bunch, and then she wandered off.";
                
        whereTheyGoNode.options.Add(new DialogueOption { 
            optionText = "Did you see which way the girl went?", 
            nextNodeId = 5 
        });

        whereTheyGoNode.options.Add(new DialogueOption { 
            optionText = "Did you see which way the car went?", 
            nextNodeId = 6
        });
        
        DialogueNode whereLadyNode = new DialogueNode();
        whereLadyNode.id = 5;
        whereLadyNode.npcText = "Yeah. The lady wandered off into the woods, towards the house up the hill. She seemed pretty upset.";
        
        whereLadyNode.options.Add(new DialogueOption { 
            optionText = "Alright, thanks.", 
            nextNodeId = 7 
        });
        whereLadyNode.options.Add(new DialogueOption { 
            optionText = "Thanks, I guess...", 
            nextNodeId = 7 
        });

        DialogueNode whereManNode = new DialogueNode();
        whereManNode.id = 6;
        whereManNode.npcText = "Yeah. Drove towards the warehouse. The car looked pretty messed up.";
        whereManNode.options.Add(new DialogueOption { 
            optionText = "Alright, thanks.", 
            nextNodeId = 7 
        });
        whereManNode.options.Add(new DialogueOption { 
            optionText = "Thanks, I guess...", 
            nextNodeId = 7 
        });

        DialogueNode byeNode = new DialogueNode();
        byeNode.id = 7;
        byeNode.npcText = "Yeah, sure.";
        byeNode.isEndNode = true;

        DialogueNode byeNode2 = new DialogueNode();
        byeNode2.id = 8;
        byeNode2.npcText = "Don't go looking for any trouble.";
        byeNode2.isEndNode = true;
        
        // Add all nodes to the dialogue tree
        strangerDialogue.nodes.Add(greetingNode);
        strangerDialogue.nodes.Add(questionNode);
        strangerDialogue.nodes.Add(cantHelpNode);
        strangerDialogue.nodes.Add(dotdotdotNode1);
        strangerDialogue.nodes.Add(whereTheyGoNode);
        strangerDialogue.nodes.Add(whereLadyNode);
        strangerDialogue.nodes.Add(whereManNode);
        strangerDialogue.nodes.Add(byeNode);
        strangerDialogue.nodes.Add(byeNode2);
        
        // Add the dialogue tree to the dialogue system
        dialogueSystem.dialogueTrees.Add(strangerDialogue);
    }
    
    // Create a separate "annoyed" dialogue tree
    void SetupAnnoyedDialogue()
    {
        DialogueTree annoyedDialogue = new DialogueTree();
        annoyedDialogue.dialogueName = "Thomas";
        
        DialogueNode annoyedNode = new DialogueNode();
        annoyedNode.id = 0;
        annoyedNode.npcText = "I'm busy.";
        annoyedNode.isEndNode = true;
        
        annoyedDialogue.nodes.Add(annoyedNode);
        
        // Add the dialogue tree to the dialogue system
        dialogueSystem.dialogueTrees.Add(annoyedDialogue);
    }
    
    void SetupKeyDialogue()
    {
        // Create a dialogue tree for when player has the key
        DialogueTree keyDialogue = new DialogueTree();
        keyDialogue.dialogueName = "Thomas";
        
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