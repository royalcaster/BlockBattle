BlockBattle - Cursor Implementation Plan

Game Concept


A VR multiplayer block-building game where two players race to recreate structures from a 3D reference. Players grab wooden blocks, stack them realistically (full physics, no snapping), then validate their build against the reference. After building, players shoot down the opponent's structure with a slingshot.

Core Design Principles

- Realistic physics: Blocks can fall, topple, slide - no snapping or auto-alignment

- End-state validation: Player presses buzzer when done, system compares their build to reference structure

- Tolerance-based matching: Blocks don't need pixel-perfect placement, just "close enough" in position and rotation

Validation Approach


Store reference structure as a collection of block transforms (position, rotation, block type). When player submits, iterate through placed blocks and match each to the closest reference block. Calculate overall accuracy percentage based on position distance and rotation delta. A block is "correct" if within configurable thresholds (e.g., 5cm position, 15° rotation).


---

Implementation Phases

Phase 1: Project Setup

- Create Unity project with XR Interaction Toolkit

- Configure XR Rig with hand controllers

- Setup basic scene with floor and table

Phase 2: Grabbable Blocks

- Create block prefabs (cube, cylinder, triangle, arch, etc.)

- Add Rigidbody with realistic mass/friction

- Add XRGrabInteractable for hand interaction

- Configure continuous collision detection for stable stacking

Phase 3: Reference Structure System

- Create ScriptableObject to define a structure (list of block type + transform pairs)

- Create editor tool or manual workflow to "record" a structure

- Create 3D preview display (hologram-style) showing target structure

Phase 4: Validation System

- Create BuildValidator that collects all placed blocks on/near table

- Implement matching algorithm: for each reference block, find closest placed block of same type

- Calculate position error (Vector3.Distance) and rotation error (Quaternion.Angle)

- Return accuracy percentage and per-block results

Phase 5: Game Flow (Single Player)

- Spawn blocks on table at round start

- Display reference structure as rotatable 3D preview

- Buzzer button to trigger validation

- Show results UI (percentage, which blocks were off)

Phase 6: Slingshot Mechanic

- Create slingshot prefab with pull-back interaction

- Spawn projectiles on release with force based on pull distance

- Add collision handling to knock over blocks

Phase 7: Multiplayer Foundation

- Setup Unity Netcode for GameObjects

- Sync block positions (only when at rest to reduce traffic)

- Sync game state (timer, phase, scores)

- Each player has their own table/workspace


---

Key Technical Decisions

- Use Rigidbody.collisionDetectionMode = ContinuousDynamic for held blocks

- Switch to Discrete after block is placed and settled (performance)

- Reference blocks identified by unique ID, not position in hierarchy

- Validation runs on host, results synced to clients