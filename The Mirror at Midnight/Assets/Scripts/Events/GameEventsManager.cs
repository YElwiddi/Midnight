using System;
using UnityEngine;

public class GameEventsManager : MonoBehaviour
{
    public static GameEventsManager instance {get; private set;}


    public DialogueEvents dialogueEvents;

    private void Awake(){
        if (instance != null){
            Debug.LogError("Found more than one game events manager in the scene");
        }
        instance = this;
        // initalize all events
        dialogueEvents = new DialogueEvents();
    }
}
