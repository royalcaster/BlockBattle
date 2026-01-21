# Wall Hole System Documentation

## Overview

The Wall Hole System is a gameplay mechanic where players must place blocks into matching holes in a wall. When a block is correctly positioned (right shape, right rotation, pushed in far enough), the wall "sucks" the block in automatically, snapping it to the final position and awarding points.

This system serves as a **prototype** for what the shelf doors will eventually become - doors with dynamic holes that match the blocks that fell out.

---

## Components

### ShapeHoleGuide

**Location:** `Assets/Scenes/BlockBattleScene/ShapeHoleGuide.cs`

**Namespace:** `Suit.Interactions`

**Purpose:** Validates block shape/rotation and handles the auto-snap "suction" mechanism.

### GameManager

**Location:** `Assets/Scenes/BlockBattleScene/GameManager.cs`

**Namespace:** `Suit.Core`

**Purpose:** Tracks score and displays victory message.

---

## Inspector Properties

### ShapeHoleGuide

#### Validation Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `_requiredTag` | string | "Shape_Cube" | Unity tag that the block must have to be accepted |
| `_rotationTolerance` | float | 15° | Maximum rotation difference allowed for acceptance |

#### Snap Logic Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `_alignmentSpeed` | float | 10 | Speed of rotation alignment during snap (Slerp factor) |
| `_autoMoveSpeed` | float | 0.8 | Speed of position movement during snap (m/s) |
| `_triggerDepth` | float | -0.1 | Local Z depth the block must reach to trigger snap |

#### References

| Property | Type | Description |
|----------|------|-------------|
| `_holeBlocker` | Collider | Collider that blocks the hole until correct shape enters |
| `_finalSnapAnchor` | Transform | Target position/rotation for the snapped block |
| `_gameManager` | GameManager | Reference for scoring (optional) |

### GameManager

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `_displayLabel` | TextMeshProUGUI | - | UI text showing current score |
| `_targetScore` | int | 2 | Number of blocks needed to win |
| `_victoryMessage` | string | "GESCHAFFT!" | Message shown on victory |
| `_victoryColor` | Color | Yellow | Color of victory message |

---

## How It Works

### 1. Shape Detection

The hole uses a trigger collider to detect when blocks enter:

```csharp
private void OnTriggerStay(Collider other)
{
    if (_isProcessing) return;
    
    // Check if block has the required tag
    if (other.CompareTag(_requiredTag))
    {
        // Make hole passable for correct shape
        if (_holeBlocker != null) _holeBlocker.isTrigger = true;
        
        HandleGuiding(other.gameObject);
    }
}

private void OnTriggerExit(Collider other)
{
    if (other.CompareTag(_requiredTag) && !_isProcessing)
    {
        // Block hole again when shape leaves
        if (_holeBlocker != null) _holeBlocker.isTrigger = false;
    }
}
```

### 2. Validation & Trigger Conditions

Three conditions must be met for the snap to trigger:

```csharp
private void HandleGuiding(GameObject shape)
{
    // 1. Check rotation alignment
    float angle = Quaternion.Angle(shape.transform.rotation, transform.rotation);
    
    // 2. Check depth penetration (local Z position)
    Vector3 localPos = transform.InverseTransformPoint(shape.transform.position);

    // 3. Trigger snap if rotation OK and pushed in far enough
    if (angle < _rotationTolerance && localPos.z > _triggerDepth)
    {
        StartCoroutine(AutoMoveAndRelease(shape));
    }
}
```

**Trigger Conditions:**
1. ✅ Block tag matches `_requiredTag`
2. ✅ Block rotation within `_rotationTolerance` degrees
3. ✅ Block local Z position > `_triggerDepth` (pushed in far enough)

### 3. Auto-Snap Coroutine

When all conditions are met, the block is automatically snapped:

```csharp
private IEnumerator AutoMoveAndRelease(GameObject shape)
{
    _isProcessing = true;

    // 1. Send haptic feedback to VR controller
    if (shape.TryGetComponent<XRGrabInteractable>(out var interactable))
    {
        var interactor = interactable.firstInteractorSelecting;
        if (interactor is XRBaseInputInteractor input)
            input.SendHapticImpulse(0.5f, 0.15f);
        
        // 2. Disable grab - forces player to release
        interactable.enabled = false;
    }

    // 3. Make block kinematic (no physics)
    Rigidbody rb = shape.GetComponent<Rigidbody>();
    if (rb != null) rb.isKinematic = true;

    // 4. Award score
    if (_gameManager != null) _gameManager.AddScore();

    // 5. Smoothly move to final position
    while (Vector3.Distance(shape.transform.position, _finalSnapAnchor.position) > 0.01f)
    {
        shape.transform.position = Vector3.MoveTowards(
            shape.transform.position, 
            _finalSnapAnchor.position, 
            _autoMoveSpeed * Time.deltaTime
        );
        
        shape.transform.rotation = Quaternion.Slerp(
            shape.transform.rotation, 
            _finalSnapAnchor.rotation, 
            _alignmentSpeed * Time.deltaTime
        );
        
        yield return null;
    }

    // 6. Re-enable hole blocker (now visible)
    if (_holeBlocker != null)
    {
        _holeBlocker.isTrigger = false;
        if (_blockerRenderer != null) _blockerRenderer.enabled = true;
    }
}
```

---

## State Machine

```
┌─────────────────────────────────────────────────────────────┐
│                    HOLE STATES                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐                                        │
│  │     BLOCKED     │  Blocker: Solid, Hidden                │
│  │   (Initial)     │  Waiting for correct shape             │
│  └────────┬────────┘                                        │
│           │                                                 │
│           │ Correct shape enters trigger                    │
│           ▼                                                 │
│  ┌─────────────────┐                                        │
│  │    PASSABLE     │  Blocker: Trigger (passable)           │
│  │   (Validating)  │  Checking rotation & depth             │
│  └────────┬────────┘                                        │
│           │                                                 │
│     ┌─────┴─────┐                                           │
│     │           │                                           │
│     ▼           ▼                                           │
│  Shape exits  Conditions met                                │
│  (wrong rot)  (rot OK, depth OK)                            │
│     │           │                                           │
│     ▼           ▼                                           │
│  ┌─────────┐  ┌─────────────────┐                           │
│  │ BLOCKED │  │   PROCESSING    │  Haptic, disable grab    │
│  │ (Reset) │  │   (Snapping)    │  Move to anchor          │
│  └─────────┘  └────────┬────────┘                           │
│                        │                                    │
│                        │ Snap complete                      │
│                        ▼                                    │
│               ┌─────────────────┐                           │
│               │    FILLED       │  Blocker: Solid, Visible  │
│               │   (Complete)    │  Score awarded            │
│               └─────────────────┘                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Wall Prefab Structure

### Hierarchy

```
Wall (Root)
├── WallMesh (Visual mesh + main collider)
├── Trigger_Hole_Cube_01
│   ├── BoxCollider (trigger, 0.24 x 0.24 x 0.1)
│   ├── ShapeHoleGuide component
│   ├── FinalSnapAnchor (empty Transform)
│   └── BlockCube (blocker mesh, 0.25 x 0.25 x 0.1)
│       ├── MeshFilter (Cube)
│       ├── MeshRenderer (initially disabled)
│       └── BoxCollider (solid blocker)
├── Trigger_Hole_Cube_02
│   └── (same structure)
└── (additional holes...)
```

### Trigger Collider Setup

| Property | Value |
|----------|-------|
| Is Trigger | ✅ Yes |
| Size | 0.24 x 0.24 x 0.1 (slightly smaller than visual hole) |
| Center | (0, 0, 0) |

### Blocker Collider Setup

| Property | Value |
|----------|-------|
| Is Trigger | ❌ No (initially solid) |
| Size | Matches visual hole size |
| MeshRenderer | Initially disabled (hidden) |

### Final Snap Anchor

- Empty Transform positioned at the final resting position
- Rotation matches desired block orientation
- Usually slightly behind the wall surface

---

## Unity Tag Setup

Blocks must have matching tags for validation:

| Shape | Required Tag |
|-------|--------------|
| Cube | `Shape_Cube` |
| Cylinder | `Shape_Cylinder` |
| Triangle | `Shape_Triangle` |
| Rectangle | `Shape_Rectangle` |
| Arch | `Shape_Arch` |

**To add tags in Unity:**
1. Select any GameObject
2. In Inspector, click Tag dropdown → "Add Tag..."
3. Add each shape tag

---

## Usage Examples

### Basic Hole Setup

```csharp
// Create a hole for cubes
ShapeHoleGuide cubeHole = holeObject.AddComponent<ShapeHoleGuide>();
cubeHole._requiredTag = "Shape_Cube";
cubeHole._rotationTolerance = 15f;
cubeHole._triggerDepth = -0.1f;
cubeHole._autoMoveSpeed = 0.8f;
cubeHole._finalSnapAnchor = snapAnchorTransform;
cubeHole._holeBlocker = blockerCollider;
```

### Difficulty Variations

```csharp
// Easy: Forgiving rotation, quick snap
hole._rotationTolerance = 30f;
hole._triggerDepth = -0.05f;  // Triggers early
hole._autoMoveSpeed = 1.5f;   // Fast snap

// Hard: Precise rotation required
hole._rotationTolerance = 5f;
hole._triggerDepth = -0.15f;  // Must push further
hole._autoMoveSpeed = 0.3f;   // Slow, deliberate snap
```

### Multiple Shape Types

```csharp
// Hole 1: Accepts cubes
hole1._requiredTag = "Shape_Cube";

// Hole 2: Accepts cylinders
hole2._requiredTag = "Shape_Cylinder";

// Hole 3: Accepts triangles
hole3._requiredTag = "Shape_Triangle";
```

---

## Integration with Shelf System

The Wall Hole System works in conjunction with the Shelf System:

```
┌─────────┐     Blocks      ┌─────────┐     Player      ┌─────────┐
│  SHELF  │ ──────────────► │  AIR    │ ──────────────► │  WALL   │
│         │    ejected      │         │   catches &     │  HOLES  │
│ 2 Doors │                 │         │    places       │         │
└─────────┘                 └─────────┘                 └─────────┘
                                                              │
                                                              ▼
                                                        ┌─────────┐
                                                        │  SCORE  │
                                                        │  +1     │
                                                        └─────────┘
```

See: [Shelf System Documentation](shelf_system.md)

---

## GameManager Integration

### Score Tracking

```csharp
public class GameManager : MonoBehaviour
{
    private int _currentScore = 0;
    [SerializeField] private int _targetScore = 2;

    public void AddScore()
    {
        _currentScore++;
        UpdateUI();
        
        if (_currentScore >= _targetScore)
        {
            StartCoroutine(ShowVictoryRoutine());
        }
    }
}
```

### UI Display

The GameManager updates a TextMeshProUGUI element:
- During game: "X von Y" (X of Y)
- On victory: Custom message with color change and scale animation

---

## Future Enhancements (Planned)

### Dynamic Hole Generation

Currently holes are static in the prefab. Planned features:

1. **Shelf Communication:** Shelf tells wall which block types were ejected
2. **Runtime Hole Creation:** Wall generates holes matching ejected blocks
3. **Procedural Mesh:** Cut holes in wall mesh at runtime
4. **Random Positioning:** Holes appear at random positions

### Multiple Orientations

- Support for blocks that can be inserted in multiple valid orientations
- Configurable per-hole rotation rules

### Visual Feedback

- Glow effect when correct shape approaches
- Color change based on rotation alignment
- Particle effects on successful snap

### Audio Feedback

- Sound when block enters trigger zone
- Different sounds for correct/incorrect shape
- Satisfying "click" on successful snap

---

## Troubleshooting

### Block Doesn't Snap

1. **Check Tag:** Ensure block has correct Unity tag (e.g., "Shape_Cube")
2. **Check Rotation:** Block may be rotated too far from target
3. **Check Depth:** Block may not be pushed in far enough
4. **Check Processing:** System may already be processing another block

### Block Passes Through Without Snapping

1. Increase `_rotationTolerance` for more forgiving rotation check
2. Decrease `_triggerDepth` (less negative) to trigger earlier
3. Verify `_finalSnapAnchor` is assigned

### Blocker Doesn't Appear After Snap

1. Check `_holeBlocker` reference is assigned
2. Verify blocker has MeshRenderer component
3. Check that blocker material is visible

### Haptic Feedback Not Working

1. Ensure block has `XRGrabInteractable` component
2. Check that interactor is `XRBaseInputInteractor`
3. Verify VR controller supports haptics

### Score Not Updating

1. Check `_gameManager` reference is assigned
2. Verify GameManager has `_displayLabel` assigned
3. Check console for errors in `AddScore()`

---

## Code Reference

### ShapeHoleGuide

**Full Source:** `Assets/Scenes/BlockBattleScene/ShapeHoleGuide.cs`

```csharp
namespace Suit.Interactions
{
    public class ShapeHoleGuide : MonoBehaviour
    {
        [Header("Validation")]
        [SerializeField] private string _requiredTag = "Shape_Cube";
        [SerializeField] private float _rotationTolerance = 15f;
        
        [Header("Snap Logic")]
        [SerializeField] private float _alignmentSpeed = 10f; 
        [SerializeField] private float _autoMoveSpeed = 0.8f; 
        [SerializeField] private float _triggerDepth = -0.1f; 

        [Header("Blocker")]
        [SerializeField] private Collider _holeBlocker;

        [Header("References")]
        [SerializeField] private Transform _finalSnapAnchor; 
        [SerializeField] private GameManager _gameManager;

        private MeshRenderer _blockerRenderer;
        private bool _isProcessing = false;

        // ... implementation
    }
}
```

### GameManager

**Full Source:** `Assets/Scenes/BlockBattleScene/GameManager.cs`

```csharp
namespace Suit.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI _displayLabel; 
        
        [Header("Settings")]
        [SerializeField] private int _targetScore = 2;
        [SerializeField] private string _victoryMessage = "GESCHAFFT!";
        [SerializeField] private Color _victoryColor = Color.yellow;

        private int _currentScore = 0;

        public void AddScore() { /* ... */ }
        private void UpdateUI() { /* ... */ }
        private IEnumerator ShowVictoryRoutine() { /* ... */ }
    }
}
```
