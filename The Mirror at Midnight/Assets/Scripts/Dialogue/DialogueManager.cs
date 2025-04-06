using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;

public class DialogueManager : MonoBehaviour
{
    [Header("Ink Story")]
    [SerializeField] private TextAsset inkJson;
    private Story story;
    private bool dialoguePlaying = false;
    public PlayerEvents player;

    private void Update() {
        CheckForContinueInput();
    }

    private void Awake(){
        story = new Story(inkJson.text);
    }
    private void OnEnable(){
        GameEventsManager.instance.dialogueEvents.onEnterDialogue += EnterDialogue;
        //Need to add check for input events? This may require interaction system rewrite
        //Just using click/spacebar for now - may cause problems later on for clicking options
    }
    
    private void OnDisable(){
        GameEventsManager.instance.dialogueEvents.onEnterDialogue -= EnterDialogue;
    }

    private void ButtonPressContinue(){
        if (!dialoguePlaying){
            return;
        }
        ContinueOrExitStory();
    }

    private void CheckForContinueInput() {
    // Check for spacebar or mouse click
    if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) {
        ButtonPressContinue();
    }
}


    private void EnterDialogue(string knotName){
        Debug.Log("enter dialogue called");
        if (dialoguePlaying){
            return;
        }
        player.DisableMovement();
        dialoguePlaying = true;
        GameEventsManager.instance.dialogueEvents.DialogueStarted();
        if (!knotName.Equals("")){
            story.ChoosePathString(knotName);
        }
        else{
            Debug.LogWarning("Knot name was the empty string when entering dialogue.");
        }
        ContinueOrExitStory();
    }

    private void ContinueOrExitStory(){
        if (story.canContinue){
            string dialogueLine = story.Continue();
            GameEventsManager.instance.dialogueEvents.DisplayDialogue(dialogueLine);
            Debug.Log(dialogueLine);
        }
        else{
            StartCoroutine(ExitDialogue());
        }
    }
    private IEnumerator ExitDialogue(){
        yield return null;
        player.EnableMovement();
        Debug.Log("Exiting dialogue");

        dialoguePlaying = false;
        GameEventsManager.instance.dialogueEvents.DialogueFinished();

        story.ResetState();
    }
}
