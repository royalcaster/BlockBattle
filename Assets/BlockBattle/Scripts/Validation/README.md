# Validation System

## Purpose

The Validation System validates a player's build against a reference structure using relative position matching. This allows the build table to be rotated in the VR space without breaking validation logic, as the system compares positions relative to the build zone rather than absolute world coordinates.

## Core Classes

### BuildValidator.cs
Main validation component that compares placed blocks against reference structure.

**Key Features:**
- Relative position matching (allows table rotation)
- Configurable tolerance values (position: 10cm default, rotation: 20° default)
- Auto-alignment to best match player's build orientation
- Presence-only validation mode (ignore position/rotation)

**Usage:**
```csharp
BuildValidator validator = GetComponent<BuildValidator>();
BuildValidationResult result = validator.ValidateBuild();
float accuracy = result.AccuracyPercentage;
```

### BuildZone.cs
Defines the build area where blocks are validated. Only blocks within this zone are considered.

**Key Features:**
- Configurable zone size (width, height, depth)
- Visual gizmo visualization in editor
- Bounds checking for block detection

**Usage:**
```csharp
BuildZone zone = GetComponent<BuildZone>();
bool isInside = zone.ZoneBounds.Contains(point);
```

### BuildZonePlacementGuides.cs
Shows visual "ghost" markers on the build zone floor indicating where blocks should be placed.

**Key Features:**
- Automatically creates guides from reference structure
- Guides rotate with table (local coordinates)
- Color changes when blocks are correctly placed
- Prevents duplicate guides with minimum spacing

**Usage:**
Attach to BuildZone GameObject. Guides are automatically generated from BuildValidator's reference configuration.

## How It Works

### 1. Relative Position Calculation

The system calculates expected positions relative to the build zone center, then transforms them to world space accounting for table rotation:

```csharp
// Combine zone rotation with offset (e.g., table rotated)
Quaternion zoneRotation = m_BuildZone.transform.rotation;
Quaternion totalRotation = zoneRotation * Quaternion.Euler(0, yRotationOffset, 0);

// Transform relative expected position to world coordinates
Vector3 expectedPos = totalRotation * relativePos;

// Compare with actual block position
float dist = Vector3.Distance(blockRelPos, expectedPos);
if (dist <= m_PositionTolerance) correctCount++;
```

### 2. Auto-Alignment

When `m_AutoAlignToBuild` is enabled, the system automatically finds the best rotation offset to match the player's build:

- Calculates build center from placed blocks
- Tries rotation offsets in 15° increments (0°, 15°, 30°, ... 345°)
- Selects rotation with highest match count
- Locks rotation after first block is placed (prevents jitter)

### 3. Validation Modes

**Full Validation (Default):**
- Checks position (within tolerance)
- Optionally checks rotation (if `m_ValidateRotation` enabled)
- Requires correct block type and color

**Presence-Only Mode:**
- Only checks if correct block type/color is present in zone
- Ignores position and rotation
- Useful for testing or relaxed gameplay

## Testing

### Manual Testing
1. Place blocks in build zone
2. Check `ValidationHUD` for real-time accuracy
3. Verify tolerance values work for your level complexity
4. Test with rotated table to confirm relative positioning

### Debug Visualization
Enable `m_ShowDebugGizmos` in BuildValidator to see:
- Expected block positions (wireframe cubes)
- Actual block positions
- Error vectors showing position differences

## Key Technical Decisions

### Why Relative Positioning?
**Problem:** Absolute world coordinates break when table is rotated in VR space.

**Solution:** Store reference positions relative to build center, then transform to world space accounting for current table rotation. This allows players to rotate the table without breaking validation.

### Why Auto-Alignment?
**Problem:** Players may build at different orientations than reference structure.

**Solution:** Automatically detect best rotation offset by trying multiple angles and selecting the one with highest match count. Locks after first block to prevent jitter.

### Why Tolerance-Based Matching?
**Problem:** VR hand tracking isn't pixel-perfect, and physics may cause slight block movement.

**Solution:** Use configurable tolerance values (10cm position, 20° rotation) to allow for natural imprecision while still requiring accuracy.

## Known Limitations

1. **Tolerance Tuning:** Default values (10cm, 20°) may need adjustment per level complexity
2. **Single-Block Structures:** Auto-alignment may struggle with very simple structures
3. **Performance:** Validation runs on main thread; could be optimized with Unity Jobs
4. **Rotation Validation:** Currently optional; may need refinement for complex structures

## Configuration

### Recommended Settings

**For Placement Guides (Guided Building):**
- `m_PositionTolerance`: 0.10m (10cm)
- `m_RotationTolerance`: 20°
- `m_ValidateRotation`: true
- `m_AutoAlignToBuild`: false (guides remove rotation ambiguity)

**For Free Building (No Guides):**
- `m_PositionTolerance`: 0.10m (10cm)
- `m_RotationTolerance`: 20°
- `m_ValidateRotation`: true
- `m_AutoAlignToBuild`: true (auto-detect orientation)

**For Relaxed Mode:**
- `m_PresenceOnlyValidation`: true
- Ignores position/rotation entirely

## Related Documentation

- [Main Scripts Overview](../README.md)
- [Build Zone Placement Guides API](../../../Docs/reference_structure_api.md)
- [Validation HUD API](../../../Docs/validation_hud_api.md)
