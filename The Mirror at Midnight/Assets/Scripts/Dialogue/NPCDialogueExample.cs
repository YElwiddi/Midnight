using UnityEngine;
using System.Collections.Generic;

// This example script shows how to set up dialogue for an NPC
public class NPCDialogueExample : MonoBehaviour
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
        
        // Initialize with example dialogue trees
        SetupExampleDialogues();
    }
    
    void SetupExampleDialogues()
    {
        // Create a simple dialogue tree
        DialogueTree greetingDialogue = new DialogueTree();
        greetingDialogue.dialogueName = "First Meeting";
        
        // Node 0: Initial greeting
        DialogueNode greetingNode = new DialogueNode();
        greetingNode.id = 0;
        greetingNode.npcText = "Hello there, traveler! I haven't seen you around these parts before. What brings you to our village?";
        
        // Options for Node 0
        greetingNode.options.Add(new DialogueOption { 
            optionText = "I'm just passing through.", 
            nextNodeId = 1 
        });
        
        greetingNode.options.Add(new DialogueOption { 
            optionText = "I'm looking for work.", 
            nextNodeId = 2 
        });
        
        greetingNode.options.Add(new DialogueOption { 
            optionText = "None of your business.", 
            nextNodeId = 3 
        });
        
        // Node 1: Response to "passing through"
        DialogueNode passingThroughNode = new DialogueNode();
        passingThroughNode.id = 1;
        passingThroughNode.npcText = "Ah, just another wanderer then. Well, be sure to visit our inn before you leave. The food is excellent, and the beds are comfortable.";
        
        // Options for Node 1
        passingThroughNode.options.Add(new DialogueOption { 
            optionText = "Thanks for the suggestion.", 
            nextNodeId = 4 
        });
        
        passingThroughNode.options.Add(new DialogueOption { 
            optionText = "Any other recommendations?", 
            nextNodeId = 5 
        });
        
        // Node 2: Response to "looking for work"
        DialogueNode workNode = new DialogueNode();
        workNode.id = 2;
        workNode.npcText = "Work, eh? You're in luck! The blacksmith is looking for an apprentice, and the local tavern could use a hand. Or if you're more adventurous, there are rumors of bandits in the nearby hills...";
        
        // Options for Node 2
        workNode.options.Add(new DialogueOption { 
            optionText = "Tell me about the blacksmith.", 
            nextNodeId = 6 
        });
        
        workNode.options.Add(new DialogueOption { 
            optionText = "I'm interested in the bandits.", 
            nextNodeId = 7 
        });
        
        // Node 3: Response to "none of your business"
        DialogueNode rudeNode = new DialogueNode();
        rudeNode.id = 3;
        rudeNode.npcText = "Well excuse me for being friendly! If that's how you want to be, I won't waste any more of your precious time.";
        rudeNode.isEndNode = true; // This ends the dialogue
        
        // Node 4: End of passing through path
        DialogueNode thanksNode = new DialogueNode();
        thanksNode.id = 4;
        thanksNode.npcText = "You're welcome! Safe travels, friend.";
        thanksNode.isEndNode = true;
        
        // Node 5: More recommendations
        DialogueNode recommendationsNode = new DialogueNode();
        recommendationsNode.id = 5;
        recommendationsNode.npcText = "Well, the local herbalist has some fascinating remedies if you're into that sort of thing. And don't miss the sunset from the hill just north of here - it's breathtaking!";
        recommendationsNode.isEndNode = true;
        
        // Node 6: Blacksmith info
        DialogueNode blacksmithNode = new DialogueNode();
        blacksmithNode.id = 6;
        blacksmithNode.npcText = "Old Grunvar is the best blacksmith for miles around. Tough as nails but fair. Tell him I sent you and he might give you a discount on your first purchase.";
        blacksmithNode.isEndNode = true;
        
        // Node 7: Bandit info
        DialogueNode banditNode = new DialogueNode();
        banditNode.id = 7;
        banditNode.npcText = "The bandits have been raiding travelers on the east road. The mayor's offering a reward for anyone who can... take care of the problem. But be careful - they're dangerous folk.";
        banditNode.isEndNode = true;
        
        // Add all nodes to the dialogue tree
        greetingDialogue.nodes.Add(greetingNode);
        greetingDialogue.nodes.Add(passingThroughNode);
        greetingDialogue.nodes.Add(workNode);
        greetingDialogue.nodes.Add(rudeNode);
        greetingDialogue.nodes.Add(thanksNode);
        greetingDialogue.nodes.Add(recommendationsNode);
        greetingDialogue.nodes.Add(blacksmithNode);
        greetingDialogue.nodes.Add(banditNode);
        
        // Create a second dialogue tree (for returning player)
        DialogueTree returnDialogue = new DialogueTree();
        returnDialogue.dialogueName = "Return Visit";
        
        // Node 0: Return greeting
        DialogueNode returnNode = new DialogueNode();
        returnNode.id = 0;
        returnNode.npcText = "Welcome back! How have your travels been?";
        
        // Options for return dialogue
        returnNode.options.Add(new DialogueOption { 
            optionText = "Good, thanks for asking.", 
            nextNodeId = 1 
        });
        
        returnNode.options.Add(new DialogueOption { 
            optionText = "I need more information about those bandits.", 
            nextNodeId = 2 
        });
        
        // Node 1: Pleasant response
        DialogueNode pleasantNode = new DialogueNode();
        pleasantNode.id = 1;
        pleasantNode.npcText = "Glad to hear it! Let me know if you need anything.";
        pleasantNode.isEndNode = true;
        
        // Node 2: More bandit info
        DialogueNode moreBanditNode = new DialogueNode();
        moreBanditNode.id = 2;
        moreBanditNode.npcText = "I've heard they have a hideout in an abandoned mine to the northeast. Be careful if you go looking for them.";
        moreBanditNode.isEndNode = true;
        
        // Add nodes to return dialogue tree
        returnDialogue.nodes.Add(returnNode);
        returnDialogue.nodes.Add(pleasantNode);
        returnDialogue.nodes.Add(moreBanditNode);
        
        // Add both dialogue trees to the dialogue system
        dialogueSystem.dialogueTrees.Add(greetingDialogue);
        dialogueSystem.dialogueTrees.Add(returnDialogue);
        
        // Set active dialogue tree to the first one by default
        dialogueSystem.activeDialogueTreeIndex = 0;
    }
    
    // Example method to switch to return dialogue after a quest is completed
    public void SwitchToReturnDialogue()
    {
        dialogueSystem.SetActiveDialogueTree(1);
    }
}