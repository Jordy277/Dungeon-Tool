# Algorithmic Order Collapse - README

## Summary
This document outlines how to set up and use the **Algorithmic Order Collapse** tool for Unity.  
It explains setup, prefab requirements, workflow, editable parameters, and known issues.  
The tool procedurally generates modular 3D layouts using a connector-based **Wave Function Collapse (WFC)** algorithm.  
It supports weighted module selection, branching and looping layouts, overlap prevention, and visual playback of the generation process.

---

## Project Setup
1. Open the Unity project (**Unity version 6000.0.60f1** or later).  
2. Open the tool via **Tools → WFC Dungeon Generator**.  
3. Assign your **Module Entries** (ScriptableObjects linking to prefabs) in the entry list.  
4. Define the maximum number of modules to generate.  
5. Optionally define a seed, or enable **Random Seed** for unique layouts each run.  
6. Assign a **Parent Object** (recommended: an empty GameObject in your scene).  
7. Press **Generate Dungeon** to build the layout. Prefabs will be automatically positioned and rotated based on connector alignment rules.  
8. Optionally enable **Visualise After Generate** to see the layout build step-by-step.  

---

## Editable Parameters

### Weighted Modules
Add your WFC Module Entries here. Each entry holds a prefab and a base weight value controlling its spawn probability.  
- Higher weights = more likely to appear earlier in generation.

### Max Modules
Defines how many prefabs the generator attempts to place before stopping.  
- Larger numbers produce larger dungeons (longer generation time).

### Random Seed
When enabled, a new random seed is used each run. Disable to use a fixed seed for repeatable layouts.

### Seed
Custom seed value for reproducible testing.

### Parent Object
Root GameObject under which all generated modules will be placed.

### Clear Before Generate
If enabled, removes all previous modules under the parent before generating again.

### Visualise After Generate
Plays back the generation process, placing prefabs one at a time for debugging or demonstration.

### Step Delay
Defines delay between prefab placements during visualisation (default: **0.15 seconds**).

---

## Prefab Requirements
Each prefab must follow the same structural conventions for correct alignment and rule matching:

- **Connectors:**  
  - Include child empty GameObjects named “Connector” for every open side.  
  - Z-axis (blue arrow) must point outward from the room’s open side.  
  - Connector should be flush with the wall or opening surface.

- **Pivot Position:**  
  - Pivot centred to the floor or middle of geometry.  
  - Apply rotation and scale in Blender before export.

- **Colliders:**  
  - Each prefab should contain accurate colliders for spatial validation.

- **Connector Count:**  
  - Prefabs with more connectors act as branching rooms and appear earlier.  
  - Prefabs with one connector act as dead ends and spawn later.

---

## Tool Workflow

### Step 1: Create Modular Assets (Blender 4.0 or equivalent)
1. Model modular rooms or corridors.  
2. Add empty axis objects for connectors.  
   - Set local Z-axis to face outward.  
   - Place the empty where two modules should connect.  
3. Parent each connector to the room’s geometry (`Ctrl + P > Object (Keep Transform)`).  
4. Apply all transforms (`Ctrl + A > Apply All Transforms`).  
5. Centre pivot (`Object > Set Origin > Origin to Geometry`).

### Step 2: Naming Conventions
- Connectors: `Connector_N`, `Connector_E`, etc.  
- Prefabs: logical names for debugging (e.g. *T-Junction Room*, *Corner*, *Corridor*).

### Step 3: Import to Unity
1. Export as **FBX** with:  
   - Selected Objects: Enabled  
   - Apply Transform: Enabled  
   - Forward: Z Forward  
   - Up: Y Up  
2. Drag FBX into Unity.  
3. Check pivot alignment and scale.  
4. Create prefabs for each module.  
5. Assign prefabs into new **WFCModuleEntry** assets:  
   - `Right-click > Create > WFC > Module Entry`  
   - Link prefab and assign Base Weight (1–5).

### Step 4: Use the Tool
1. Open **Tools > WFC Dungeon Generator**.  
2. Add module entries.  
3. Adjust Max Modules and seed options.  
4. Assign a parent object (empty GameObject).  
5. Click **Generate Dungeon**.  
6. Optionally enable visualisation to observe generation.

### Step 5: Post-Generation
- You can move, delete, or duplicate pieces freely.  
- To test variation:  
  - Change seed or weights.  
  - Modify prefab list to control theme or density.  
  - Increase **Max Modules** to expand layout size.

---

## Troubleshooting

### Tool-Level Issues
- **Nothing generates:**  
  Ensure at least one valid WFCModuleEntry with connectors is assigned.

- **Crashes during generation:**  
  May occur from too few valid connections or contradictory shapes.  
  Lower **Max Modules** or fix connector alignment.

- **Repeats same layout:**  
  Change the seed or enable Random Seed.

- **Fails early / only one prefab spawns:**  
  Check colliders and ensure connectors face outward.  
  Prefabs with single connectors may terminate early.

### Prefab Debug Tips
- Confirm pivot is centred and transforms applied.  
- Maintain consistent connector spacing and height.  
- Ensure opposing connectors (e.g., East and West) are equal and oppositely oriented.

---

## Known Issues
- **Performance:**  
  Large **Max Module** values (>500) slow generation due to recursion depth.  

- **Loop Closure Accuracy:**  
  Occasionally misaligns in tight junctions.  

- **Branch Overload:**  
  Prefabs with many connectors cause exponential branching.  
  Lower their weight for stability.

---

## Example Weight Guidelines

| Connector Count | Suggested Base Weight | Description |
|------------------|------------------------|-------------|
| 1 Connector | 0.5 – 1.0 | Dead-end rooms, used to close branches |
| 2 Connectors | 1.0 – 2.0 | Corridors, straight sections, corners |
| 3 Connectors | 2.0 – 3.5 | T-junctions, 3-way rooms, branching nodes |
| 4+ Connectors | 3.5 – 5.0 | Central hubs, spawn early for connectivity |

---

## Version and Compatibility
- **Tool Name:** Algorithmic Order Collapse  
- **Unity Version:** 6000.0.60f1 (LTS)  
- **Supported Pipeline:** URP and HDRP compatible  
- **Dependencies:** None beyond Unity’s standard Editor and Physics APIs  

---

## Author Notes
This version represents the complete iteration cycle with all core features implemented:
- Connector-based placement  
- Recursive backtracking  
- Weighted probabilities  
- Loop-closure support  
- Playback visualisation  

Design prioritises stability and creative control while remaining expandable for future rule-based systems.
