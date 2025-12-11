# Reference Structure System - Programmatic API

## Overview

The `ReferenceStructureSpawner` component provides a complete API for programmatically managing reference structures in a game loop. You can spawn different structures one after another, check status, and respond to events.

## Key Components

- **ReferenceStructureSpawner**: Main component that spawns and manages reference structures
- **BlockSpawnConfiguration**: ScriptableObject asset that defines a structure (block types, positions, rotations, and colors)

## Public API

### Properties

```csharp
// Check if structure is currently spawned
bool IsStructureSpawned { get; }

// Get currently spawned spawn configuration
BlockSpawnConfiguration CurrentSpawnConfiguration { get; }

// Get number of blocks in current structure
int SpawnedBlockCount { get; }

// Set spawn configuration (auto-spawns if in play mode)
BlockSpawnConfiguration SpawnConfiguration { get; set; }
```

### Methods

```csharp
// Spawn structure using currently assigned SpawnConfiguration
void SpawnStructure()

// Spawn a specific BlockSpawnConfiguration (clears existing structure first)
void SpawnStructure(BlockSpawnConfiguration spawnConfiguration)

// Clear the currently spawned structure
void ClearStructure()
```

### Events

```csharp
// Fired when a structure is successfully spawned
event Action<BlockSpawnConfiguration> OnStructureSpawned

// Fired when a structure is cleared/destroyed
event Action OnStructureCleared
```

## Usage Examples

### Basic Game Loop - Switching Structures

```csharp
using UnityEngine;
using BlockBattle;

public class StructureGameManager : MonoBehaviour
{
    [SerializeField] private ReferenceStructureSpawner structureSpawner;
    [SerializeField] private BlockSpawnConfiguration[] levelStructures;
    
    private int currentLevelIndex = 0;

    void Start()
    {
        // Subscribe to events
        structureSpawner.OnStructureSpawned += OnStructureSpawned;
        structureSpawner.OnStructureCleared += OnStructureCleared;
        
        // Start first level
        LoadNextStructure();
    }

    void LoadNextStructure()
    {
        if (currentLevelIndex >= levelStructures.Length)
        {
            Debug.Log("All levels completed!");
            return;
        }

        // Clear existing structure and spawn new one
        structureSpawner.SpawnStructure(levelStructures[currentLevelIndex]);
    }

    void OnStructureSpawned(BlockSpawnConfiguration config)
    {
        Debug.Log($"Structure '{config.ConfigurationName}' spawned with {structureSpawner.SpawnedBlockCount} blocks");
        // Start gameplay, enable player controls, etc.
    }

    void OnStructureCleared()
    {
        Debug.Log("Structure cleared");
        // Cleanup, show results, etc.
    }

    // Called when player completes a structure
    public void OnStructureCompleted()
    {
        // Clear current structure
        structureSpawner.ClearStructure();
        
        // Wait a moment, then load next
        Invoke(nameof(LoadNextStructure), 2f);
    }
}
```

### Check Structure Status

```csharp
if (structureSpawner.IsStructureSpawned)
{
    Debug.Log($"Current structure: {structureSpawner.CurrentSpawnConfiguration.ConfigurationName}");
    Debug.Log($"Block count: {structureSpawner.SpawnedBlockCount}");
}
```

### Manual Structure Management

```csharp
// Clear existing structure
structureSpawner.ClearStructure();

// Spawn new structure
structureSpawner.SpawnStructure(mySpawnConfiguration);

// Or use the property (auto-spawns)
structureSpawner.SpawnConfiguration = mySpawnConfiguration;
```

### Using Spawn Configurations

```csharp
[SerializeField] private BlockSpawnConfiguration[] difficultyLevels;

void LoadDifficultyLevel(int difficulty)
{
    if (difficulty >= 0 && difficulty < difficultyLevels.Length)
    {
        structureSpawner.SpawnStructure(difficultyLevels[difficulty]);
    }
}
```

## Complete Game Loop Example

```csharp
using UnityEngine;
using BlockBattle;
using System.Collections;

public class CompleteGameLoop : MonoBehaviour
{
    [SerializeField] private ReferenceStructureSpawner structureSpawner;
    [SerializeField] private BlockSpawnConfiguration[] structures;
    
    private int currentStructureIndex = 0;
    private bool isPlaying = false;

    void Start()
    {
        structureSpawner.OnStructureSpawned += HandleStructureSpawned;
        structureSpawner.OnStructureCleared += HandleStructureCleared;
        
        StartCoroutine(GameLoop());
    }

    IEnumerator GameLoop()
    {
        while (currentStructureIndex < structures.Length)
        {
            // Load structure
            structureSpawner.SpawnStructure(structures[currentStructureIndex]);
            
            // Wait for structure to spawn
            yield return new WaitUntil(() => structureSpawner.IsStructureSpawned);
            
            // Start gameplay
            isPlaying = true;
            Debug.Log($"Level {currentStructureIndex + 1} started!");
            
            // Wait for player to complete (you'd check validation here)
            yield return new WaitUntil(() => !isPlaying);
            
            // Clear structure
            structureSpawner.ClearStructure();
            
            // Wait for clear
            yield return new WaitUntil(() => !structureSpawner.IsStructureSpawned);
            
            // Brief pause between levels
            yield return new WaitForSeconds(2f);
            
            currentStructureIndex++;
        }
        
        Debug.Log("All structures completed!");
    }

    void HandleStructureSpawned(BlockSpawnConfiguration config)
    {
        Debug.Log($"Spawned: {config.ConfigurationName}");
    }

    void HandleStructureCleared()
    {
        Debug.Log("Structure cleared");
    }

    // Call this when player completes a structure
    public void CompleteCurrentStructure()
    {
        isPlaying = false;
    }
}
```

## Notes

- Structures are automatically cleared when spawning a new one
- The `StructureData` and `SpawnConfiguration` properties auto-spawn when set (only in play mode)
- Use `SpawnStructure(StructureData)` for explicit control without auto-spawning
- Events are fired after successful spawn/clear operations
- All spawned blocks are automatically made static (no physics, no interaction)
- Holographic effect is applied by default (configurable in inspector)

