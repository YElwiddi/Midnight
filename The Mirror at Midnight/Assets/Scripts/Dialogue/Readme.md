# Item-Based Dialogue System

This documentation explains how to create and set up dialogue trees that change based on items in the player's inventory.

## Table of Contents
1. [Overview](#overview)
2. [Creating a New NPC with Item-Based Dialogue](#creating-a-new-npc)
3. [Setting Up Dialogue Trees](#setting-up-dialogue-trees)
4. [Triggering Different Dialogues Based on Inventory](#triggering-different-dialogues)
5. [Advanced: Multiple Item Requirements](#advanced-multiple-item-requirements)
6. [Troubleshooting](#troubleshooting)

## Overview

The item-based dialogue system allows NPCs to have different conversations with the player depending on what items the player has in their inventory. This creates more dynamic interactions and can be used for quest progression, secret dialogues, or conditional story elements.

## Creating a New NPC with Item-Based Dialogue

1. Create a new GameObject in your scene
2. Add the `DialogueSystem` component
3. Add your custom dialogue script (similar to `StrangerDialogue.cs`)
4. Configure the NPC settings in the Inspector

Example GameObject hierarchy:
```
MyNPC
├── DialogueSystem component
└── MyNPCDialogue script
```

## Setting Up Dialogue Trees

### Step 1: Create a new script for your NPC

Create a new C# script (e.g., `MyNPCDialogue.cs`) that follows this template:

```csharp
using UnityEngine;
using System.Collections.Generic;

public class MyNPCDialogue : MonoBehaviour
{
    private DialogueSystem dialogueSystem;
    private PlayerInventory playerInventory;
    
    [Header("Required Items")]
    public string requiredItemName = "Your Item Name";
    
    // Index references for dialogue trees
    private int regularDialogueIndex = 0;
    private int itemDialogueIndex = 1;
    // Add more indices as needed for additional dialogue trees
    
    void Start()
    {
        // Get references
        dialogueSystem = GetComponent<DialogueSystem>();
        playerInventory = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerInventory>();
        
        // Check for required components
        if (dialogueSystem == null)
        {
            Debug.LogError("DialogueSystem component not found on this game object!");
            return;
        }
        
        if (playerInventory == null)
        {
            Debug.LogWarning("PlayerInventory not found! Item-based dialogue will not work.");
        }
        
        // Initialize dialogue trees
        SetupRegularDialogue();
        SetupItemDialogue();
        // Add more setup methods as needed
        
        // Subscribe to interaction event
        dialogueSystem.onCustomInteract += OnInteractionStarted;
    }
    
    private void OnDestroy()
    {
        // Clean up event subscription
        if (dialogueSystem != null)
        {
            dialogueSystem.onCustomInteract -= OnInteractionStarted;
        }
    }
    
    // Add your dialogue setup methods here
    void SetupRegularDialogue()
    {
        // Create and configure your regular dialogue tree
    }
    
    void SetupItemDialogue()
    {
        // Create and configure your item-dependent dialogue tree
    }
    
    // Method to check for items
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
```

### Step 2: Define dialogue trees

For each dialogue tree, you need to:

1. Create a new `DialogueTree` object
2. Set its `dialogueName`
3. Create nodes for the conversation
4. Add options to each node
5. Add the tree to `dialogueSystem.dialogueTrees`

Example of creating a basic dialogue tree:

```csharp
void SetupRegularDialogue()
{
    // Create a new dialogue tree
    DialogueTree regularDialogue = new DialogueTree();
    regularDialogue.dialogueName = "NPC Name";
    
    // Node 0: Initial greeting
    DialogueNode greetingNode = new DialogueNode();
    greetingNode.id = 0;
    greetingNode.npcText = "Hello there!";
    
    // Add option to continue
    greetingNode.options.Add(new DialogueOption { 
        optionText = "[CONTINUE]", 
        nextNodeId = 1 
    });
    
    // Node 1: Follow-up
    DialogueNode followupNode = new DialogueNode();
    followupNode.id = 1;
    followupNode.npcText = "How can I help you today?";
    
    // Add options for the player
    followupNode.options.Add(new DialogueOption { 
        optionText = "I'm just looking around.", 
        nextNodeId = 2 
    });
    
    followupNode.options.Add(new DialogueOption { 
        optionText = "Goodbye.", 
        nextNodeId = 3 
    });
    
    // Node 2: Response to looking around
    DialogueNode lookingNode = new DialogueNode();
    lookingNode.id = 2;
    lookingNode.npcText = "Well, enjoy your stay!";
    lookingNode.isEndNode = true; // This ends the dialogue
    
    // Node 3: Goodbye response
    DialogueNode goodbyeNode = new DialogueNode();
    goodbyeNode.id = 3;
    goodbyeNode.npcText = "Farewell!";
    goodbyeNode.isEndNode = true; // This ends the dialogue
    
    // Add all nodes to the dialogue tree
    regularDialogue.nodes.Add(greetingNode);
    regularDialogue.nodes.Add(followupNode);
    regularDialogue.nodes.Add(lookingNode);
    regularDialogue.nodes.Add(goodbyeNode);
    
    // Add the dialogue tree to the dialogue system
    dialogueSystem.dialogueTrees.Add(regularDialogue);
}
```

## Triggering Different Dialogues Based on Inventory

Add an `OnInteractionStarted` method to check inventory and select the appropriate dialogue tree:

```csharp
private void OnInteractionStarted()
{
    // Check inventory for required item
    bool hasItem = CheckForItem(requiredItemName);
    
    if (hasItem)
    {
        Debug.Log($"Player has the {requiredItemName} - Using Item Dialogue Tree");
        dialogueSystem.SetActiveDialogueTree(itemDialogueIndex);
    }
    else
    {
        Debug.Log($"Player does not have the {requiredItemName} - Using Regular Dialogue Tree");
        dialogueSystem.SetActiveDialogueTree(regularDialogueIndex);
    }
}
```

## Advanced: Multiple Item Requirements

For more complex dialogue conditions with multiple items, you can expand the system:

```csharp
[Header("Required Items")]
public string primaryItemName = "Key";
public string secondaryItemName = "Map";
public string specialItemName = "Ancient Relic";

// Index references
private int regularDialogueIndex = 0;
private int primaryItemDialogueIndex = 1;
private int secondaryItemDialogueIndex = 2;
private int bothItemsDialogueIndex = 3;
private int specialItemDialogueIndex = 4;

private void OnInteractionStarted()
{
    bool hasPrimaryItem = CheckForItem(primaryItemName);
    bool hasSecondaryItem = CheckForItem(secondaryItemName);
    bool hasSpecialItem = CheckForItem(specialItemName);
    
    // Special item takes highest priority
    if (hasSpecialItem)
    {
        Debug.Log($"Player has the {specialItemName} - Using Special Dialogue Tree");
        dialogueSystem.SetActiveDialogueTree(specialItemDialogueIndex);
    }
    // Both primary and secondary items
    else if (hasPrimaryItem && hasSecondaryItem)
    {
        Debug.Log($"Player has both {primaryItemName} and {secondaryItemName} - Using Both Items Dialogue Tree");
        dialogueSystem.SetActiveDialogueTree(bothItemsDialogueIndex);
    }
    // Only primary item
    else if (hasPrimaryItem)
    {
        Debug.Log($"Player has the {primaryItemName} - Using Primary Item Dialogue Tree");
        dialogueSystem.SetActiveDialogueTree(primaryItemDialogueIndex);
    }
    // Only secondary item
    else if (hasSecondaryItem)
    {
        Debug.Log($"Player has the {secondaryItemName} - Using Secondary Item Dialogue Tree");
        dialogueSystem.SetActiveDialogueTree(secondaryItemDialogueIndex);
    }
    // No special items
    else
    {
        Debug.Log("Player has no special items - Using Regular Dialogue Tree");
        dialogueSystem.SetActiveDialogueTree(regularDialogueIndex);
    }
}
```

Remember to create a setup method for each dialogue tree:
- `SetupRegularDialogue()`
- `SetupPrimaryItemDialogue()`
- `SetupSecondaryItemDialogue()`
- `SetupBothItemsDialogue()`
- `SetupSpecialItemDialogue()`

## Troubleshooting

### Common Issues:

1. **Dialogue Not Changing Based on Inventory:**
   - Check that the `PlayerInventory` reference is not null
   - Verify that the item name matches exactly (case-sensitive)
   - Ensure you're subscribing to the `onCustomInteract` event
   - Confirm your dialogue tree indices match the order they were added

2. **Dialogue System Not Working:**
   - Make sure the `DialogueSystem` component is attached to the same GameObject
   - Check the Unity console for errors
   - Verify that player tags are set correctly for finding the player

3. **Log Messages Not Appearing:**
   - Enable "Debug" logs in Unity Console settings
   - Check for typos in log message code

4. **Dialogue Trees Missing:**
   - Ensure all dialogue trees are properly added to `dialogueSystem.dialogueTrees`
   - Check that all required nodes are created and added to the tree

If you encounter persistent issues, check the Unity console for specific error messages that might provide more details about what's going wrong.