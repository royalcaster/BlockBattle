# Game Flow: Shelf to Wall System

## Overview

This document describes the complete game flow from blocks being stored in the shelf, ejected when doors open, caught by the player, and placed into matching wall holes.

---

## Game Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           BLOCKBATTLE GAME FLOW                             │
│                         (Shelf → Wall Mechanic)                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────┐                                                        │
│  │   GAME START    │                                                        │
│  │                 │                                                        │
│  │ • Blocks loaded │                                                        │
│  │   in shelf      │                                                        │
│  │ • Doors closed  │                                                        │
│  │ • Score = 0     │                                                        │
│  └────────┬────────┘                                                        │
│           │                                                                 │
│           ▼                                                                 │
│  ┌─────────────────┐                                                        │
│  │  SHELF READY    │◄──────────────────────────────────┐                    │
│  │                 │                                   │                    │
│  │ • Blocks inside │                                   │                    │
│  │ • Awaiting door │                                   │                    │
│  │   opening       │                                   │                    │
│  └────────┬────────┘                                   │                    │
│           │                                            │                    │
│           │ Player opens door (angle >= 70°)           │                    │
│           ▼                                            │                    │
│  ┌─────────────────┐                                   │                    │
│  │  BLOCKS EJECT   │                                   │                    │
│  │                 │                                   │                    │
│  │ • Random spread │                                   │                    │
│  │ • Random force  │                                   │                    │
│  │ • Tumble spin   │                                   │                    │
│  │ • Door kick     │                                   │                    │
│  └────────┬────────┘                                   │                    │
│           │                                            │                    │
│           │ Blocks fly through air                     │                    │
│           ▼                                            │                    │
│  ┌─────────────────┐                                   │                    │
│  │  PLAYER ACTION  │                                   │                    │
│  │                 │                                   │                    │
│  │ • Catch blocks  │                                   │                    │
│  │ • Grab with VR  │                                   │                    │
│  │   controllers   │                                   │                    │
│  └────────┬────────┘                                   │                    │
│           │                                            │                    │
│           │ Player moves block to wall                 │                    │
│           ▼                                            │                    │
│  ┌─────────────────┐                                   │                    │
│  │  WALL HOLES     │                                   │                    │
│  │                 │                                   │                    │
│  │ • Shape check   │                                   │                    │
│  │ • Rotation chk  │                                   │                    │
│  │ • Depth check   │                                   │                    │
│  └────────┬────────┘                                   │                    │
│           │                                            │                    │
│     ┌─────┴─────┐                                      │                    │
│     │           │                                      │                    │
│     ▼           ▼                                      │                    │
│  Wrong       Correct                                   │                    │
│  Shape       Shape                                     │                    │
│     │           │                                      │                    │
│     ▼           ▼                                      │                    │
│  ┌─────────┐  ┌─────────────────┐                      │                    │
│  │ BLOCKED │  │  AUTO-SNAP      │                      │                    │
│  │         │  │                 │                      │                    │
│  │ Try     │  │ • Haptic buzz   │                      │                    │
│  │ again   │  │ • Force release │                      │                    │
│  └─────────┘  │ • Smooth move   │                      │                    │
│               │ • Score +1      │                      │                    │
│               └────────┬────────┘                      │                    │
│                        │                               │                    │
│                        ▼                               │                    │
│               ┌─────────────────┐                      │                    │
│               │  SCORE CHECK    │                      │                    │
│               │                 │                      │                    │
│               │ Score < Target? │                      │                    │
│               └────────┬────────┘                      │                    │
│                        │                               │                    │
│                  ┌─────┴─────┐                         │                    │
│                  │           │                         │                    │
│                  ▼           ▼                         │                    │
│               Yes           No                         │                    │
│                  │           │                         │                    │
│                  │           ▼                         │                    │
│                  │  ┌─────────────────┐                │                    │
│                  │  │    VICTORY!     │                │                    │
│                  │  │                 │                │                    │
│                  │  │ • Show message  │                │                    │
│                  │  │ • Celebration   │                │                    │
│                  │  └─────────────────┘                │                    │
│                  │                                     │                    │
│                  │  Doors close (angle < 60°)          │                    │
│                  └─────────────────────────────────────┘                    │
│                        (Shelf reloads)                                      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Phase Details

### Phase 1: Game Start

**Initial State:**
- Blocks are pre-loaded inside the shelf trigger volume
- Shelf doors are closed (angle ≈ 0°)
- Wall holes are blocked (solid colliders, hidden visuals)
- Score display shows "0 von X"

**Setup Requirements:**
- Shelf with `ShelfLogic_TwoDoors` component
- Blocks with `Rigidbody` components inside shelf
- Wall with `ShapeHoleGuide` components on each hole
- `GameManager` with UI references

### Phase 2: Shelf Ready

**State:**
- `hasTriggered = false`
- Blocks tracked in `storedBlocks` list
- Monitoring door angles every frame

**Player Action:**
- Player approaches shelf
- Player grabs door handle (or pushes door)
- Door swings open via physics

### Phase 3: Block Ejection

**Trigger Condition:**
```csharp
if (leftDoor.angle >= triggerAngle || rightDoor.angle >= triggerAngle)
```

**What Happens:**
1. All stored blocks receive:
   - Randomized velocity (base direction + spread)
   - Randomized force (80%-120% of ejectionForce)
   - Tumble torque (random rotation)
2. Doors receive kick impulse
3. `hasTriggered = true` (prevents re-firing)

**Physics Parameters:**
| Parameter | Default | Effect |
|-----------|---------|--------|
| ejectionForce | 15 | Base speed of blocks |
| spreadAmount | 0.3 | Cone of spread |
| tumbleForce | 10 | Spin intensity |
| doorKickForce | 30 | Door push strength |

### Phase 4: Player Catches Blocks

**Player Actions:**
- Track flying blocks visually
- Position VR controllers to intercept
- Grab blocks using grip button
- Blocks have `XRGrabInteractable` component

**Physics Considerations:**
- Blocks continue physics simulation until grabbed
- Grabbing makes block follow controller
- Player can hold multiple blocks (one per hand)

### Phase 5: Wall Hole Validation

**When Block Enters Hole Trigger:**

```csharp
// Check 1: Shape tag
if (other.CompareTag(_requiredTag))  // e.g., "Shape_Cube"

// Check 2: Rotation alignment
float angle = Quaternion.Angle(block.rotation, hole.rotation);
if (angle < _rotationTolerance)  // e.g., < 15°

// Check 3: Depth penetration
Vector3 localPos = hole.InverseTransformPoint(block.position);
if (localPos.z > _triggerDepth)  // e.g., > -0.1
```

**Outcomes:**
- ❌ Wrong shape → Block bounces off solid blocker
- ❌ Wrong rotation → Block can enter but won't snap
- ❌ Not deep enough → Block can enter but won't snap
- ✅ All correct → Auto-snap triggered

### Phase 6: Auto-Snap

**Sequence:**
1. **Haptic Feedback:** Controller vibrates (0.5 intensity, 0.15s)
2. **Force Release:** `XRGrabInteractable.enabled = false`
3. **Kinematic:** `Rigidbody.isKinematic = true`
4. **Score:** `GameManager.AddScore()`
5. **Movement:** Smooth lerp to `_finalSnapAnchor`
6. **Blocker:** Re-enable solid, show visual

**Movement Code:**
```csharp
while (Vector3.Distance(block.position, anchor.position) > 0.01f)
{
    block.position = Vector3.MoveTowards(block.position, anchor.position, speed * Time.deltaTime);
    block.rotation = Quaternion.Slerp(block.rotation, anchor.rotation, alignSpeed * Time.deltaTime);
    yield return null;
}
```

### Phase 7: Score Check

**After Each Snap:**
```csharp
_currentScore++;
UpdateUI();  // "1 von 2", "2 von 2", etc.

if (_currentScore >= _targetScore)
{
    StartCoroutine(ShowVictoryRoutine());
}
```

### Phase 8: Victory or Continue

**If Score < Target:**
- Continue gameplay
- Player catches/places remaining blocks
- Shelf can reload if doors close

**If Score >= Target:**
- Victory message displayed
- UI color changes to yellow
- UI scales up 1.2x
- Game complete!

### Phase 9: Shelf Reload (Optional)

**Reload Condition:**
```csharp
if (leftDoor.angle < resetAngle && rightDoor.angle < resetAngle)
{
    hasTriggered = false;  // Ready to fire again
}
```

**Use Cases:**
- Player missed some blocks
- Timed rounds with multiple ejections
- Practice mode

---

## Component Communication

```
┌─────────────────────────────────────────────────────────────┐
│                    COMPONENT DIAGRAM                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐         ┌─────────────────┐            │
│  │ ShelfLogic_     │         │  Block          │            │
│  │ TwoDoors        │◄───────►│  (Rigidbody)    │            │
│  │                 │ stores  │                 │            │
│  │ • storedBlocks  │         │ • XRGrabInter.  │            │
│  │ • leftDoor      │         │ • Collider      │            │
│  │ • rightDoor     │         │ • Tag           │            │
│  └─────────────────┘         └────────┬────────┘            │
│                                       │                     │
│                                       │ enters              │
│                                       ▼                     │
│  ┌─────────────────┐         ┌─────────────────┐            │
│  │ GameManager     │◄────────│ ShapeHoleGuide  │            │
│  │                 │ AddScore│                 │            │
│  │ • _currentScore │         │ • _requiredTag  │            │
│  │ • _targetScore  │         │ • _holeBlocker  │            │
│  │ • _displayLabel │         │ • _finalSnap    │            │
│  └─────────────────┘         └─────────────────┘            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Timing Diagram

```
Time ──────────────────────────────────────────────────────────►

Door      ░░░░░░░▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░░░░░░░░░░░
Angle     0°    70°                      60°                   0°
          closed │                        │                    closed
                 │                        │
                 ▼                        ▼
Shelf     READY ─┼─ FIRED ───────────────┼─ READY ─────────────
State            │                        │
                 │                        │
Blocks    stored ┼─ flying ─ caught ─ placing ─ snapped ───────
                 │    │         │          │        │
                 │    │         │          │        │
Player           │    │  grab   │  move    │        │
Action    ───────┼────┼─────────┼──────────┼────────┼──────────
                 │    │         │          │        │
                 │    │         │          │        │
Score     0 ─────┼────┼─────────┼──────────┼────────┼─ 1 ──────
                 │    │         │          │        │
                 │    │         │          │        ▼
Haptic    ───────┼────┼─────────┼──────────┼────────█──────────
                                                    │
                                                    0.15s
```

---

## Configuration Recommendations

### Easy Mode

```csharp
// Shelf: Slow, predictable blocks
shelf.ejectionForce = 5f;
shelf.spreadAmount = 0.1f;
shelf.tumbleForce = 2f;

// Holes: Forgiving validation
hole._rotationTolerance = 30f;
hole._triggerDepth = -0.05f;
hole._autoMoveSpeed = 1.5f;

// Game: Few blocks needed
gameManager._targetScore = 2;
```

### Medium Mode

```csharp
// Shelf: Moderate chaos
shelf.ejectionForce = 10f;
shelf.spreadAmount = 0.3f;
shelf.tumbleForce = 8f;

// Holes: Standard validation
hole._rotationTolerance = 15f;
hole._triggerDepth = -0.1f;
hole._autoMoveSpeed = 0.8f;

// Game: More blocks
gameManager._targetScore = 4;
```

### Hard Mode

```csharp
// Shelf: Fast, chaotic blocks
shelf.ejectionForce = 20f;
shelf.spreadAmount = 0.6f;
shelf.tumbleForce = 15f;

// Holes: Precise validation
hole._rotationTolerance = 5f;
hole._triggerDepth = -0.15f;
hole._autoMoveSpeed = 0.3f;

// Game: Many blocks
gameManager._targetScore = 6;
```

---

## Future Enhancements

### Dynamic Hole Generation

**Concept:** Wall holes match the exact blocks that fell from the shelf.

**Implementation Steps:**
1. Shelf tracks block types ejected
2. Shelf sends block list to Wall
3. Wall generates holes at runtime
4. Holes positioned randomly or in pattern

**Code Sketch:**
```csharp
// In ShelfLogic
public event Action<List<BlockType>> OnBlocksEjected;

private void FireEverything()
{
    List<BlockType> ejectedTypes = new List<BlockType>();
    foreach (var block in storedBlocks)
    {
        ejectedTypes.Add(block.GetComponent<BlockIdentifier>().Type);
    }
    OnBlocksEjected?.Invoke(ejectedTypes);
    // ... rest of ejection
}

// In Wall
private void OnEnable()
{
    shelf.OnBlocksEjected += GenerateHoles;
}

private void GenerateHoles(List<BlockType> types)
{
    foreach (var type in types)
    {
        CreateHoleForType(type);
    }
}
```

### Timed Rounds

**Concept:** Multiple ejection rounds with time pressure.

**Features:**
- Timer counting down
- Multiple shelf reloads
- Increasing difficulty per round
- Bonus points for speed

### Multiplayer Competition

**Concept:** Two players race to fill their walls.

**Features:**
- Separate shelves per player
- Separate walls per player
- First to target score wins
- Sabotage mechanics (optional)

---

## Related Documentation

- [Shelf System](shelf_system.md) - Detailed shelf mechanics
- [Wall Hole System](wall_hole_system.md) - Detailed hole/snap mechanics
- [Core Implementation](core_implementation.md) - Overall game design
- [Build Validator API](build_validator_api.md) - Block validation system
