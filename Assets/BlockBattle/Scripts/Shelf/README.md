# Shelf System

## Purpose

The Shelf System spawns blocks inside a shelf and ejects them when the doors are opened. The shelf has two hinged doors that players can interact with in VR. When opened past a certain angle (70° default), all stored blocks are ejected with randomized physics for a chaotic, fun experience. The system also tracks when blocks are returned to the shelf for level completion.

## Core Classes

### ShelfBlockSpawner.cs
Unified component that combines block spawning with door-triggered ejection mechanics.

**Key Features:**
- Spawns blocks inside shelf based on `BlockSpawnConfiguration`
- Monitors door angles via HingeJoint components
- Ejects blocks with randomized physics when doors open past threshold
- Tracks blocks inside shelf via trigger collider
- Motor-driven doors for haptic feedback (auto-open at 30°)

**Usage:**
```csharp
ShelfBlockSpawner spawner = GetComponent<ShelfBlockSpawner>();
spawner.SpawnConfiguration = levelConfig;
spawner.SpawnBlocks(); // Blocks spawn inside shelf

// Blocks automatically eject when doors open past m_TriggerAngle (70°)
```

### ShelfProgressUI.cs
UI component showing shelf door status and block return progress.

**Key Features:**
- Displays door open/closed status
- Shows count of blocks returned to shelf
- Visual feedback for level completion

## How It Works

### 1. Block Spawning

Blocks are spawned inside the shelf as kinematic (no physics) until ejected:

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

### 2. Door Angle Monitoring

Every frame, the system checks both door angles:

```csharp
private void MonitorDoors()
{
    float angleL = Mathf.Abs(m_LeftDoor.angle);
    float angleR = Mathf.Abs(m_RightDoor.angle);

    // Auto-Open: Türen öffnen sich motorisiert ab 30° Winkel
    if (angle >= m_AutoOpenThreshold) 
        EnableDoorMotor(door, true);

    // Eject: Kickt Blöcke physikalisch raus, wenn Türen weit offen (70°)
    if ((angleL >= m_TriggerAngle || angleR >= m_TriggerAngle) && !_hasTriggered)
    {
        EjectBlocks(); // Wendet Force & Torque auf alle Rigidbodies an
        _hasTriggered = true;
    }

    // Reset: When BOTH doors close below reset angle
    if (angleL < m_ResetAngle && angleR < m_ResetAngle && _hasTriggered)
    {
        _hasTriggered = false;
        OnShelfReset?.Invoke();
    }
}
```

### 3. Block Ejection

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

### 4. Block Storage Tracking

The shelf uses a trigger collider to detect and track blocks:

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
         │ Either door angle >= triggerAngle (70°)
         ▼
┌─────────────────┐
│    FIRED        │
│  _hasTriggered  │──────────────────┐
│    = true       │                  │
│  Blocks ejected │                  │
└────────┬────────┘                  │
         │                           │
         │ BOTH doors < resetAngle   │ Can call SpawnBlocks()
         │ (60°)                     │ again to reload
         ▼                           │
┌─────────────────┐                  │
│   RESET         │◄─────────────────┘
│  _hasTriggered  │
│    = false      │
│  OnShelfReset   │
└─────────────────┘
```

## Testing

### Manual Testing
1. Assign `BlockSpawnConfiguration` to `SpawnConfiguration` field
2. Call `SpawnBlocks()` - blocks should appear inside shelf
3. Open doors past 70° - blocks should eject with physics
4. Close both doors below 60° - system should reset
5. Test auto-open motor (doors should motorize at 30°)

### Debug
- Check `StoredBlockCount` property for current block count
- Monitor `HasTriggered` to see if ejection has fired
- Use `ForceEject()` for testing without door interaction

## Key Technical Decisions

### Why Motor-Driven Doors?
**Problem:** Players need haptic feedback when opening doors.

**Solution:** Enable HingeJoint motor at 30° angle threshold. This provides resistance and auto-opens doors, making interaction feel more physical and responsive.

### Why Randomized Ejection Physics?
**Problem:** Predictable block trajectories are boring.

**Solution:** Apply random spread (0-30% default), random force variation (80-120%), and random torque for realistic tumbling. Creates chaotic, fun block ejection.

### Why Two-Door System?
**Problem:** Single door is less interactive.

**Solution:** Two doors allow players to open either side. System triggers when either door opens past threshold, but only resets when BOTH close. This prevents accidental resets.

## Known Limitations

1. **Hardcoded Angles:** Trigger (70°) and reset (60°) angles are configurable but may need per-level tuning
2. **Block Detection:** Relies on trigger collider; blocks must have Rigidbody
3. **Performance:** Door angle monitoring runs every frame (could be optimized with events)
4. **Door Limits:** HingeJoint limits must allow doors to open past trigger angle

## Configuration

### Recommended Settings

**Default (Balanced):**
- `m_TriggerAngle`: 70°
- `m_ResetAngle`: 60°
- `m_EjectionForce`: 8-15 N
- `m_SpreadAmount`: 0.3 (30% spread)
- `m_TumbleForce`: 5-10 N⋅m
- `m_DoorKickForce`: 30 N⋅m

**Easy Mode:**
- `m_EjectionForce`: 5 N (slower blocks)
- `m_SpreadAmount`: 0.1 (more predictable)

**Hard Mode:**
- `m_EjectionForce`: 20 N (faster blocks)
- `m_SpreadAmount`: 0.6 (more chaotic)

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
- **Connected Body:** Shelf body Rigidbody (or null for world)
- **Anchor:** Door hinge position (local)
- **Axis:** (0, 1, 0) for vertical rotation
- **Use Limits:** Enabled
- **Min Limit:** -120° (or 0° for one-way)
- **Max Limit:** 120° (or 0° for one-way)
- **Use Motor:** Optional - for auto-open behavior

## Related Documentation

- [Main Scripts Overview](../README.md)
- [Detailed Shelf System Documentation](../../../Docs/shelf_system.md)
- [Level Manager Integration](../GameFlow/README.md)
