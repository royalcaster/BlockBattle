# BuildValidator API Documentation

## Overview

The `BuildValidator` component validates a player's build against a reference structure. It compares placed blocks to reference blocks from a `BlockSpawnConfiguration` and calculates accuracy based on position and rotation errors.

## Component Setup

### Inspector Properties

- **Position Tolerance**: Maximum position error in meters for a block to be considered correct (default: 0.05m = 5cm)
- **Rotation Tolerance**: Maximum rotation error in degrees for a block to be considered correct (default: 15°)
- **Max Height Above Table**: Maximum height above table to consider blocks as 'placed' (default: 1.0m)
- **Max Distance From Table**: Maximum horizontal distance from table center to consider blocks (default: 2.0m)
- **Table**: Table GameObject reference (auto-finds "Table" if null)
- **Reference Configuration**: The `BlockSpawnConfiguration` to validate against
- **Reference Structure Spawner**: Optional reference to `ReferenceStructureSpawner` to get actual spawned structure positions (if null, calculates from configuration)
- **Table Offset**: Offset from table position for reference structure (should match `ReferenceStructureSpawner`'s TableOffset, default: Vector3.zero)

## Usage

### Basic Validation

```csharp
using BlockBattle;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private BuildValidator m_BuildValidator;
    [SerializeField] private BlockSpawnConfiguration m_ReferenceStructure;

    public void ValidatePlayerBuild()
    {
        // Set the reference configuration
        m_BuildValidator.ReferenceConfiguration = m_ReferenceStructure;

        // Validate the build
        BuildValidationResult result = m_BuildValidator.ValidateBuild();

        // Display results
        Debug.Log($"Build Accuracy: {result.AccuracyPercentage:F1}%");
        Debug.Log($"Correct Blocks: {result.CorrectBlocks}/{result.TotalReferenceBlocks}");
        Debug.Log($"Placed Blocks Found: {result.PlacedBlocksFound}");
    }
}
```

### Accessing Detailed Results

```csharp
BuildValidationResult result = m_BuildValidator.ValidateBuild();

// Check overall accuracy
float accuracy = result.AccuracyPercentage;

// Iterate through per-block results
foreach (BlockValidationResult blockResult in result.BlockResults)
{
    if (blockResult.IsCorrect)
    {
        Debug.Log($"Block {blockResult.BlockType} ({blockResult.BlockColor}) is correct!");
    }
    else
    {
        Debug.Log($"Block {blockResult.BlockType} ({blockResult.BlockColor}) is incorrect:");
        Debug.Log($"  Position Error: {blockResult.PositionError:F3}m");
        Debug.Log($"  Rotation Error: {blockResult.RotationError:F1}°");
    }
}

// Check for extra blocks (blocks placed but not in reference)
if (result.ExtraBlocks.Count > 0)
{
    Debug.Log($"Found {result.ExtraBlocks.Count} extra blocks that don't match the reference");
}

// Check for missing blocks (reference blocks not placed)
if (result.MissingBlocks.Count > 0)
{
    Debug.Log($"Missing {result.MissingBlocks.Count} blocks from the reference structure");
}
```

### Programmatic Configuration

```csharp
// Adjust tolerances at runtime
m_BuildValidator.PositionTolerance = 0.1f; // 10cm tolerance
m_BuildValidator.RotationTolerance = 20f; // 20° tolerance

// Change reference configuration
m_BuildValidator.ReferenceConfiguration = newStructureConfig;
```

## Validation Algorithm

1. **Collect Placed Blocks**: Finds all blocks with `XRGrabInteractable` component that are:
   - Not currently being held (not selected)
   - Within the configured height above table
   - Within the configured horizontal distance from table

2. **Match Blocks**: For each reference block:
   - Finds the closest placed block of the same type AND color
   - Calculates position error (Vector3.Distance)
   - Calculates rotation error (Quaternion.Angle)
   - Marks as correct if both errors are within tolerance

3. **Calculate Accuracy**: Percentage of correctly placed blocks out of total reference blocks

## Data Structures

### BuildValidationResult

- `AccuracyPercentage` (float): Overall accuracy 0-100%
- `CorrectBlocks` (int): Number of correctly placed blocks
- `TotalReferenceBlocks` (int): Total number of reference blocks
- `PlacedBlocksFound` (int): Number of placed blocks found near table
- `BlockResults` (List<BlockValidationResult>): Per-block validation results
- `ExtraBlocks` (List<GameObject>): Blocks placed but not matching any reference
- `MissingBlocks` (List<BlockSpawnEntry>): Reference blocks with no matching placed block

### BlockValidationResult

- `PlacedBlock` (GameObject): The placed block GameObject (null if missing)
- `ReferenceEntry` (BlockSpawnEntry): The reference block entry
- `PositionError` (float): Position error in meters
- `RotationError` (float): Rotation error in degrees
- `IsCorrect` (bool): Whether block is within tolerance
- `BlockType` (BlockType): Type of the block
- `BlockColor` (BlockColor): Color of the block

## Integration with Reference Structure System

The `BuildValidator` works seamlessly with the `ReferenceStructureSpawner`:

```csharp
[SerializeField] private ReferenceStructureSpawner m_ReferenceSpawner;
[SerializeField] private BuildValidator m_BuildValidator;

void Start()
{
    // Spawn reference structure
    BlockSpawnConfiguration config = // ... load configuration
    m_ReferenceSpawner.SpawnStructure(config);
    
    // Set same configuration for validation
    m_BuildValidator.ReferenceConfiguration = config;
}

void OnBuzzerPressed()
{
    // Validate build when player presses buzzer
    BuildValidationResult result = m_BuildValidator.ValidateBuild();
    
    // Show results to player
    DisplayResults(result);
}
```

## Notes

- Blocks must have `XRGrabInteractable` component to be detected
- Blocks currently being held are excluded from validation
- Block type and color matching is required (blocks must match both)
- Reference structure positions are relative to table position
- The validator automatically finds the table GameObject if not assigned

