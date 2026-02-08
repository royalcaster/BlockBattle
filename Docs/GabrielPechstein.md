# Gabriel Pechstein - BlockBattle Project Diary

## Overview

Main contributions focused on core gameplay systems: validation, reference structures, shelf mechanics, destruction phase, and game flow orchestration. Worked primarily on the technical architecture and gameplay loop implementation.

## Development Log

### December 2025 - Reference Structure System
**Status**: Implemented

**What was done:**
- Implemented holographic reference structure system with colored blocks
- Created `ReferenceStructureSpawner` component
- Added custom holographic shader with transparency, emission, and fresnel effects
- Implemented XR controller rotation for reference structures

**Problems & Solutions:**
- **Problem**: Reference structure needed to be clearly visible but not confused with player's build
- **Solution**: Applied custom holographic shader with transparency and glow effects. Makes reference clearly distinct while maintaining visibility.

**Decisions:**
- Used ScriptableObject-based configuration (`BlockSpawnConfiguration`) for structure definitions
- Applied holographic effect via material rather than separate rendering system for performance
- Made reference structures static (kinematic) to prevent accidental interaction

**References**: 
- [Block System Documentation](../Assets/BlockBattle/Scripts/Blocks/README.md)
- [Reference Structure API](reference_structure_api.md)

---

### December 2025 - Validation System
**Status**: Implemented

**What was done:**
- Implemented `BuildValidator` with relative position matching
- Created `BuildZone` component for build area definition
- Added `BuildZonePlacementGuides` for visual building assistance
- Implemented auto-alignment system for flexible build orientation
- Added validation HUD for real-time feedback

**Problems & Solutions:**
- **Problem**: Absolute world coordinates break when table is rotated in VR space
- **Solution**: Implemented relative position matching. Store reference positions relative to build center, then transform to world space accounting for current table rotation. This allows players to rotate the table without breaking validation.

- **Problem**: Players may build at different orientations than reference structure
- **Solution**: Added auto-alignment system that detects best rotation offset by trying multiple angles and selecting the one with highest match count. Locks after first block to prevent jitter.

**Decisions:**
- Used tolerance-based matching (10cm position, 20° rotation) to allow for natural VR imprecision
- Made rotation validation optional (can be disabled for simpler gameplay)
- Implemented presence-only validation mode for testing/relaxed gameplay

**References**: 
- [Validation System Documentation](../Assets/BlockBattle/Scripts/Validation/README.md)
- [Validation HUD API](validation_hud_api.md)
- [Build Validator API](build_validator_api.md)

---

### January 2026 - Shelf System
**Status**: Implemented

**What was done:**
- Implemented `ShelfBlockSpawner` component merging block spawning with door-triggered ejection
- Added physics-based door mechanics with HingeJoint motors
- Implemented block ejection system with randomized physics
- Created `ShelfProgressUI` for block return tracking
- Integrated shelf system into game loop

**Problems & Solutions:**
- **Problem**: Need haptic feedback when opening doors in VR
- **Solution**: Enabled HingeJoint motor at 30° angle threshold. This provides resistance and auto-opens doors, making interaction feel more physical and responsive.

- **Problem**: Predictable block trajectories are boring
- **Solution**: Applied random spread (0-30% default), random force variation (80-120%), and random torque for realistic tumbling. Creates chaotic, fun block ejection.

**Decisions:**
- Used two-door system (left/right) for more interactive gameplay
- System triggers when either door opens past threshold (70°), but only resets when BOTH close (60°)
- Blocks spawn as kinematic and become dynamic only when ejected

**References**: 
- [Shelf System Documentation](../Assets/BlockBattle/Scripts/Shelf/README.md)
- [Detailed Shelf System Documentation](shelf_system.md)

---

### January 2026 - Destruction Phase & Slingshot
**Status**: Implemented

**What was done:**
- Implemented `VRSlingshot` with pull-back-and-release mechanics
- Created `DestructionPhaseManager` for destruction phase lifecycle
- Added pull-distance-based force calculation
- Implemented projectile physics and collision handling
- Integrated destruction phase into game loop

**Problems & Solutions:**
- **Problem**: Simple button press is not engaging in VR
- **Solution**: Calculate launch velocity from pull distance: `velocity = pullDistance * multiplier`. This gives players control over shot power and makes the mechanic more physical and satisfying.

- **Problem**: `AddForce` can be inconsistent with varying frame rates
- **Solution**: Set `Rigidbody.linearVelocity` directly. This gives precise, frame-rate-independent control over projectile speed.

**Decisions:**
- Used direct velocity assignment instead of AddForce for consistency
- Implemented periodic block checking (0.5s intervals) for performance
- Added completion delay to prevent false positives from physics settling

**References**: 
- [Destruction System Documentation](../Assets/BlockBattle/Scripts/Destruction/README.md)

---

### January 2026 - Game Loop & Level Progression
**Status**: Implemented

**What was done:**
- Implemented `LevelManager` for game flow orchestration
- Created phase-based architecture (Building → Destruction → WaitingForReturn → Countdown)
- Added timer management (stops only when blocks returned in final level)
- Implemented level progression system with ScriptableObject configurations
- Created teleportation system for phase transitions

**Problems & Solutions:**
- **Problem**: Different phases need different systems active (validation vs destruction vs return tracking)
- **Solution**: Used `LevelPhase` enum to track current phase. Each phase activates/deactivates relevant systems. This keeps code organized and prevents conflicts between systems.

- **Problem**: Players should be rewarded for fast building, but cleanup shouldn't be rushed
- **Solution**: Timer runs during building and destruction (skill-based phases), but stops when blocks are returned in final level. This makes cleanup part of the gameplay without time pressure, while still rewarding speed in the main phases.

**Decisions:**
- Timer stops only in final level when blocks returned (cleanup is part of gameplay)
- Used teleportation for phase transitions to allow flexible scene layout
- Made completion threshold configurable (100% default, but can be adjusted per level)

**References**: 
- [Game Flow Documentation](../Assets/BlockBattle/Scripts/GameFlow/README.md)
- [Game Flow Documentation](game_flow_shelf_wall.md)

---

### January 2026 - Multiplayer Exploration
**Status**: Partially Implemented (Abandoned)

**What was done:**
- Explored Unity Netcode for GameObjects integration
- Attempted multiplayer avatar sync
- Tested Distributed Authority mode for block spawning

**Problems & Solutions:**
- **Problem**: Multiplayer synchronization was complex and not core to project scope
- **Solution**: Focused on single-player experience. Multiplayer can be added later if needed.

**Decisions:**
- Prioritized single-player polish over multiplayer features
- Kept codebase clean for potential future multiplayer addition

---

## Key Technical Achievements

1. **Relative Position Validation**: Implemented validation system that works regardless of table rotation, making VR gameplay more flexible
2. **Physics-Based Interactions**: Created shelf door system with motor-driven haptics and realistic block ejection
3. **Pull-Distance Slingshot**: Implemented engaging VR slingshot mechanic with player-controlled power
4. **Phase-Based Game Flow**: Created clean architecture for managing complex game state transitions

## Lessons Learned

1. **Relative vs Absolute Coordinates**: Using relative positioning in VR is crucial for flexible gameplay
2. **Physics-Based Interactions**: Physics-driven interactions (doors, slingshot) feel more natural in VR than button presses
3. **Event-Driven Architecture**: Using C# events for system communication keeps code loosely coupled and maintainable
4. **ScriptableObject Configuration**: Using ScriptableObjects for level data makes iteration fast and designer-friendly

## Known Issues & Future Work

- Tolerance values may need per-level tuning
- Validation runs on main thread (could be optimized with Unity Jobs)
- Multiplayer support was explored but not completed
- Some hardcoded values (door angles, teleport positions) could be made configurable
