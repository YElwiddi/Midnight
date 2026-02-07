# Crosshair Interaction System - Setup Guide

## 1. Setting Up the Crosshair

1. Create a new script called `CrosshairManager.cs` in your project
2. Copy the CrosshairManager code into this script
3. Attach the CrosshairManager script to your player camera

## 2. Setting Up the Interaction System

1. Create a new script called `InteractionSystem.cs` in your project
2. Copy the InteractionSystem code into this script
3. Attach the InteractionSystem script to your player camera
4. Configure the settings in the Inspector:
   - Interaction Distance: How far the player can interact (default: 3 units)
   - Interact Key: Which key triggers interaction (default: E)
   - Highlight Crosshair Color: Color when hovering over interactable items

## 3. Creating an Interactable Layer

1. Go to Edit > Project Settings > Tags and Layers
2. Under Layers, add a new user layer named "Interactable" (e.g., layer 8)
3. This layer will be used to identify objects that can be interacted with

## 4. Making Objects Interactable

1. Create a new script called `InteractableItem.cs` in your project
2. Copy the InteractableItem code into this script
3. For any object you want to be interactable:
   - Attach the InteractableItem script to the object
   - Set the object's layer to "Interactable"
   - Configure the item name and interaction verb in the Inspector
   - Optionally enable visual effects like highlighting or rotation

## 5. Final Configuration

1. Select your player camera with the InteractionSystem script
2. In the Inspector, set the Interactable Mask to include your "Interactable" layer
3. Make sure TextMeshPro is installed in your project (Window > Package Manager)
   - Required for the interaction prompt text

## 6. How It Works

- The crosshair stays in the center of the screen
- When you point at an interactable object:
  - The crosshair changes color
  - A prompt appears showing what action you can take
  - Pressing the interact key (E) triggers the interaction
- Each interactable item can define what happens when interacted with

## 7. Customizing Interactables

You can extend the InteractableItem script for different types of interactions:
- Collectible items
- Doors that open
- Switches or levers
- NPCs to talk to
- And more!

Simply implement the IInteractable interface in your custom scripts.