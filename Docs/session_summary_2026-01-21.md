# Session Summary - January 21, 2026

## Overview

Major implementation of the unified **ShelfBlockSpawner** system that merges block spawning with the shelf's door-triggered ejection mechanics. Also added comprehensive documentation for the shelf and wall hole systems.

---

## Completed Features

### 1. ShelfBlockSpawner Component

**New File:** `Assets/BlockBattle/Scripts/ShelfBlockSpawner.cs`

A unified component that combines:
- Block spawning logic (from `BlockSpawner`)
- Door-triggered ejection (from `ShelfLogic_TwoDoors`)

**Key Features:**
- Spawns blocks inside shelf based on `BlockSpawnConfiguration`
- Blocks spawn as kinematic (no physics) until doors open
- Monitors door angles via `HingeJoint`
- Ejects blocks with randomized physics when door angle exceeds threshold
- Rotation-independent door kick (works regardless of shelf world rotation)
- Events: `OnBlocksSpawned`, `OnBlocksEjected`, `OnShelfReset`

**Public API:**
```csharp
void SpawnBlocks()           // Spawn blocks inside shelf
void ClearSpawnedBlocks()    // Remove all spawned blocks
void ForceEject()            // Manual ejection trigger
void ResetTriggerState()     // Reset for next round
int StoredBlockCount { get; }
bool HasTriggered { get; }
```

### 2. Editor Setup Tool

**New File:** `Assets/Editor/BlockBattleShelfBlockSpawnerSetup.cs`

**Menu:** `BlockBattle → Setup Shelf Block Spawner`

Automates the setup process:
- Finds `Regal` GameObject (shelf trigger)
- Adds `ShelfBlockSpawner` component
- Auto-assigns door references (`Tür_links`, `Tür_Rechts`)
- Auto-assigns ejection direction (`Schuss_Richtung`)
- Creates `SpawnAnchor` if missing
- Assigns all 6 block prefabs
- Copies settings from old `ShelfLogic_TwoDoors`
- Updates `LevelManager` reference automatically

### 3. LevelManager Integration

**Modified:** `Assets/BlockBattle/Scripts/LevelManager.cs`

- Changed `m_BlockSpawner` (BlockSpawner) → `m_ShelfSpawner` (ShelfBlockSpawner)
- `StartLevel()` now calls `m_ShelfSpawner.SpawnBlocks()`
- `ClearPlacedBlocks()` now also calls `m_ShelfSpawner.ClearSpawnedBlocks()`
- Handles both old (`_Spawned`) and new (`_Shelf`) block naming conventions

### 4. Documentation

**New Files:**
- `Docs/shelf_system.md` - Complete shelf system documentation
- `Docs/wall_hole_system.md` - Wall hole "suction" mechanic documentation  
- `Docs/game_flow_shelf_wall.md` - End-to-end game flow documentation

**Contents:**
- Full API references
- Inspector property tables
- Scene hierarchy diagrams
- Code examples
- Migration guides
- Troubleshooting sections

---

## Bug Fixes

### 1. Blocks Not Ejecting (Kinematic Issue)

**Problem:** Blocks spawned as kinematic weren't being added to `_storedBlocks` because `OnTriggerEnter` doesn't fire for kinematic objects.

**Solution:** Manually register blocks in `_storedBlocks` when spawning:
```csharp
if (!_storedBlocks.Contains(rb))
{
    _storedBlocks.Add(rb);
}
```

### 2. Rotation-Dependent Door Kick

**Problem:** Door kick force used `AddRelativeTorque(Vector3.up)` which didn't work correctly when shelf was rotated in world space.

**Solution:** Use the HingeJoint's axis transformed to world space:
```csharp
Vector3 hingeAxisWorld = door.transform.TransformDirection(door.axis);
rb.AddTorque(hingeAxisWorld * m_DoorKickForce * direction, ForceMode.Impulse);
```

---

## Scene Object References

| Object Path | Purpose |
|-------------|---------|
| `Shelf/Regal` | Trigger collider + ShelfBlockSpawner |
| `Shelf/Regal/Tür_links` | Left door (HingeJoint) |
| `Shelf/Regal/Tür_Rechts` | Right door (HingeJoint) |
| `Shelf/Regal/Schuss_Richtung` | Ejection direction transform |
| `Shelf/Regal/SpawnAnchor` | Block spawn position (created by setup tool) |
| `LevelManager` | References ShelfBlockSpawner |

---

## Files Changed

### New Files
- `Assets/BlockBattle/Scripts/ShelfBlockSpawner.cs`
- `Assets/BlockBattle/Scripts/ShelfBlockSpawner.cs.meta`
- `Assets/Editor/BlockBattleShelfBlockSpawnerSetup.cs`
- `Assets/Editor/BlockBattleShelfBlockSpawnerSetup.cs.meta`
- `Docs/shelf_system.md`
- `Docs/wall_hole_system.md`
- `Docs/game_flow_shelf_wall.md`
- `Docs/session_summary_2026-01-21.md`

### Modified Files
- `Assets/BlockBattle/Scripts/LevelManager.cs` - Use ShelfBlockSpawner instead of BlockSpawner

---

## How to Use

### Quick Setup
1. Menu: `BlockBattle → Setup Shelf Block Spawner`
2. Click "Setup ShelfBlockSpawner"
3. Assign a `BlockSpawnConfiguration` (or use LevelManager's level list)
4. Disable old `ShelfLogic_TwoDoors` component

### Game Flow
1. `LevelManager.StartLevel()` calls `ShelfBlockSpawner.SpawnBlocks()`
2. Blocks appear inside shelf (kinematic, no physics)
3. Player opens door past 70° threshold
4. Blocks are ejected with randomized physics
5. Player catches blocks and builds structure

---

---

## Part 2: Shelf Return Game Loop (Session 2)

### Overview

Implemented a new game loop where players must return blocks to the shelf and close the doors after building a structure, before the level is considered complete.

### New Game Flow

**Old Flow:**
1. Level starts → Blocks spawn → Doors open → Blocks ejected
2. Player builds structure → 100% accuracy detected
3. Success message → Blocks destroyed → Next level starts immediately

**New Flow:**
1. Level starts → Blocks spawn → Doors open → Blocks ejected
2. Player builds structure → 100% accuracy detected → **Building Phase Complete**
3. Player returns ALL blocks to shelf
4. System detects: all blocks inside → **3-second countdown starts**
5. If blocks leave shelf during countdown → Countdown cancelled, return to step 3
6. Countdown completes → Blocks destroyed → Next level starts

### State Machine

Added `LevelPhase` enum to track progression:

```csharp
public enum LevelPhase
{
    Building,           // Player is building the structure
    WaitingForReturn,   // Structure complete, waiting for blocks to return
    Countdown,          // Blocks returned, counting down to next level
    Transitioning       // Loading next level
}
```

### Changes to ShelfBlockSpawner

**New Public API:**

```csharp
// Check if both doors are closed (below reset angle)
bool AreDoorsClosed { get; }

// Check if all blocks have been returned
bool AreAllBlocksReturned(int expectedCount)

// Get list of blocks currently in shelf
IReadOnlyList<Rigidbody> GetBlocksInShelf()
```

### Changes to LevelManager

**Major Refactoring:**
- Replaced boolean flags (`m_IsLevelComplete`, `m_IsTransitioning`) with `LevelPhase` state machine
- Added `m_ExpectedBlockCount` to track how many blocks need to be returned
- Added `m_CountdownDuration` setting (default: 3 seconds)
- Added countdown coroutine with cancellation support

**New Events:**
```csharp
event Action<int> OnBuildingPhaseCompleted;  // Structure built correctly
event Action<int> OnLevelCompleted;          // Full level complete (blocks returned)
event Action<float> OnCountdownStarted;
event Action<float> OnCountdownTick;
event Action OnCountdownCancelled;
```

**New Inspector Settings:**
- `m_CountdownDuration` (float, default 3s) - Time before next level after blocks returned

### Changes to GameplayHUD

**New Methods:**
```csharp
void ShowReturnBlocksMessage(int blockCount)  // "Return X blocks to shelf"
void ShowCountdownMessage(int secondsRemaining)  // "Next level in 3..."
void ShowStatusMessage(string message)  // Generic status display
```

### Edge Cases Handled

1. **Doors open during countdown** → Countdown cancelled, returns to WaitingForReturn
2. **Blocks fall out during countdown** → Countdown cancelled, returns to WaitingForReturn
3. **Player destroys blocks** → Null references cleaned up automatically
4. **Restart/Skip level** → Cancels any active countdown

### Files Changed

**Modified:**
- `Assets/BlockBattle/Scripts/ShelfBlockSpawner.cs` - Added door/block detection API
- `Assets/BlockBattle/Scripts/LevelManager.cs` - Complete rewrite with state machine
- `Assets/BlockBattle/Scripts/GameplayHUD.cs` - Added return phase UI methods
- `Docs/session_summary_2026-01-21.md` - Added Part 2 documentation

---

## Part 3: Block Detection Fixes & Shelf Progress UI (Session 3)

### Overview

Fixed critical issues with block detection when returning blocks to the shelf, and added a world-space progress UI above the shelf.

### Bug Fixes

#### 1. Blocks Not Detected When Returned to Shelf

**Problem:** `OnTriggerEnter`/`OnTriggerStay` were unreliable for detecting blocks placed in the shelf. Blocks could be visually inside but not counted.

**Root Causes:**
- Triggers don't always fire for slowly-moving objects
- Block colliders may be on child objects, but `GetComponent<Rigidbody>()` only checks the same GameObject
- After ejection, `_storedBlocks` wasn't cleared, so blocks were never "re-detected"

**Solution:** Replaced trigger-based detection with `Physics.OverlapBox()` in Update:
```csharp
private void DetectBlocksInShelf()
{
    BoxCollider boxCollider = GetComponent<BoxCollider>();
    Vector3 worldCenter = transform.TransformPoint(boxCollider.center);
    Vector3 halfExtents = Vector3.Scale(boxCollider.size, transform.lossyScale) * 0.5f;
    
    Collider[] colliders = Physics.OverlapBox(worldCenter, halfExtents, transform.rotation);
    // ... process colliders and update _storedBlocks
}
```

This runs every frame and reliably detects all blocks inside the shelf volume.

#### 2. Blocks Falling Through Floor

**Problem:** Ejected blocks sometimes tunneled through the floor due to high velocity.

**Solution:** 
- Added `CollisionDetectionMode.Continuous` to ejected blocks
- Reduced default ejection force: 15 → 8
- Reduced default tumble force: 10 → 5
- Added `m_UseContinuousCollision` toggle in Inspector

#### 3. Stored Blocks Not Cleared After Ejection

**Problem:** `_storedBlocks` list wasn't cleared after ejection, so `AreAllBlocksReturned()` returned true immediately.

**Solution:** Added `_storedBlocks.Clear()` at the end of `EjectBlocks()`.

### New Feature: Shelf Progress UI

**New Files:**
- `Assets/BlockBattle/Scripts/ShelfProgressUI.cs` - World-space UI component
- `Assets/Editor/ShelfProgressUISetup.cs` - Editor tool to create the UI

**Features:**
- Progress bar showing blocks returned (fills as blocks are added)
- Count text: "3/6" format
- Status text: "Return 3 more blocks" or "All blocks returned!"
- Color coding: Gray (empty) → Yellow (partial) → Green (complete)
- Auto-hides during building phase, shows only during WaitingForReturn phase
- World-space canvas positioned above the shelf

**Editor Tool:** Menu → `BlockBattle → Create Shelf Progress UI`

### Changes to ShelfBlockSpawner

**Block Detection:**
- Added `DetectBlocksInShelf()` using `Physics.OverlapBox()`
- Added `GetComponentInParent<Rigidbody>()` fallback for child colliders
- Improved `TryAddBlockToStorage()` with better filtering

**Ejection Physics:**
- Added `m_UseContinuousCollision` setting (default: true)
- Reduced default forces for safer ejection
- Clear `_storedBlocks` after ejection

**New Inspector Settings:**
- `m_UseContinuousCollision` (bool) - Prevents blocks tunneling through floor

### Files Changed

**New Files:**
- `Assets/BlockBattle/Scripts/ShelfProgressUI.cs`
- `Assets/Editor/ShelfProgressUISetup.cs`

**Modified:**
- `Assets/BlockBattle/Scripts/ShelfBlockSpawner.cs` - Physics.OverlapBox detection, ejection fixes
- `Assets/BlockBattle/Scripts/LevelManager.cs` - Simplified (removed door-closing requirement)
- `Docs/session_summary_2026-01-21.md` - Added Part 3

### Game Flow (Final)

1. Level starts → Blocks spawn in shelf → Doors open → Blocks ejected
2. Player builds structure → 100% accuracy → **"Structure Complete!"**
3. Player returns ALL blocks to shelf (progress bar shows count)
4. All blocks detected → 3-second countdown starts
5. If blocks leave shelf → Countdown cancelled, return to step 3
6. Countdown completes → Next level starts

---

## Next Steps

- [ ] Test with all level configurations
- [ ] Implement dynamic hole generation on wall based on ejected block types
- [ ] Add VR hand interaction for doors (grab and pull)
- [ ] Consider adding sound effects for ejection
- [ ] Consider adding haptic feedback when countdown starts/cancels

---

## Technical Notes

- Blocks are named `Block_{Type}_{Color}_Shelf` (e.g., `Block_Cube_Yellow_Shelf`)
- Default trigger angle: 70°, reset angle: 60°
- Ejection includes random spread (0.3), tumble force (5), and force variation (80-120%)
- Door kick uses HingeJoint axis for rotation-independent behavior
- Block detection uses `Physics.OverlapBox()` for reliability (not triggers)
- Continuous collision detection prevents blocks from tunneling through floor
