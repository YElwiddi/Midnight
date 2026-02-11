# Dialogue UI Creation Guide - Step by Step

This guide walks you through creating the dialogue UI from scratch in Unity.

---

## Prerequisites

- TextMeshPro package installed (Window > TextMeshPro > Import TMP Essential Resources)
- The refactored dialogue scripts in place (DialogueManager.cs, DialogueUI.cs, DialogueUISettings.cs)

---

## Part 1: Create the Dialogue Canvas

### Step 1.1: Create the Canvas

1. In the **Hierarchy** window, right-click in empty space
2. Select **UI > Canvas**
3. Rename it to `DialogueCanvas`

### Step 1.2: Configure Canvas Settings

Select `DialogueCanvas` and in the **Inspector**:

**Canvas Component:**
| Property | Value |
|----------|-------|
| Render Mode | Screen Space - Overlay |
| Sort Order | 100 |

**Canvas Scaler Component:**
| Property | Value |
|----------|-------|
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | 1920 x 1080 |
| Match | 0.5 (slider in middle) |

**Graphic Raycaster Component:**
- Keep default settings (required for button clicks)

---

## Part 2: Create the Dialogue Panel

### Step 2.1: Create the Panel GameObject

1. Right-click on `DialogueCanvas` in Hierarchy
2. Select **UI > Panel**
3. Rename it to `DialoguePanel`

### Step 2.2: Position the Panel (Bottom of Screen)

Select `DialoguePanel` and configure the **RectTransform**:

**Method A: Using Anchor Presets**
1. Click the anchor preset box (square icon in RectTransform)
2. Hold **Alt + Shift** and click the bottom-stretch option (bottom row, middle)

**Method B: Manual Values**
| Property | Value |
|----------|-------|
| Anchor Min | X: 0, Y: 0 |
| Anchor Max | X: 1, Y: 0.25 |
| Left | 50 |
| Right | 50 |
| Top | 0 |
| Bottom | 30 |
| Pivot | X: 0.5, Y: 0 |

### Step 2.3: Style the Panel Background

Select `DialoguePanel` and configure the **Image** component:

| Property | Value |
|----------|-------|
| Color | R: 20, G: 20, B: 30, A: 230 |
| Raycast Target | Checked |

**Optional: Add rounded corners**
1. Create a rounded rectangle sprite in your image editor
2. Assign it to the Image component's Source Image
3. Set Image Type to "Sliced"

### Step 2.4: Add CanvasGroup (for fade effects)

1. With `DialoguePanel` selected, click **Add Component**
2. Search for and add **Canvas Group**
3. Keep default settings (Alpha: 1, Interactable: checked, Blocks Raycasts: checked)

---

## Part 3: Create the Dialogue Text

### Step 3.1: Create the Text Object

1. Right-click on `DialoguePanel` in Hierarchy
2. Select **UI > Text - TextMeshPro**
3. Rename it to `DialogueText`

### Step 3.2: Position the Text

Select `DialogueText` and configure the **RectTransform**:

| Property | Value |
|----------|-------|
| Anchor Min | X: 0, Y: 0.25 |
| Anchor Max | X: 1, Y: 1 |
| Left | 30 |
| Right | 30 |
| Top | 20 |
| Bottom | 10 |
| Pivot | X: 0.5, Y: 0.5 |

### Step 3.3: Configure Text Settings

Select `DialogueText` and configure **TextMeshPro - Text (UI)**:

| Property | Value |
|----------|-------|
| Text | (leave empty or add placeholder like "Dialogue text appears here...") |
| Font Asset | Your preferred font (or LiberationSans SDF) |
| Font Size | 28 |
| Vertex Color | White (R: 255, G: 255, B: 255, A: 255) |
| Alignment | Left, Top |
| Wrapping | Enabled |
| Overflow | Ellipsis |
| Raycast Target | Unchecked (important for performance) |

**Extra Settings (expand if collapsed):**
| Property | Value |
|----------|-------|
| Margins | Left: 10, Top: 5, Right: 10, Bottom: 5 |

---

## Part 4: Create the Choice Container

### Step 4.1: Create the Container

1. Right-click on `DialoguePanel` in Hierarchy
2. Select **Create Empty**
3. Rename it to `ChoiceContainer`

### Step 4.2: Position the Container

Select `ChoiceContainer` and configure the **RectTransform**:

| Property | Value |
|----------|-------|
| Anchor Min | X: 0, Y: 0 |
| Anchor Max | X: 1, Y: 0.3 |
| Left | 50 |
| Right | 50 |
| Top | 0 |
| Bottom | 10 |
| Pivot | X: 0.5, Y: 0 |

### Step 4.3: Add Vertical Layout Group

1. With `ChoiceContainer` selected, click **Add Component**
2. Search for and add **Vertical Layout Group**

Configure the Vertical Layout Group:

| Property | Value |
|----------|-------|
| Padding | Left: 0, Right: 0, Top: 5, Bottom: 5 |
| Spacing | 8 |
| Child Alignment | Middle Center |
| Control Child Size - Width | Checked |
| Control Child Size - Height | Unchecked |
| Use Child Scale - Width | Unchecked |
| Use Child Scale - Height | Unchecked |
| Child Force Expand - Width | Unchecked |
| Child Force Expand - Height | Unchecked |

### Step 4.4: Add Content Size Fitter

1. With `ChoiceContainer` still selected, click **Add Component**
2. Search for and add **Content Size Fitter**

Configure:

| Property | Value |
|----------|-------|
| Horizontal Fit | Preferred Size |
| Vertical Fit | Preferred Size |

---

## Part 5: Create the Choice Button Prefab

### Step 5.1: Create a Temporary Button

1. Right-click on `ChoiceContainer` in Hierarchy
2. Select **UI > Button - TextMeshPro**
3. Rename it to `ChoiceButton`

### Step 5.2: Configure Button RectTransform

Select `ChoiceButton` and configure:

| Property | Value |
|----------|-------|
| Width | 400 |
| Height | 45 |
| Pivot | X: 0.5, Y: 0.5 |

### Step 5.3: Style the Button Background

Select `ChoiceButton` and configure the **Image** component:

| Property | Value |
|----------|-------|
| Color | R: 40, G: 40, B: 50, A: 220 |
| Raycast Target | Checked |

### Step 5.4: Configure Button Component

Select `ChoiceButton` and configure the **Button** component:

| Property | Value |
|----------|-------|
| Interactable | Checked |
| Transition | Color Tint |
| Target Graphic | (should auto-assign to Image) |
| Normal Color | R: 255, G: 255, B: 255, A: 255 |
| Highlighted Color | R: 200, G: 200, B: 255, A: 255 |
| Pressed Color | R: 150, G: 150, B: 200, A: 255 |
| Selected Color | R: 200, G: 200, B: 255, A: 255 |
| Disabled Color | R: 128, G: 128, B: 128, A: 128 |
| Color Multiplier | 1 |
| Fade Duration | 0.1 |

### Step 5.5: Configure Button Text

1. Expand `ChoiceButton` in Hierarchy
2. Select the child `Text (TMP)` object
3. Rename it to `ButtonText`

Configure **TextMeshPro - Text (UI)**:

| Property | Value |
|----------|-------|
| Text | Choice Text |
| Font Asset | Same font as DialogueText |
| Font Size | 20 |
| Vertex Color | White |
| Alignment | Center, Middle |
| Wrapping | Disabled |
| Overflow | Ellipsis |
| Raycast Target | Unchecked |

Configure **RectTransform** for ButtonText:

| Property | Value |
|----------|-------|
| Anchor Min | X: 0, Y: 0 |
| Anchor Max | X: 1, Y: 1 |
| Left | 15 |
| Right | 15 |
| Top | 5 |
| Bottom | 5 |

### Step 5.6: Add Layout Element to Button

1. Select `ChoiceButton`
2. Add Component > **Layout Element**

Configure:

| Property | Value |
|----------|-------|
| Min Width | 200 |
| Min Height | 45 |
| Preferred Width | 400 |
| Preferred Height | 45 |
| Flexible Width | 0 |
| Flexible Height | 0 |

### Step 5.7: Save as Prefab

1. In the **Project** window, navigate to `Assets/Prefabs`
2. Drag `ChoiceButton` from Hierarchy into the Prefabs folder
3. Choose **Original Prefab** when prompted
4. Delete `ChoiceButton` from the Hierarchy (the container should now be empty)

---

## Part 6: Create Continue Indicator (Optional)

### Step 6.1: Create the Indicator

1. Right-click on `DialoguePanel` in Hierarchy
2. Select **UI > Text - TextMeshPro**
3. Rename it to `ContinueIndicator`

### Step 6.2: Position the Indicator

Configure **RectTransform**:

| Property | Value |
|----------|-------|
| Anchor Min | X: 1, Y: 0 |
| Anchor Max | X: 1, Y: 0 |
| Pos X | -30 |
| Pos Y | 20 |
| Width | 100 |
| Height | 30 |
| Pivot | X: 1, Y: 0 |

### Step 6.3: Configure Indicator Text

Configure **TextMeshPro - Text (UI)**:

| Property | Value |
|----------|-------|
| Text | Click to continue... |
| Font Size | 16 |
| Font Style | Italic |
| Vertex Color | R: 200, G: 200, B: 200, A: 180 |
| Alignment | Right, Middle |
| Raycast Target | Unchecked |

---

## Part 7: Create Speaker Name Display (Optional)

### Step 7.1: Create Speaker Name Container

1. Right-click on `DialoguePanel` in Hierarchy
2. Select **Create Empty**
3. Rename it to `SpeakerNameContainer`

### Step 7.2: Position the Container

Configure **RectTransform**:

| Property | Value |
|----------|-------|
| Anchor Min | X: 0, Y: 1 |
| Anchor Max | X: 0, Y: 1 |
| Pos X | 50 |
| Pos Y | 15 |
| Width | 200 |
| Height | 40 |
| Pivot | X: 0, Y: 0 |

### Step 7.3: Add Background Image

1. With `SpeakerNameContainer` selected, click **Add Component**
2. Add **Image**

Configure:

| Property | Value |
|----------|-------|
| Color | R: 60, G: 40, B: 80, A: 240 |

### Step 7.4: Create Speaker Name Text

1. Right-click on `SpeakerNameContainer`
2. Select **UI > Text - TextMeshPro**
3. Rename it to `SpeakerNameText`

Configure **RectTransform**:

| Property | Value |
|----------|-------|
| Anchor Min | X: 0, Y: 0 |
| Anchor Max | X: 1, Y: 1 |
| Left | 15 |
| Right | 15 |
| Top | 5 |
| Bottom | 5 |

Configure **TextMeshPro - Text (UI)**:

| Property | Value |
|----------|-------|
| Text | Speaker Name |
| Font Size | 18 |
| Font Style | Bold |
| Vertex Color | R: 255, G: 220, B: 100, A: 255 |
| Alignment | Left, Middle |
| Raycast Target | Unchecked |

---

## Part 8: Add DialogueUI Component

### Step 8.1: Add the Component

1. Select `DialogueCanvas` in Hierarchy
2. Click **Add Component**
3. Search for and add **Dialogue UI**

### Step 8.2: Assign References

With `DialogueCanvas` selected, in the **Dialogue UI** component:

| Field | Drag From Hierarchy |
|-------|---------------------|
| Dialogue Panel | DialoguePanel |
| Dialogue Panel Background | DialoguePanel (Image will auto-detect) |
| Continue Indicator | ContinueIndicator (if created) |
| Dialogue Text | DialogueText |
| Speaker Name Text | SpeakerNameText (if created) |
| Speaker Name Container | SpeakerNameContainer (if created) |
| Choice Container | ChoiceContainer |
| Choice Button Prefab | ChoiceButton (drag from Project/Prefabs folder) |

---

## Part 9: Save as Prefab

### Step 9.1: Create the Prefab

1. In the **Project** window, navigate to `Assets/Prefabs`
2. Drag `DialogueCanvas` from Hierarchy into the Prefabs folder
3. Choose **Original Prefab** when prompted

### Step 9.2: Keep in Scene or Delete

- **Keep**: Leave DialogueCanvas in the scene if this is your main game scene
- **Delete**: Remove from Hierarchy if you'll instantiate it at runtime or include in a different scene

---

## Part 10: Configure DialogueManager

### Step 10.1: Find or Create DialogueManager

1. Look for existing `DialogueManager` in your scene
2. If none exists, create an empty GameObject named `DialogueManager`
3. Add the **Dialogue Manager** component if not present

### Step 10.2: Assign DialogueUI Reference

In the **Dialogue Manager** component:

| Field | Value |
|-------|-------|
| Dialogue UI | Drag DialogueCanvas from Hierarchy (or leave empty for auto-find) |
| Continue Key | Mouse0 (left click) |
| Exit Key | Escape |
| Click To Continue | Checked |

---

## Part 11: Create UI Settings (Optional)

### Step 11.1: Create the ScriptableObject

1. In **Project** window, right-click in `Assets/Scripts/Dialogue` folder
2. Select **Create > Dialogue > UI Settings**
3. Name it `DefaultDialogueUISettings`

### Step 11.2: Configure Settings

Select the new asset and configure in Inspector:

**Dialogue Text Style:**
| Property | Recommended Value |
|----------|-------------------|
| Dialogue Font | Your preferred TMP font |
| Dialogue Font Size | 28 |
| Dialogue Text Color | White |
| Dialogue Font Style | Normal |

**Choice Button Text Style:**
| Property | Recommended Value |
|----------|-------------------|
| Choice Button Font | Same as dialogue font |
| Choice Button Font Size | 20 |
| Choice Button Text Color | White |
| Choice Button Font Style | Normal |

**Choice Button Colors:**
| Property | Recommended Value |
|----------|-------------------|
| Button Normal Color | R: 40, G: 40, B: 50, A: 220 |
| Button Highlighted Color | R: 60, G: 60, B: 80, A: 240 |
| Button Pressed Color | R: 30, G: 30, B: 40, A: 255 |
| Button Disabled Color | R: 80, G: 80, B: 80, A: 128 |

**Animation Settings:**
| Property | Recommended Value |
|----------|-------------------|
| Use Typewriter Effect | Unchecked (or Checked for effect) |
| Typewriter Speed | 50 |
| Panel Fade In Duration | 0.2 |
| Panel Fade Out Duration | 0.15 |

**Speaker Name:**
| Property | Recommended Value |
|----------|-------------------|
| Show Speaker Name | Checked (if you created speaker name objects) |
| Speaker Name Font | Same font, or a bold variant |
| Speaker Name Font Size | 18 |
| Speaker Name Color | R: 255, G: 220, B: 100, A: 255 |

### Step 11.3: Assign to DialogueUI

1. Select `DialogueCanvas` in Hierarchy (or open prefab)
2. In **Dialogue UI** component, find **UI Settings** field
3. Drag `DefaultDialogueUISettings` from Project window into the field

---

## Final Hierarchy Structure

Your Hierarchy should look like this:

```
DialogueCanvas
├── DialoguePanel
│   ├── DialogueText
│   ├── ChoiceContainer (empty, buttons spawn here)
│   ├── ContinueIndicator (optional)
│   └── SpeakerNameContainer (optional)
│       └── SpeakerNameText
└── (EventSystem - auto-created with Canvas)

DialogueManager (separate GameObject)
```

---

## Testing

1. Enter **Play Mode**
2. Interact with an NPC that has `NPCInteraction` component
3. Verify:
   - Panel appears at bottom of screen
   - Dialogue text displays correctly
   - Choice buttons appear and are clickable
   - Clicking advances dialogue when no choices
   - ESC closes dialogue
   - Panel disappears when dialogue ends

---

## Troubleshooting

### Panel not visible
- Check Canvas Sort Order is 100+
- Verify DialoguePanel Image alpha > 0
- Ensure DialoguePanel is active in hierarchy

### Text not showing
- Verify DialogueText is assigned in DialogueUI
- Check font is assigned in TextMeshPro component
- Ensure text color alpha > 0

### Buttons not appearing
- Verify ChoiceButtonPrefab is assigned
- Check ChoiceContainer has LayoutGroup component
- Ensure button prefab is saved correctly

### Buttons not clickable
- Verify EventSystem exists in scene
- Check GraphicRaycaster is on Canvas
- Ensure Button component's Interactable is checked
- Verify nothing is blocking raycasts above buttons

### Layout issues
- Check LayoutGroup settings match guide
- Verify ContentSizeFitter is on ChoiceContainer
- Ensure button prefab has LayoutElement component
