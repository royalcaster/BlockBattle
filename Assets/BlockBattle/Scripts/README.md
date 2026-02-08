# BlockBattle Scripts - Overview

## Purpose

This directory contains all core gameplay scripts for BlockBattle, a VR block-building and destruction game. The scripts are organized into functional modules that handle validation, block spawning, shelf mechanics, destruction phase, game flow, and UI systems.

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    LevelManager                              │
│              (Game Flow Orchestration)                        │
└──────────────┬──────────────────────────────────────────────┘
               │
    ┌──────────┼──────────┬──────────────┬──────────────┐
    │          │          │              │              │
    ▼          ▼          ▼              ▼              ▼
┌────────┐ ┌────────┐ ┌──────────┐ ┌──────────┐ ┌─────────┐
│Shelf   │ │Blocks  │ │Validation│ │Destruction│ │   UI    │
│System  │ │System  │ │  System  │ │  System   │ │ System  │
└────────┘ └────────┘ └──────────┘ └──────────┘ └─────────┘
```

## Module Overview

### 1. Validation System
**Location:** See [Validation README](Validation/README.md)

Core classes:
- `BuildValidator.cs` - Validates player builds against reference structures using relative positioning
- `BuildZone.cs` - Defines the build area where blocks are validated
- `BuildZonePlacementGuides.cs` - Visual guides showing where blocks should be placed

**Key Feature:** Relative position matching allows the build table to be rotated without breaking validation logic.

### 2. Shelf System
**Location:** See [Shelf README](Shelf/README.md)

Core classes:
- `ShelfBlockSpawner.cs` - Spawns blocks inside shelf and ejects them when doors open
- `ShelfProgressUI.cs` - UI showing shelf door status and block return progress

**Key Feature:** Physics-based door mechanics with motor-driven auto-open and ejection system.

### 3. Destruction System
**Location:** See [Destruction README](Destruction/README.md)

Core classes:
- `VRSlingshot.cs` - VR slingshot with pull-back-and-release mechanics
- `DestructionPhaseManager.cs` - Manages destruction phase and detects completion
- `BallProjectile.cs` - Projectile physics and collision handling

**Key Feature:** Pull-distance-based force calculation for realistic slingshot physics.

### 4. Game Flow
**Location:** See [GameFlow README](GameFlow/README.md)

Core classes:
- `LevelManager.cs` - Manages level progression, timer, and phase transitions
- `StartScreenUI.cs` - Main menu and game start UI

**Key Feature:** Timer stops only when blocks are physically returned to shelf in final level.

### 5. Block System
**Location:** See [Blocks README](Blocks/README.md)

Core classes:
- `BlockSpawner.cs` - Spawns blocks on table (legacy, use ShelfBlockSpawner for new code)
- `BlockSpawnConfiguration.cs` - ScriptableObject defining structure layouts
- `StructureData.cs` - Data structures for block configurations
- `ReferenceStructureSpawner.cs` - Spawns holographic reference structures
- `BlockReference.cs` - Component attached to block prefabs for identification
- `BlockType.cs` / `BlockColor.cs` - Enums for block identification

**Key Feature:** Configuration-based spawning system using ScriptableObjects for easy level design.

### 6. UI System
**Location:** See [UI README](UI/README.md)

Core classes:
- `ValidationHUD.cs` - Real-time validation feedback HUD
- `GameplayHUD.cs` - Main gameplay HUD with block indicators and progress bar
- `ShelfProgressUI.cs` - Shelf door status and block return progress

**Key Feature:** Live-updating HUDs that follow camera and provide real-time feedback.

## How to Use

1. **Scene Setup:** Attach `LevelManager` to a GameObject in your scene
2. **Configure Levels:** Create `BlockSpawnConfiguration` assets for each level
3. **Assign References:** Link all required components in LevelManager inspector
4. **Test:** Use Unity Play mode to test gameplay flow

## Testing

- **Validation:** Place blocks in build zone and check ValidationHUD for accuracy
- **Shelf:** Open shelf doors past 70° to trigger block ejection
- **Destruction:** Use slingshot to destroy built structures
- **Level Progression:** Complete levels to test automatic progression

## Key Technical Decisions

1. **Relative Positioning:** Validation uses relative positions to allow table rotation
2. **Physics-Based Interactions:** Doors use HingeJoints with motors for haptic feedback
3. **Event-Driven Architecture:** Systems communicate via C# events for loose coupling
4. **ScriptableObject Configuration:** Levels defined as assets for easy iteration

## Known Limitations

- Tolerance values may need per-level tuning
- Shelf door angles are hardcoded (70° trigger, 60° reset)
- Validation runs on main thread (could be optimized with jobs)
- No multiplayer synchronization (single-player only currently)

## Related Documentation

- [Validation System](Validation/README.md)
- [Shelf System](Shelf/README.md)
- [Destruction System](Destruction/README.md)
- [Game Flow](GameFlow/README.md)
- [Block System](Blocks/README.md)
- [UI System](UI/README.md)
- [Main Project Documentation](../../../Docs/)
