# Block System

## Purpose

The Block System handles block spawning, configuration, and structure definitions. It provides ScriptableObject-based configuration for easy level design and supports both table spawning (legacy) and shelf spawning (current).

## Core Classes

### BlockSpawner.cs
Legacy component that spawns blocks on the table. **Note:** Use `ShelfBlockSpawner` for new implementations.

**Key Features:**
- Spawns blocks on table at runtime
- Supports configuration-based or simple spawning
- Can spawn as static (for reference structures)

### ShelfBlockSpawner.cs
**See [Shelf System Documentation](../Shelf/README.md)** - This is the recommended component for block spawning.

### BlockSpawnConfiguration.cs
ScriptableObject asset that defines a structure layout.

**Key Features:**
- List of `BlockSpawnEntry` (block type, color, position, rotation)
- Can be created in editor via "Create → BlockBattle → Spawn Configuration"
- Used by both spawners and reference structure system

**Usage:**
```csharp
// Create in editor: Right-click → Create → BlockBattle → Spawn Configuration
// Or access programmatically:
BlockSpawnConfiguration config = ScriptableObject.CreateInstance<BlockSpawnConfiguration>();
config.SpawnEntries.Add(new BlockSpawnEntry
{
    BlockType = BlockType.Cube,
    BlockColor = BlockColor.Red,
    RelativePosition = new Vector3(0, 0, 0),
    Rotation = Quaternion.identity
});
```

### StructureData.cs
Data structures for block configurations.

**Key Classes:**
- `BlockSpawnEntry` - Single block definition (type, color, position, rotation)
- Helper methods for structure manipulation

### ReferenceStructureSpawner.cs
Spawns holographic reference structures next to the build table.

**Key Features:**
- Spawns structure from `BlockSpawnConfiguration`
- Applies holographic shader effect
- Allows rotation via XR controllers
- Auto-hides when building phase completes

**Usage:**
```csharp
ReferenceStructureSpawner spawner = GetComponent<ReferenceStructureSpawner>();
spawner.SpawnConfiguration = levelConfig;
spawner.SpawnStructure(); // Creates holographic preview
```

### BlockReference.cs
Component attached to block prefabs for identification.

**Key Features:**
- Stores block type and color
- Used by validation system to identify blocks
- Required for all block prefabs

### BlockType.cs / BlockColor.cs
Enums for block identification.

**Block Types:**
- Cube
- Cylinder
- Triangle
- Rectangle
- Arch
- BigTriangle

**Block Colors:**
- Red, Green, Blue, Yellow, Orange, DarkGreen, Natural

## How It Works

### 1. Configuration Creation

Levels are defined as ScriptableObject assets:

1. Right-click in Project window
2. Select "Create → BlockBattle → Spawn Configuration"
3. Name the asset (e.g., "Level1_Configuration")
4. Add `BlockSpawnEntry` items to the list
5. Set position, rotation, type, and color for each block

### 2. Block Spawning

Blocks are spawned from configuration:

```csharp
public void SpawnBlocks()
{
    foreach (BlockSpawnEntry entry in m_SpawnConfiguration.SpawnEntries)
    {
        // Get prefab for block type
        GameObject prefab = GetPrefabForType(entry.BlockType);
        
        // Calculate spawn position
        Vector3 spawnPos = m_SpawnAnchor.position + entry.RelativePosition;
        
        // Instantiate block
        GameObject block = Instantiate(prefab, spawnPos, entry.Rotation);
        
        // Set color via material
        SetBlockColor(block, entry.BlockColor);
        
        // Add BlockReference component
        BlockReference blockRef = block.GetComponent<BlockReference>();
        if (blockRef == null)
        {
            blockRef = block.AddComponent<BlockReference>();
        }
        blockRef.BlockType = entry.BlockType;
        blockRef.BlockColor = entry.BlockColor;
    }
}
```

### 3. Reference Structure Spawning

Reference structures use the same configuration but with holographic effect:

```csharp
public void SpawnStructure()
{
    // Spawn blocks from configuration
    foreach (BlockSpawnEntry entry in m_SpawnConfiguration.SpawnEntries)
    {
        GameObject block = SpawnBlock(entry);
        
        // Apply holographic material
        if (m_UseHolographicEffect)
        {
            ApplyHolographicMaterial(block);
        }
        
        // Make static (no physics)
        block.GetComponent<Rigidbody>().isKinematic = true;
    }
}
```

## Testing

### Manual Testing
1. Create `BlockSpawnConfiguration` asset
2. Add block entries with different types/colors
3. Assign to `ShelfBlockSpawner.SpawnConfiguration`
4. Call `SpawnBlocks()` - verify blocks spawn correctly
5. Test with `ReferenceStructureSpawner` - verify holographic effect

### Editor Tools
- Use structure recording tools (if available) to capture structures from scene
- Use validation system to verify spawned structures match configuration

## Key Technical Decisions

### Why ScriptableObject Configuration?
**Problem:** Hardcoding structures in code is inflexible and requires recompilation.

**Solution:** Use ScriptableObject assets. Designers can create/modify levels without touching code. Assets can be version controlled and shared easily.

### Why Relative Positions?
**Problem:** Absolute world positions break when table is moved/rotated.

**Solution:** Store positions relative to spawn anchor. System transforms to world space at spawn time. This allows flexible scene layout.

### Why Holographic Effect for Reference?
**Problem:** Reference structure should be visible but not confused with player's build.

**Solution:** Apply custom holographic shader with transparency, emission, and fresnel effects. Makes reference clearly distinct while still visible.

## Known Limitations

1. **Manual Configuration:** Structures must be manually defined in editor (no automatic recording tool yet)
2. **Block Prefab Requirements:** All block types must have prefabs assigned
3. **Color System:** Colors are set via materials; changing requires material assignment
4. **Static Reference:** Reference structures are static (no physics) - cannot be interacted with

## Configuration

### Block Prefab Setup
Each block prefab must have:
- `Rigidbody` component (for physics)
- `XRGrabInteractable` component (for VR interaction)
- `BlockReference` component (for identification)
- `Collider` component (for physics and interaction)
- Appropriate mesh and material

### Spawn Configuration Setup
1. Create `BlockSpawnConfiguration` asset
2. For each block in structure:
   - Set `BlockType` (Cube, Cylinder, etc.)
   - Set `BlockColor` (Red, Green, etc.)
   - Set `RelativePosition` (position relative to spawn anchor)
   - Set `Rotation` (Quaternion for block orientation)

### Reference Structure Setup
1. Assign `BlockSpawnConfiguration` to `ReferenceStructureSpawner`
2. Set table offset (default: 1.5m to the right)
3. Configure holographic effect settings (transparency, emission, etc.)
4. Assign block prefabs (same as spawner)

## Related Documentation

- [Main Scripts Overview](../README.md)
- [Shelf System](../Shelf/README.md) - Current spawning system
- [Validation System](../Validation/README.md) - How blocks are validated
- [Reference Structure API](../../../Docs/reference_structure_api.md)
