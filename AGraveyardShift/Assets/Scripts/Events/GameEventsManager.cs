using System;
using UnityEngine;

public class GameEventsManager : MonoBehaviour
{
    public static GameEventsManager instance {get; private set;}

    public DialogueEvents dialogueEvents;
    public GameFlowEvents gameFlowEvents;
    public SideGameEvents sideGameEvents;
    public SanityEvents sanityEvents;

    private void Awake(){
        if (instance != null){
            Debug.LogError("Found more than one game events manager in the scene");
        }
        instance = this;
        // initalize all events
        dialogueEvents = new DialogueEvents();
        gameFlowEvents = new GameFlowEvents();
        sideGameEvents = new SideGameEvents();
        sanityEvents = new SanityEvents();
    }
}
