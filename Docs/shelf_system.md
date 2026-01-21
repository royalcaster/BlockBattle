# Shelf System Documentation

## Overview

The Shelf System is a core gameplay mechanic in BlockBattle that spawns blocks, stores them inside the shelf, and ejects them when the doors are opened. The shelf has two hinged doors that players can interact with, and when opened past a certain angle, all stored blocks are ejected with randomized physics for a chaotic, fun experience.

---

## Components

### ShelfBlockSpawner (Recommended)

**Location:** `Assets/BlockBattle/Scripts/ShelfBlockSpawner.cs`

**Namespace:** `BlockBattle`

**Purpose:** Unified component that spawns blocks inside the shelf and ejects them when doors are opened. This is the recommended component for new implementations.

### ShelfLogic_TwoDoors (Legacy)

**Location:** `Assets/Scenes/BlockBattleScene/ShelfLogic_AngleTrigger.cs`

**Namespace:** `Scenes.Sandbox`

**Purpose:** Original prototype for door-triggered ejection (ejection only, no spawning).

---

## ShelfBlockSpawner API

The `ShelfBlockSpawner` component combines block spawning with door-triggered ejection in a single unified component.

### Inspector Properties

#### Block Spawning Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `m_SpawnConfiguration` | BlockSpawnConfiguration | - | Configuration defining which blocks to spawn |

#### Block Prefabs

| Property | Type | Description |
|----------|------|-------------|
| `m_CubeBlockPrefab` | GameObject | Cube block prefab |
| `m_CylinderBlockPrefab` | GameObject | Cylinder block prefab |
| `m_TriangleBlockPrefab` | GameObject | Triangle block prefab |
| `m_RectangleBlockPrefab` | GameObject | Rectangle block prefab |
| `m_ArchBlockPrefab` | GameObject | Arch block prefab |
| `m_BigTriangleBlockPrefab` | GameObject | Big Triangle block prefab |

#### Spawn Position Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `m_SpawnAnchor` | Transform | this | Anchor point for spawning (center of shelf interior) |
| `m_SpawnSpacing` | float | 0.12 | Spacing between blocks when spawning (meters) |
| `m_SpawnDirection` | Vector3 | (1,0,0) | Direction to arrange blocks when spawning |
| `m_RandomizeSpawnOrder` | bool | true | Whether to randomize the order of spawned blocks |

#### Door Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `m_LeftDoor` | HingeJoint | - | Left door HingeJoint reference |
| `m_RightDoor` | HingeJoint | - | Right door HingeJoint reference |
| `m_TriggerAngle` | float | 70° | Door angle at which blocks are ejected (0-120°) |
| `m_ResetAngle` | float | 60° | Door angle below which the system resets (0-120°) |
| `m_DoorKickForce` | float | 30 | Impulse force applied to doors when blocks eject |

#### Ejection Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `m_EjectionDirection` | Transform | this | Transform whose forward defines ejection direction |
| `m_EjectionForce` | float | 15 | Base force applied to eject blocks |
| `m_SpreadAmount` | float | 0.3 | How much blocks spread (0 = straight, 1 = wide) |
| `m_TumbleForce` | float | 10 | Rotational force for realistic tumbling |

### Public Methods

```csharp
/// <summary>
/// Spawns blocks inside the shelf based on the current spawn configuration.
/// Blocks are spawned as kinematic (no physics) until ejected.
/// </summary>
public void SpawnBlocks()

/// <summary>
/// Clears all spawned blocks from the shelf.
/// </summary>
public void ClearSpawnedBlocks()

/// <summary>
/// Manually triggers block ejection (regardless of door angle).
/// </summary>
public void ForceEject()

/// <summary>
/// Resets the trigger state, allowing the shelf to fire again.
/// </summary>
public void ResetTriggerState()
```

### Public Properties

```csharp
/// <summary>
/// Gets or sets the spawn configuration used for spawning blocks.
/// </summary>
public BlockSpawnConfiguration SpawnConfiguration { get; set; }

/// <summary>
/// Gets the number of blocks currently stored in the shelf.
/// </summary>
public int StoredBlockCount { get; }

/// <summary>
/// Gets whether the shelf has been triggered and is waiting for reset.
/// </summary>
public bool HasTriggered { get; }
```

### Events

```csharp
/// <summary>
/// Event fired when blocks are spawned inside the shelf.
/// </summary>
public event Action<int> OnBlocksSpawned;

/// <summary>
/// Event fired when blocks are ejected from the shelf.
/// </summary>
public event Action<int> OnBlocksEjected;

/// <summary>
/// Event fired when the shelf is ready to be triggered again.
/// </summary>
public event Action OnShelfReset;
```

---

## How It Works

### 1. Block Spawning

When `SpawnBlocks()` is called (typically by a LevelManager), blocks are instantiated inside the shelf:

```csharp
public void SpawnBlocks()
{
    // Get spawn entries from configuration (optionally shuffled)
    List<BlockSpawnEntry> entries = m_SpawnConfiguration.SpawnEntries.ToList();
    if (m_RandomizeSpawnOrder)
    {
        ShuffleList(entries);
    }

    // Spawn each block at calculated position
    Vector3 basePosition = m_SpawnAnchor.position;
    foreach (BlockSpawnEntry entry in entries)
    {
        Vector3 spawnPosition = basePosition + m_SpawnDirection * (index * m_SpawnSpacing);
        GameObject block = SpawnSingleBlock(entry, spawnPosition);
        
        // Block is kinematic until ejected
        block.GetComponent<Rigidbody>().isKinematic = true;
    }
}
```

### 2. Block Storage

The shelf uses a trigger collider to detect and track blocks inside:

```csharp
private List<Rigidbody> _storedBlocks = new List<Rigidbody>();

private void OnTriggerEnter(Collider other)
{
    Rigidbody rb = other.GetComponent<Rigidbody>();
    if (rb != null && !_storedBlocks.Contains(rb))
    {
        _storedBlocks.Add(rb);
    }
}

private void OnTriggerExit(Collider other)
{
    Rigidbody rb = other.GetComponent<Rigidbody>();
    if (rb != null && _storedBlocks.Contains(rb))
    {
        _storedBlocks.Remove(rb);
    }
}
```

### 3. Door Angle Monitoring

Every frame, the system checks both door angles:

```csharp
private void MonitorDoors()
{
    float angleL = Mathf.Abs(m_LeftDoor.angle);
    float angleR = Mathf.Abs(m_RightDoor.angle);

    // FIRE: When either door opens past trigger angle
    if ((angleL >= m_TriggerAngle || angleR >= m_TriggerAngle) && !_hasTriggered)
    {
        EjectBlocks();
        _hasTriggered = true;
    }

    // RESET: When BOTH doors close below reset angle
    if (angleL < m_ResetAngle && angleR < m_ResetAngle && _hasTriggered)
    {
        _hasTriggered = false;
        OnShelfReset?.Invoke();
    }
}
```

### 4. Block Ejection

When triggered, all stored blocks are ejected with randomized physics:

```csharp
private void EjectBlocks()
{
    foreach (Rigidbody rb in _storedBlocks)
    {
        // 1. Get base direction from ejectionDirection transform
        Vector3 baseDir = m_EjectionDirection.forward;

        // 2. Add random spread
        Vector3 randomDir = (baseDir + new Vector3(
            Random.Range(-m_SpreadAmount, m_SpreadAmount),
            Random.Range(-m_SpreadAmount, m_SpreadAmount) + 0.1f,
            Random.Range(-m_SpreadAmount, m_SpreadAmount)
        )).normalized;

        // 3. Randomize force (80%-120% of base)
        float randomPower = m_EjectionForce * Random.Range(0.8f, 1.2f);

        // 4. Enable physics and apply forces
        rb.WakeUp();
        rb.isKinematic = false;
        rb.linearVelocity = randomDir * randomPower;

        // 5. Add tumble rotation
        rb.AddTorque(Random.insideUnitSphere * m_TumbleForce, ForceMode.Impulse);
    }

    // Kick doors open further
    KickDoor(m_LeftDoor);
    KickDoor(m_RightDoor);
    
    OnBlocksEjected?.Invoke(_storedBlocks.Count);
}
```

### 5. Door Kick

Doors receive an impulse when blocks fire, pushing them further open:

```csharp
private void KickDoor(HingeJoint door)
{
    Rigidbody rb = door.GetComponent<Rigidbody>();
    if (rb != null)
    {
        float direction = Mathf.Sign(door.angle);
        if (direction == 0) direction = 1;
        rb.AddRelativeTorque(Vector3.up * m_DoorKickForce * direction, ForceMode.Impulse);
    }
}
```

---

## State Machine

```
┌─────────────────┐
│     EMPTY       │
│  No blocks      │
│  spawned yet    │
└────────┬────────┘
         │
         │ SpawnBlocks() called
         ▼
┌─────────────────┐
│     READY       │
│  _hasTriggered  │
│    = false      │
│  Blocks inside  │
└────────┬────────┘
         │
         │ Either door angle >= triggerAngle
         ▼
┌─────────────────┐
│    FIRED        │
│  _hasTriggered  │──────────────────┐
│    = true       │                  │
│  Blocks ejected │                  │
└────────┬────────┘                  │
         │                           │
         │ BOTH doors < resetAngle   │ Can call SpawnBlocks()
         ▼                           │ again to reload
┌─────────────────┐                  │
│   RESET         │◄─────────────────┘
│  _hasTriggered  │
│    = false      │
│  OnShelfReset   │
└─────────────────┘
```

---

## Scene Setup

### Required Hierarchy

```
Shelf (Empty GameObject)
├── ShelfBody (Visual mesh + colliders)
├── BlockStorageTrigger (BoxCollider, isTrigger=true)
│   └── ShelfBlockSpawner component
├── SpawnAnchor (Empty Transform - center of shelf interior)
├── EjectionDirection (Empty Transform - forward = ejection direction)
├── LeftDoor (with HingeJoint + Rigidbody)
│   └── DoorMesh
└── RightDoor (with HingeJoint + Rigidbody)
    └── DoorMesh
```

### HingeJoint Configuration

Each door requires a `HingeJoint` component:

| Property | Recommended Value |
|----------|-------------------|
| Connected Body | Shelf body Rigidbody (or null for world) |
| Anchor | Door hinge position (local) |
| Axis | (0, 1, 0) for vertical rotation |
| Use Limits | ✅ Enabled |
| Min Limit | -120° (or 0° for one-way) |
| Max Limit | 120° (or 0° for one-way) |
| Use Spring | Optional - for auto-close behavior |
| Use Motor | Optional - for motorized doors |

### Rigidbody Configuration (Doors)

| Property | Recommended Value |
|----------|-------------------|
| Mass | 1-5 kg |
| Drag | 0.5-2 (for damping) |
| Angular Drag | 1-3 (prevents spinning) |
| Use Gravity | ✅ Enabled |
| Is Kinematic | ❌ Disabled |

---

## Usage Examples

### Basic Integration with LevelManager

```csharp
using BlockBattle;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private ShelfBlockSpawner m_ShelfSpawner;
    [SerializeField] private BlockSpawnConfiguration[] m_LevelConfigurations;
    
    private int m_CurrentLevel = 0;

    void Start()
    {
        // Subscribe to events
        m_ShelfSpawner.OnBlocksSpawned += OnBlocksSpawned;
        m_ShelfSpawner.OnBlocksEjected += OnBlocksEjected;
        m_ShelfSpawner.OnShelfReset += OnShelfReset;
        
        // Load first level
        LoadLevel(0);
    }

    public void LoadLevel(int levelIndex)
    {
        m_CurrentLevel = levelIndex;
        m_ShelfSpawner.SpawnConfiguration = m_LevelConfigurations[levelIndex];
        m_ShelfSpawner.SpawnBlocks();
    }

    private void OnBlocksSpawned(int count)
    {
        Debug.Log($"Spawned {count} blocks in shelf");
    }

    private void OnBlocksEjected(int count)
    {
        Debug.Log($"Ejected {count} blocks!");
        // Start gameplay timer, enable wall holes, etc.
    }

    private void OnShelfReset()
    {
        Debug.Log("Shelf reset - ready for next round");
        // Could auto-spawn next level's blocks here
    }
}
```

### Manual Ejection (Skip Door Interaction)

```csharp
// For testing or alternative game modes
public void StartRound()
{
    m_ShelfSpawner.SpawnBlocks();
    
    // Wait a moment, then force eject
    Invoke(nameof(ForceStart), 2f);
}

private void ForceStart()
{
    m_ShelfSpawner.ForceEject();
}
```

### Difficulty Scaling

```csharp
// Easy: Slow, predictable blocks
m_ShelfSpawner.m_EjectionForce = 5f;
m_ShelfSpawner.m_SpreadAmount = 0.1f;
m_ShelfSpawner.m_TumbleForce = 2f;

// Medium: Moderate chaos
m_ShelfSpawner.m_EjectionForce = 10f;
m_ShelfSpawner.m_SpreadAmount = 0.3f;
m_ShelfSpawner.m_TumbleForce = 8f;

// Hard: Fast, chaotic blocks
m_ShelfSpawner.m_EjectionForce = 20f;
m_ShelfSpawner.m_SpreadAmount = 0.6f;
m_ShelfSpawner.m_TumbleForce = 15f;
```

---

## Integration with Wall System

The shelf system is designed to work with the Wall Hole System:

1. **LevelManager calls SpawnBlocks()** → Blocks appear inside shelf
2. **Player opens door** → Door angle exceeds trigger threshold
3. **Shelf ejects blocks** → Blocks fly toward wall with randomized physics
4. **Player catches/grabs blocks** → Using VR controllers
5. **Player places blocks in wall holes** → Matching shape required
6. **Wall "sucks in" correctly placed blocks** → Auto-snap mechanism

See: [Wall Hole System Documentation](wall_hole_system.md)

---

## Editor Visualization

When the `ShelfBlockSpawner` is selected in the editor, Gizmos show:

- **Green sphere**: Spawn anchor position
- **Cyan line**: Spawn direction (how blocks are arranged)
- **Red line**: Ejection direction
- **Orange cone**: Spread angle visualization

---

## Migration from ShelfLogic_TwoDoors

### Automated Setup (Recommended)

Use the editor tool for automatic setup:

1. Open Unity menu: **BlockBattle → Setup Shelf Block Spawner**
2. Click **"Setup ShelfBlockSpawner"** button
3. The tool will automatically:
   - Find the `Regal` GameObject (shelf trigger)
   - Add `ShelfBlockSpawner` component
   - Assign door references (`Tür_links`, `Tür_Rechts`)
   - Assign ejection direction (`Schuss_Richtung`)
   - Create `SpawnAnchor` if missing
   - Assign all 6 block prefabs
   - Copy settings from old `ShelfLogic_TwoDoors`

### Manual Setup

If you prefer manual setup or have a different scene structure:

#### Scene Hierarchy in BlockBattleScene

```
Shelf                          ← Root container (empty)
└── Regal                      ← Trigger collider + ShelfBlockSpawner goes here
    ├── Rückwand               ← Back wall mesh
    ├── Boden                  ← Floor mesh
    ├── Dach                   ← Roof mesh
    ├── Seitenwand_Links       ← Left wall mesh
    ├── Seitenwand_Rechts      ← Right wall mesh
    ├── Türklinge              ← Door handle
    ├── Tür_links              ← Left door (HingeJoint)
    ├── Tür_Rechts             ← Right door (HingeJoint)
    ├── Schuss_Richtung        ← Ejection direction (Transform)
    └── SpawnAnchor            ← Spawn position (create if missing)
```

#### Step-by-Step Manual Instructions

1. **Select the trigger GameObject:**
   - In Hierarchy: `Shelf → Regal`
   - This has the BoxCollider (trigger) and old `ShelfLogic_TwoDoors`

2. **Add ShelfBlockSpawner component:**
   - Inspector → Add Component → Search "ShelfBlockSpawner"

3. **Assign Door References:**
   - `m_LeftDoor`: Drag `Shelf/Regal/Tür_links` (the HingeJoint component)
   - `m_RightDoor`: Drag `Shelf/Regal/Tür_Rechts` (the HingeJoint component)

4. **Assign Ejection Direction:**
   - `m_EjectionDirection`: Drag `Shelf/Regal/Schuss_Richtung` Transform

5. **Create SpawnAnchor:**
   - Right-click `Regal` → Create Empty
   - Name it `SpawnAnchor`
   - Set Position to `(0.8, 1.1, -1.4)` (center of shelf interior)
   - Assign to `m_SpawnAnchor` field

6. **Assign Block Prefabs:**
   - `m_CubeBlockPrefab`: `Assets/BlockBattle/Prefabs/Blocks/Block_Cube.prefab`
   - `m_CylinderBlockPrefab`: `Assets/BlockBattle/Prefabs/Blocks/Block_Cylinder.prefab`
   - `m_TriangleBlockPrefab`: `Assets/BlockBattle/Prefabs/Blocks/Block_Triangle.prefab`
   - `m_RectangleBlockPrefab`: `Assets/BlockBattle/Prefabs/Blocks/Block_Rectangle.prefab`
   - `m_ArchBlockPrefab`: `Assets/BlockBattle/Prefabs/Blocks/Block_Arch.prefab`
   - `m_BigTriangleBlockPrefab`: `Assets/BlockBattle/Prefabs/Blocks/Block_BigTriangle.prefab`

7. **Copy Ejection Settings from old component:**
   - `m_EjectionForce`: 5 (from `ejectionForce`)
   - `m_SpreadAmount`: 0.3 (from `spreadAmount`)
   - `m_TumbleForce`: 10 (from `tumbleForce`)
   - `m_DoorKickForce`: 30 (from `doorKickForce`)
   - `m_TriggerAngle`: 70 (from `triggerAngle`)
   - `m_ResetAngle`: 60 (from `resetAngle`)

8. **Assign BlockSpawnConfiguration:**
   - Create new: Right-click → Create → BlockBattle → Spawn Configuration
   - Or use existing level configuration from `LevelManager`

9. **Disable old component:**
   - Uncheck the checkbox on `ShelfLogic_TwoDoors` component (or remove it)

10. **Update LevelManager (if using):**
    - See "Integrating with LevelManager" section below

---

## Integrating with LevelManager

The current `LevelManager` uses the old `BlockSpawner` component. To use `ShelfBlockSpawner` instead, you need to update `LevelManager.cs`:

### Option 1: Modify LevelManager to use ShelfBlockSpawner

Replace the `BlockSpawner` reference with `ShelfBlockSpawner`:

```csharp
// In LevelManager.cs, change:
[SerializeField, Tooltip("Reference to the BlockSpawner")]
private BlockSpawner m_BlockSpawner;

// To:
[SerializeField, Tooltip("Reference to the ShelfBlockSpawner")]
private ShelfBlockSpawner m_ShelfSpawner;
```

Then update the `StartLevel()` method:

```csharp
// Change:
if (m_BlockSpawner != null)
{
    m_BlockSpawner.SpawnConfiguration = levelConfig;
    m_BlockSpawner.SpawnBlocks();
}

// To:
if (m_ShelfSpawner != null)
{
    m_ShelfSpawner.SpawnConfiguration = levelConfig;
    m_ShelfSpawner.SpawnBlocks();
}
```

### Option 2: Keep Both (Transition Period)

Keep both references during transition:

```csharp
[SerializeField] private BlockSpawner m_BlockSpawner;      // Old (table spawning)
[SerializeField] private ShelfBlockSpawner m_ShelfSpawner; // New (shelf spawning)

[SerializeField] private bool m_UseShelfSpawner = true;    // Toggle between them

private void SpawnPlayerBlocks(BlockSpawnConfiguration config)
{
    if (m_UseShelfSpawner && m_ShelfSpawner != null)
    {
        m_ShelfSpawner.SpawnConfiguration = config;
        m_ShelfSpawner.SpawnBlocks();
    }
    else if (m_BlockSpawner != null)
    {
        m_BlockSpawner.SpawnConfiguration = config;
        m_BlockSpawner.SpawnBlocks();
    }
}
```

### Scene References

After modifying `LevelManager.cs`:

1. Select `LevelManager` GameObject in Hierarchy
2. In Inspector, assign:
   - `m_ShelfSpawner`: Drag `Shelf/Regal` (with ShelfBlockSpawner component)
3. Clear or keep `m_BlockSpawner` reference as needed

---

## Troubleshooting

### Blocks Don't Spawn

1. Check that `SpawnConfiguration` is assigned
2. Verify configuration has spawn entries
3. Check that block prefabs are assigned
4. Look for warnings in console about missing prefabs

### Blocks Don't Eject

1. Check that blocks have `Rigidbody` components
2. Verify blocks are inside the trigger collider
3. Ensure door `HingeJoint` references are assigned
4. Check door angles are being read correctly (debug log)

### Doors Don't Move

1. Verify `HingeJoint` is configured correctly
2. Check `Rigidbody` is not kinematic
3. Ensure connected body reference is correct
4. Check joint limits aren't too restrictive

### Blocks Eject in Wrong Direction

1. Check `m_EjectionDirection` transform's forward (blue arrow) in Scene view
2. Verify transform rotation is correct
3. Consider the shelf's world rotation

### System Doesn't Reset

1. Ensure `m_ResetAngle` < `m_TriggerAngle`
2. Check BOTH doors can close below `m_ResetAngle`
3. Verify door limits allow closing

---

## Code Reference

**Full Source:** `Assets/BlockBattle/Scripts/ShelfBlockSpawner.cs`

```csharp
namespace BlockBattle
{
    public class ShelfBlockSpawner : MonoBehaviour
    {
        [Header("Block Spawning")]
        private BlockSpawnConfiguration m_SpawnConfiguration;
        // Block prefab references (6 types)

        [Header("Spawn Position")]
        private Transform m_SpawnAnchor;
        private float m_SpawnSpacing = 0.12f;
        private Vector3 m_SpawnDirection = Vector3.right;
        private bool m_RandomizeSpawnOrder = true;

        [Header("Door Settings")]
        private HingeJoint m_LeftDoor;
        private HingeJoint m_RightDoor;
        private float m_TriggerAngle = 70f;
        private float m_ResetAngle = 60f;
        private float m_DoorKickForce = 30f;

        [Header("Ejection Settings")]
        private Transform m_EjectionDirection;
        private float m_EjectionForce = 15f;
        private float m_SpreadAmount = 0.3f;
        private float m_TumbleForce = 10f;

        // Events
        public event Action<int> OnBlocksSpawned;
        public event Action<int> OnBlocksEjected;
        public event Action OnShelfReset;

        // Public API
        public void SpawnBlocks() { /* ... */ }
        public void ClearSpawnedBlocks() { /* ... */ }
        public void ForceEject() { /* ... */ }
        public void ResetTriggerState() { /* ... */ }
        
        public BlockSpawnConfiguration SpawnConfiguration { get; set; }
        public int StoredBlockCount { get; }
        public bool HasTriggered { get; }
    }
}
```

---

## Legacy Component Reference

### ShelfLogic_TwoDoors (Deprecated)

**Full Source:** `Assets/Scenes/BlockBattleScene/ShelfLogic_AngleTrigger.cs`

This component only handles ejection, not spawning. Use `ShelfBlockSpawner` for new implementations.

```csharp
namespace Scenes.Sandbox
{
    public class ShelfLogic_TwoDoors : MonoBehaviour
    {
        public float spreadAmount = 0.3f;
        public float tumbleForce = 10f;
        public float ejectionForce = 15f;
        public Transform ejectionDirection;
        public HingeJoint leftDoor;  
        public HingeJoint rightDoor;
        public float triggerAngle = 70f;
        public float resetAngle = 60f;
        public float doorKickForce = 30f;

        private bool hasTriggered = false; 
        private List<Rigidbody> storedBlocks = new List<Rigidbody>();
    }
}
```
