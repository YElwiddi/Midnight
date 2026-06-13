# A Graveyard Shift - Project Instructions

## Project Overview
- **Game**: A Graveyard Shift — PSX-style VHS horror game
- **Engine**: Unity 6 (6000.x) with Universal Render Pipeline (URP)
- **Previous Project**: "The Mirror at Midnight" (AGraveyardShift folder) — lessons learned applied here

## Known Pitfalls from The Mirror at Midnight
1. **VHS Effect used legacy `OnRenderImage`** — This is a Built-in RP method and does NOT work properly with URP. Must use a **URP Renderer Feature + ScriptableRenderPass** instead.
2. **PSXShaderKit uses CGPROGRAM/Built-in shader syntax** — Conflicts with URP. Need PSX shaders written for URP (ShaderLab/HLSL with URP includes) or use a compatibility approach.
3. **Terrain assets were loosely named** ("New Terrain", "New Terrain 1", "New Terrain 2") — No organization or clear purpose for each tile.
4. **No custom URP Renderer/Pipeline Asset in Settings** — Should create dedicated assets from the start.
5. **Quality settings had terrain pixel error of 1** — Very high quality, potentially too expensive. Use 5-8 for PSX aesthetic.
6. **Render Graph was disabled** — Should evaluate whether to use it for the new project.

## World Layout
- Modularized retro-style house in the center of the map
- Stretch of land on one side
- Chasm with water at the bottom separating two land masses
- Bridge connecting the two land areas

## Visual Style
- PSX-era low-poly aesthetic (vertex snapping, affine texture warping, low-res textures)
- VHS post-processing overlay (scanlines, RGB shift, grain, glitch, vignette)
- Target internal render resolution: 320x240 or 480x360 upscaled
- Limited color palette / color banding

## Architecture Decisions
- URP with custom Renderer Features for all post-processing
- Proper ScriptableRenderPass for VHS effect (NOT OnRenderImage)
- Organized folder structure from day one
- Terrain setup with proper naming and clear tile purpose

## Folder Structure (Target)
```
Assets/
├── Art/
│   ├── Materials/
│   ├── Models/
│   ├── Textures/
│   └── Skyboxes/
├── Audio/
│   ├── Music/
│   ├── SFX/
│   └── Ambience/
├── Prefabs/
│   ├── Environment/
│   ├── Characters/
│   └── Interactables/
├── Rendering/
│   ├── PSXShaders/
│   ├── VHSEffect/
│   ├── RenderPipelineAssets/
│   └── PostProcessing/
├── Scenes/
├── Scripts/
│   ├── Core/
│   ├── Player/
│   ├── AI/
│   ├── Dialogue/
│   ├── UI/
│   └── Effects/
├── Settings/
└── Terrain/
    ├── Data/
    ├── Layers/
    └── Textures/
```
