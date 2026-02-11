# Dialogue System Setup Guide

This guide explains how to set up and customize the refactored dialogue system using prefabs and GameObjects.

## Architecture Overview

The dialogue system consists of three main components:

| Component | Purpose |
|-----------|---------|
| **DialogueManager** | Handles Ink story logic, input, and game state |
| **DialogueUI** | Manages visual presentation using prefab references |
| **DialogueUISettings** | ScriptableObject for styling (fonts, colors, animations) |

## Quick Start

### Step 1: Create the Dialogue Canvas Prefab

1. In Unity, create a new **UI > Canvas** and name it `DialogueCanvas`
2. Set Canvas properties:
   - Render Mode: `Screen Space - Overlay`
   - Sort Order: `100` (ensures dialogue appears on top)

3. Create the following hierarchy under the Canvas:

```
DialogueCanvas
├── DialoguePanel (Panel)
│   ├── DialogueText (TextMeshPro - Text)
│   ├── SpeakerNameContainer (Panel) [Optional]
│   │   └── SpeakerNameText (TextMeshPro - Text)
│   ├── ChoiceContainer (Empty GameObject)
│   └── ContinueIndicator (Image/Text) [Optional]
```

### Step 2: Configure the DialoguePanel

1. Add an **Image** component for background
2. Set anchors to position the panel (recommended: bottom of screen)
   - Anchor Min: `(0, 0)`
   - Anchor Max: `(1, 0.3)`
3. Add padding using offset values

### Step 3: Configure DialogueText

1. Add **TextMeshProUGUI** component
2. Set anchors to fill parent with padding:
   - Anchor Min: `(0, 0.2)`
   - Anchor Max: `(1, 1)`
3. Configure text alignment and overflow settings

### Step 4: Configure ChoiceContainer

1. Add a **Vertical Layout Group** (or Horizontal/Grid depending on preference)
2. Configure spacing and alignment:
   ```
   Spacing: 10
   Child Alignment: Middle Center
   Control Child Size: Width (checked), Height (checked)
   ```
3. Position at the bottom of DialoguePanel

### Step 5: Create Choice Button Prefab

1. Create **UI > Button - TextMeshPro**
2. Configure the button:
   - Add Image component for background
   - Configure TextMeshProUGUI child for button text
3. Save as prefab: `Assets/Prefabs/ChoiceButton.prefab`

### Step 6: Add DialogueUI Component

1. Select `DialogueCanvas` in hierarchy
2. Add Component > **DialogueUI**
3. Assign references:
   - Dialogue Panel: `DialoguePanel`
   - Dialogue Text: `DialogueText`
   - Choice Container: `ChoiceContainer`
   - Choice Button Prefab: `ChoiceButton` prefab
   - (Optional) Speaker Name Text, Continue Indicator

### Step 7: Configure DialogueManager

1. Create an empty GameObject named `DialogueManager`
2. Add Component > **DialogueManager**
3. Assign the **DialogueUI** reference
4. Save as prefab

## Creating UI Settings (Optional)

1. Right-click in Project window
2. Select **Create > Dialogue > UI Settings**
3. Configure styling:

| Setting | Description |
|---------|-------------|
| Dialogue Font | TMP Font for dialogue text |
| Dialogue Font Size | Size of dialogue text |
| Dialogue Text Color | Color of dialogue text |
| Choice Button Font | TMP Font for choice buttons |
| Choice Button Font Size | Size of choice button text |
| Button Normal/Highlighted/Pressed Color | Button state colors |
| Use Typewriter Effect | Enable character-by-character reveal |
| Typewriter Speed | Characters per second |
| Panel Fade Duration | Fade in/out animation time |

4. Assign to DialogueUI component's **UI Settings** field

## Prefab Structure Reference

### Recommended DialoguePanel Layout

```
DialoguePanel (Image)
├── RectTransform
│   ├── Anchor Min: (0, 0)
│   ├── Anchor Max: (1, 0.3)
│   └── Pivot: (0.5, 0)
├── Image
│   └── Color: RGBA(20, 20, 20, 200)
└── CanvasGroup (for fade effects)
```

### Recommended ChoiceButton Layout

```
ChoiceButton (Button)
├── RectTransform
│   ├── Width: 300
│   └── Height: 50
├── Image (background)
│   └── Color: RGBA(50, 50, 50, 200)
├── Button
│   └── Transition: Color Tint
└── Text (TMP)
    ├── Alignment: Center
    ├── Font Size: 18
    └── Color: White
```

## Using Speaker Names (Ink Tags)

Add speaker names to dialogue using Ink tags:

```ink
# speaker: Guard
Hey, you there! Stop!

# speaker: Player
Who, me?
```

The `speaker:` tag will be parsed and displayed in the SpeakerNameText component if configured.

## API Reference

### DialogueManager Methods

```csharp
// Start dialogue with an Ink story
DialogueManager.GetInstance().EnterDialogueMode(inkJSONAsset, npcTransform);

// Start at specific knot
DialogueManager.GetInstance().EnterDialogueMode(inkJSONAsset, "knotName", npcTransform);

// Exit dialogue
DialogueManager.GetInstance().ExitDialogueMode();

// Check if dialogue is active
bool isPlaying = DialogueManager.GetInstance().IsDialoguePlaying();

// Get/Set Ink variables
int karma = DialogueManager.GetInstance().GetInkVariable<int>("player_karma");
DialogueManager.GetInstance().SetInkVariable("player_karma", 10);
```

### DialogueManager Events

```csharp
DialogueManager dm = DialogueManager.GetInstance();

dm.OnDialogueStarted += () => Debug.Log("Dialogue started");
dm.OnDialogueEnded += () => Debug.Log("Dialogue ended");
dm.OnDialogueTextChanged += (text) => Debug.Log($"New text: {text}");
dm.OnChoicesPresented += (choices) => Debug.Log($"Showing {choices.Count} choices");
```

### DialogueUI Methods

```csharp
DialogueUI ui = FindObjectOfType<DialogueUI>();

ui.Show();  // Show dialogue panel
ui.Hide();  // Hide dialogue panel
ui.SetDialogueText("Hello!", "Guard");  // Set text with optional speaker name
ui.DisplayChoices(new List<string> { "Option 1", "Option 2" });
ui.ClearChoices();
ui.SkipTypewriter();  // Skip to end of typewriter effect
```

## NPC Setup

### Using NPCInteraction

```csharp
// On your NPC GameObject:
// 1. Add NPCInteraction component
// 2. Assign Ink JSON Asset
// 3. Set NPC Name and Interaction Prompt
```

### Using DialogueInteractable (Event-based)

```csharp
// On any interactable object:
// 1. Add DialogueInteractable component
// 2. Set Knot Name (the Ink knot to start from)
// 3. Ensure GameEventsManager handles the onEnterDialogue event
```

## Troubleshooting

### Dialogue panel not appearing
- Ensure Canvas Sort Order is high enough (100+)
- Check DialoguePanel is assigned in DialogueUI
- Verify Canvas has GraphicRaycaster component

### Choices not clickable
- Ensure ChoiceButton prefab has Button component
- Check EventSystem exists in scene
- Verify ChoiceContainer has proper RectTransform size

### Text not visible
- Check TextMeshProUGUI font is assigned
- Verify text color has alpha > 0
- Ensure text object is active and within parent bounds

### Typewriter effect not working
- Enable "Use Typewriter Effect" in DialogueUISettings
- Assign DialogueUISettings to DialogueUI component
- Set Typewriter Speed > 0

## Migration from Old System

If upgrading from the previous hard-coded system:

1. Create new DialogueCanvas prefab following this guide
2. Add DialogueUI component and configure references
3. Update DialogueManager reference to point to new DialogueUI
4. (Optional) Create DialogueUISettings for styling
5. Delete old hard-coded positioning fields from inspector
6. Test with existing Ink files (no changes needed to .ink files)

The new system is fully compatible with existing Ink dialogue files and NPC interaction scripts.
