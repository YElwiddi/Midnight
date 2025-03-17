using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerCellPhone : MonoBehaviour
{
    [Header("Phone UI References")]
    public GameObject phoneUI;
    public GameObject homeScreen;
    public GameObject messagesApp;
    public GameObject contactListScreen;
    
    [Header("Contact Specific Screens")]
    public GameObject contact1ConversationScreen;
    public GameObject contact2ConversationScreen;
    
    [Header("Message Response UI")]
    public GameObject responsePromptPanel;
    public TextMeshProUGUI responsePromptText;
    public Button responseOption1;
    public Button responseOption2;
    
    [Header("Contacts")]
    public string contact1Name = "Alex";
    public string contact2Name = "Jamie";
    
    [Header("Input Settings")]
    public KeyCode togglePhoneKey = KeyCode.X;
    
    // Track current UI state
    private enum PhoneScreen
    {
        Off,
        Home,
        Messages,
        ContactList,
        Contact1Conversation,
        Contact2Conversation
    }
    
    private PhoneScreen currentScreen = PhoneScreen.Off;
    private bool isResponsePromptActive = false;
    
    // Message data structures
    [System.Serializable]
    public class Message
    {
        public string senderName;
        public string messageContent;
        public bool isPlayerMessage;
    }
    
    [System.Serializable]
    public class Conversation
    {
        public string contactName;
        public List<Message> messages = new List<Message>();
    }
    
    [System.Serializable]
    public class ResponsePrompt
    {
        public string promptText;
        public string option1Text;
        public string option1Response;
        public string option2Text;
        public string option2Response;
    }
    
    public List<Conversation> conversations = new List<Conversation>();
    public List<ResponsePrompt> contact1Prompts = new List<ResponsePrompt>();
    public List<ResponsePrompt> contact2Prompts = new List<ResponsePrompt>();
    
    private int currentContact1PromptIndex = 0;
    private int currentContact2PromptIndex = 0;
    
    void Start()
    {
        InitializePhone();
    }
    
    void Update()
    {
        // Toggle phone on/off with X key
        if (Input.GetKeyDown(togglePhoneKey))
        {
            TogglePhone();
        }
    }
    
    private void InitializePhone()
    {
        // Setup initial state
        phoneUI.SetActive(false);
        currentScreen = PhoneScreen.Off;
        
        // Initialize conversations with some starter messages if needed
        if (conversations.Count == 0)
        {
            // Create conversations for each contact
            Conversation contact1Convo = new Conversation();
            contact1Convo.contactName = contact1Name;
            contact1Convo.messages.Add(new Message { senderName = contact1Name, messageContent = "Hey, how's it going?", isPlayerMessage = false });
            
            Conversation contact2Convo = new Conversation();
            contact2Convo.contactName = contact2Name;
            contact2Convo.messages.Add(new Message { senderName = contact2Name, messageContent = "Did you get my email about that thing?", isPlayerMessage = false });
            
            conversations.Add(contact1Convo);
            conversations.Add(contact2Convo);
        }
        
        // Setup sample response prompts
        if (contact1Prompts.Count == 0)
        {
            contact1Prompts.Add(new ResponsePrompt {
                promptText = "How do you want to respond to " + contact1Name + "?",
                option1Text = "I'm doing great!",
                option1Response = "I'm doing great! How about you?",
                option2Text = "Been better, to be honest.",
                option2Response = "Been better, to be honest. It's been a rough week."
            });
        }
        
        if (contact2Prompts.Count == 0)
        {
            contact2Prompts.Add(new ResponsePrompt {
                promptText = "How do you want to respond to " + contact2Name + "?",
                option1Text = "Yes, I got it. I'll look at it soon.",
                option1Response = "Yes, I got it. I'll look at it soon and get back to you.",
                option2Text = "What email? I don't see anything.",
                option2Response = "What email? I don't see anything in my inbox from you."
            });
        }
        
        // Hide all screens initially
        DeactivateAllScreens();
        
        // Setup button click handlers
        SetupButtonListeners();
    }
    
    private void SetupButtonListeners()
    {
        // Set up response option buttons
        responseOption1.onClick.AddListener(() => OnResponseOptionSelected(1));
        responseOption2.onClick.AddListener(() => OnResponseOptionSelected(2));
    }
    
    private void TogglePhone()
    {
        if (currentScreen == PhoneScreen.Off)
        {
            // Turn on phone
            phoneUI.SetActive(true);
            ShowHomeScreen();
        }
        else
        {
            // Turn off phone
            phoneUI.SetActive(false);
            currentScreen = PhoneScreen.Off;
        }
    }
    
    public void ShowHomeScreen()
    {
        DeactivateAllScreens();
        homeScreen.SetActive(true);
        currentScreen = PhoneScreen.Home;
    }
    
    public void ShowMessagesApp()
    {
        DeactivateAllScreens();
        messagesApp.SetActive(true);
        contactListScreen.SetActive(true);
        currentScreen = PhoneScreen.ContactList;
    }
    
    public void ShowContact1Conversation()
    {
        DeactivateAllScreens();
        messagesApp.SetActive(true);
        contact1ConversationScreen.SetActive(true);
        currentScreen = PhoneScreen.Contact1Conversation;
        UpdateConversationUI(0); // Contact1 is at index 0
    }
    
    public void ShowContact2Conversation()
    {
        DeactivateAllScreens();
        messagesApp.SetActive(true);
        contact2ConversationScreen.SetActive(true);
        currentScreen = PhoneScreen.Contact2Conversation;
        UpdateConversationUI(1); // Contact2 is at index 1
    }
    
    private void DeactivateAllScreens()
    {
        homeScreen.SetActive(false);
        messagesApp.SetActive(false);
        contactListScreen.SetActive(false);
        contact1ConversationScreen.SetActive(false);
        contact2ConversationScreen.SetActive(false);
        responsePromptPanel.SetActive(false);
        isResponsePromptActive = false;
    }
    
    // This method should be called after updating the messages list
    private void UpdateConversationUI(int conversationIndex)
    {
        Conversation convo = conversations[conversationIndex];
        
        // Find the right conversation container based on the index
        GameObject conversationContainer = (conversationIndex == 0) ? 
            contact1ConversationScreen.transform.Find("MessageContainer").gameObject : 
            contact2ConversationScreen.transform.Find("MessageContainer").gameObject;
        
        // Clear existing message bubbles (you'd implement this differently in a real game,
        // perhaps with object pooling or maintaining references to message UI elements)
        foreach (Transform child in conversationContainer.transform)
        {
            Destroy(child.gameObject);
        }
        
        // Create message bubbles for each message
        // In a real implementation, you would have prefabs for sent/received messages
        // and instantiate them here, setting text and positioning them properly
        
        // For demonstration - show a prompt after displaying messages
        if (conversationIndex == 0 && currentContact1PromptIndex < contact1Prompts.Count)
        {
            ShowResponsePrompt(contact1Prompts[currentContact1PromptIndex]);
        }
        else if (conversationIndex == 1 && currentContact2PromptIndex < contact2Prompts.Count)
        {
            ShowResponsePrompt(contact2Prompts[currentContact2PromptIndex]);
        }
    }
    
    private void ShowResponsePrompt(ResponsePrompt prompt)
    {
        responsePromptPanel.SetActive(true);
        responsePromptText.text = prompt.promptText;
        responseOption1.GetComponentInChildren<TextMeshProUGUI>().text = prompt.option1Text;
        responseOption2.GetComponentInChildren<TextMeshProUGUI>().text = prompt.option2Text;
        isResponsePromptActive = true;
    }
    
    private void OnResponseOptionSelected(int optionNumber)
    {
        if (!isResponsePromptActive) return;
        
        string responseText = "";
        
        // Determine which conversation is active and get the appropriate response
        if (currentScreen == PhoneScreen.Contact1Conversation && currentContact1PromptIndex < contact1Prompts.Count)
        {
            ResponsePrompt prompt = contact1Prompts[currentContact1PromptIndex];
            responseText = optionNumber == 1 ? prompt.option1Response : prompt.option2Response;
            
            // Add the player's response to the conversation
            conversations[0].messages.Add(new Message { 
                senderName = "You", 
                messageContent = responseText, 
                isPlayerMessage = true 
            });
            
            // Update the conversation UI
            UpdateConversationUI(0);
            
            // Move to the next prompt
            currentContact1PromptIndex++;
            
            // Add a "typing" delay and then a response from the contact
            StartCoroutine(ContactReplyDelay(0));
        }
        else if (currentScreen == PhoneScreen.Contact2Conversation && currentContact2PromptIndex < contact2Prompts.Count)
        {
            ResponsePrompt prompt = contact2Prompts[currentContact2PromptIndex];
            responseText = optionNumber == 1 ? prompt.option1Response : prompt.option2Response;
            
            // Add the player's response to the conversation
            conversations[1].messages.Add(new Message { 
                senderName = "You", 
                messageContent = responseText, 
                isPlayerMessage = true 
            });
            
            // Update the conversation UI
            UpdateConversationUI(1);
            
            // Move to the next prompt
            currentContact2PromptIndex++;
            
            // Add a "typing" delay and then a response from the contact
            StartCoroutine(ContactReplyDelay(1));
        }
        
        // Hide the response prompt
        responsePromptPanel.SetActive(false);
        isResponsePromptActive = false;
    }
    
    private IEnumerator ContactReplyDelay(int conversationIndex)
    {
        // In a real game, you might show a "typing" indicator here
        
        // Wait a realistic amount of time for the contact to "type" a response
        yield return new WaitForSeconds(Random.Range(1.5f, 3.0f));
        
        // Add a response from the contact
        string contactName = conversations[conversationIndex].contactName;
        string responseContent = "Thanks for your response! This is a simulated reply.";
        
        // Add more varied and contextual responses in a real game
        conversations[conversationIndex].messages.Add(new Message {
            senderName = contactName,
            messageContent = responseContent,
            isPlayerMessage = false
        });
        
        // Update the UI
        if ((conversationIndex == 0 && currentScreen == PhoneScreen.Contact1Conversation) ||
            (conversationIndex == 1 && currentScreen == PhoneScreen.Contact2Conversation))
        {
            UpdateConversationUI(conversationIndex);
        }
    }
    
    // Methods to add new messages or prompts from other scripts
    
    public void AddMessageToContact1(string messageContent, bool isPlayerMessage)
    {
        conversations[0].messages.Add(new Message {
            senderName = isPlayerMessage ? "You" : contact1Name,
            messageContent = messageContent,
            isPlayerMessage = isPlayerMessage
        });
        
        if (currentScreen == PhoneScreen.Contact1Conversation)
        {
            UpdateConversationUI(0);
        }
    }
    
    public void AddMessageToContact2(string messageContent, bool isPlayerMessage)
    {
        conversations[1].messages.Add(new Message {
            senderName = isPlayerMessage ? "You" : contact2Name,
            messageContent = messageContent,
            isPlayerMessage = isPlayerMessage
        });
        
        if (currentScreen == PhoneScreen.Contact2Conversation)
        {
            UpdateConversationUI(1);
        }
    }
    
    public void AddResponsePromptToContact1(string promptText, string option1Text, string option1Response, string option2Text, string option2Response)
    {
        contact1Prompts.Add(new ResponsePrompt {
            promptText = promptText,
            option1Text = option1Text,
            option1Response = option1Response,
            option2Text = option2Text,
            option2Response = option2Response
        });
    }
    
    public void AddResponsePromptToContact2(string promptText, string option1Text, string option1Response, string option2Text, string option2Response)
    {
        contact2Prompts.Add(new ResponsePrompt {
            promptText = promptText,
            option1Text = option1Text,
            option1Response = option1Response,
            option2Text = option2Text,
            option2Response = option2Response
        });
    }
}