using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DialogueNPC : MonoBehaviour, IInteractable
{
    [Header("NPC Settings")]
    public string npcName = "NPC";
    public string interactionVerb = "talk to";
    
    [Header("Dialogue Settings")]
    public string[] dialogueLines;
    public DialogueOption[] responseOptions;
    
    [Header("UI References")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI npcNameText;
    public Button[] responseButtons;
    public TextMeshProUGUI[] responseButtonTexts;
    
    [Header("Interaction Effects")]
    public bool highlightWhenLookedAt = true;
    public Color highlightColor = new Color(1f, 0.8f, 0.2f, 1f);
    public bool lookAtPlayerWhenTalking = true;
    
    [Header("Audio")]
    public AudioClip[] dialogueSounds;
    public AudioClip interactSound;
    
    // Private variables
    private Color originalColor;
    private Material[] materials;
    private bool isHighlighted = false;
    private bool isInDialogue = false;
    private int currentDialogueLine = 0;
    private Transform playerTransform;
    private Quaternion originalRotation;
    private AudioSource audioSource;
    
    [System.Serializable]
    public class DialogueOption
    {
        public string responseText;
        public string[] npcReplyLines;
    }
    
    void Start()
    {
        // Cache original materials and colors
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            materials = renderer.materials;
            if (materials.Length > 0)
            {
                originalColor = materials[0].color;
            }
        }
        
        // Make sure this object is on the right layer for interaction
        if (LayerMask.NameToLayer("Interactable") != -1)
            gameObject.layer = LayerMask.NameToLayer("Interactable");
            
        // Get player reference
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        // Save original rotation
        originalRotation = transform.rotation;
        
        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.volume = 0.8f;
        }
        
        // Hide dialogue UI at start if it's assigned
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"Dialogue Panel not assigned to {npcName} NPC");
        }
    }
    
    void Update()
    {
        // Look at player when in dialogue if enabled
        if (lookAtPlayerWhenTalking && isInDialogue && playerTransform != null)
        {
            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.y = 0; // Don't look up/down
            
            if (directionToPlayer != Vector3.zero)
            {
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.LookRotation(directionToPlayer),
                    5f * Time.deltaTime
                );
            }
        }
        
        // Close dialogue if escape is pressed
        if (isInDialogue && Input.GetKeyDown(KeyCode.Escape))
        {
            EndDialogue();
        }
    }
    
    // IInteractable implementation
    public void Interact()
    {
        if (isInDialogue)
            return;
            
        Debug.Log($"Player interacted with {npcName}");
        
        // Play interaction sound
        if (interactSound != null)
        {
            audioSource.PlayOneShot(interactSound);
        }
        
        StartDialogue();
    }
    
    public string GetInteractionPrompt()
    {
        return interactionVerb + " " + npcName;
    }
    
    // Called when the crosshair hovers over this NPC
    public void OnHoverEnter()
    {
        isHighlighted = true;
        
        // Highlight the NPC if enabled
        if (highlightWhenLookedAt && materials != null)
        {
            foreach (Material mat in materials)
            {
                mat.color = highlightColor;
            }
        }
    }
    
    // Called when the crosshair moves away from this NPC
    public void OnHoverExit()
    {
        isHighlighted = false;
        
        // Restore original color
        if (highlightWhenLookedAt && materials != null)
        {
            foreach (Material mat in materials)
            {
                mat.color = originalColor;
            }
        }
    }
    
    // Dialogue system methods
    private void StartDialogue()
    {
        isInDialogue = true;
        
        // Enable dialogue UI
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }
        
        // Set NPC name in UI
        if (npcNameText != null)
        {
            npcNameText.text = npcName;
        }
        
        // Start from the first line
        currentDialogueLine = 0;
        DisplayCurrentDialogueLine();
        
        // Disable player movement/shooting/etc if you have such systems
        // PlayerController player = FindObjectOfType<PlayerController>();
        // if (player != null) player.enabled = false;
    }
    
    private void EndDialogue()
    {
        isInDialogue = false;
        
        // Hide dialogue UI
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        
        // Return to original rotation
        if (lookAtPlayerWhenTalking)
        {
            StartCoroutine(ReturnToOriginalRotation());
        }
        
        // Re-enable player components
        // PlayerController player = FindObjectOfType<PlayerController>();
        // if (player != null) player.enabled = true;
    }
    
    private void DisplayCurrentDialogueLine()
    {
        // Check if we've reached the end of dialogue
        if (currentDialogueLine >= dialogueLines.Length)
        {
            ShowResponseOptions();
            return;
        }
        
        // Display the current line
        if (dialogueText != null)
        {
            dialogueText.text = dialogueLines[currentDialogueLine];
        }
        
        // Play random dialogue sound if available
        if (dialogueSounds != null && dialogueSounds.Length > 0)
        {
            int soundIndex = Random.Range(0, dialogueSounds.Length);
            audioSource.PlayOneShot(dialogueSounds[soundIndex]);
        }
        
        // Hide response options during NPC dialogue
        HideResponseButtons();
        
        // Advance to next line on mouse click or button press
        StartCoroutine(WaitForNextLine());
    }
    
    private IEnumerator WaitForNextLine()
    {
        // Wait for player to click or press E/Space
        bool advanceDialogue = false;
        while (!advanceDialogue)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            {
                advanceDialogue = true;
            }
            yield return null;
        }
        
        // Move to the next line
        currentDialogueLine++;
        DisplayCurrentDialogueLine();
    }
    
    private void ShowResponseOptions()
    {
        // Make sure we have response options
        if (responseOptions == null || responseOptions.Length == 0)
        {
            EndDialogue();
            return;
        }
        
        // Display up to 3 response options
        int optionsToShow = Mathf.Min(responseOptions.Length, responseButtons.Length);
        
        for (int i = 0; i < responseButtons.Length; i++)
        {
            if (i < optionsToShow)
            {
                responseButtons[i].gameObject.SetActive(true);
                responseButtonTexts[i].text = responseOptions[i].responseText;
                
                // Set up button click handler
                int responseIndex = i; // Need to capture the index in a local variable
                responseButtons[i].onClick.RemoveAllListeners();
                responseButtons[i].onClick.AddListener(() => HandleResponseSelected(responseIndex));
            }
            else
            {
                responseButtons[i].gameObject.SetActive(false);
            }
        }
    }
    
    private void HideResponseButtons()
    {
        // Hide all response buttons
        if (responseButtons != null)
        {
            foreach (Button button in responseButtons)
            {
                button.gameObject.SetActive(false);
            }
        }
    }
    
    private void HandleResponseSelected(int responseIndex)
    {
        // Make sure the index is valid
        if (responseIndex < 0 || responseIndex >= responseOptions.Length)
            return;
            
        // Get the selected response
        DialogueOption selectedOption = responseOptions[responseIndex];
        
        // Replace dialogue lines with NPC's reply
        dialogueLines = selectedOption.npcReplyLines;
        
        // Start from the beginning of the new dialogue
        currentDialogueLine = 0;
        DisplayCurrentDialogueLine();
    }
    
    private IEnumerator ReturnToOriginalRotation()
    {
        float returnDuration = 1.5f;
        float elapsed = 0;
        
        while (elapsed < returnDuration)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, originalRotation, elapsed / returnDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.rotation = originalRotation;
    }
}