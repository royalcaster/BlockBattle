# Game Flow System

## Purpose

The Game Flow System manages level progression, phase transitions, and the overall game loop in BlockBattle. It coordinates between building, validation, destruction, and block return phases, handling timer management and final scoring.

## Core Classes

### LevelManager.cs
Main orchestrator for game flow and level progression.

**Key Features:**
- Manages level configurations (list of `BlockSpawnConfiguration` assets)
- Handles phase transitions (Building → Destruction → WaitingForReturn → Countdown)
- Timer management (stops only when blocks returned in final level)
- Teleports player between phases
- Final score calculation

**Usage:**
```csharp
LevelManager manager = GetComponent<LevelManager>();
// Levels are automatically loaded from m_LevelConfigurations list
// System handles all phase transitions automatically
```

### StartScreenUI.cs
Main menu and game start UI.

**Key Features:**
- Start game button
- Level selection (if implemented)
- Settings menu

## How It Works

### 1. Level Progression

The system loads levels sequentially from the configuration list:

```csharp
public void StartLevel(int levelIndex)
{
    m_CurrentLevelIndex = levelIndex;
    BlockSpawnConfiguration levelConfig = m_LevelConfigurations[levelIndex];
    
    // Spawn blocks in shelf
    m_ShelfSpawner.SpawnConfiguration = levelConfig;
    m_ShelfSpawner.SpawnBlocks();
    
    // Spawn reference structure
    m_ReferenceSpawner.SpawnConfiguration = levelConfig;
    m_ReferenceSpawner.SpawnStructure();
    
    // Reset timer
    m_GameTimer = 0f;
    m_TimerRunning = true;
    
    // Set phase to Building
    m_CurrentPhase = LevelPhase.Building;
}
```

### 2. Phase Transitions

The game progresses through phases:

**Building Phase:**
- Player builds structure in build zone
- Validation runs continuously (via ValidationHUD)
- When accuracy reaches threshold (100% default), phase completes

**Destruction Phase:**
- Player is teleported to slingshot position
- DestructionPhaseManager activates
- Player shoots at structure until all blocks cleared

**WaitingForReturn Phase:**
- Player must return blocks to shelf
- ShelfProgressUI tracks progress
- When all blocks returned AND doors closed, phase completes

**Countdown Phase:**
- Brief countdown before next level
- Shows success message

**Transitioning Phase:**
- Loads next level configuration
- Resets all systems

### 3. Timer Management

The timer runs during building and destruction, but stops only when blocks are returned in the final level:

```csharp
private void OnShelfDoorsClosedWithBlocks()
{
    // Prüfung nur im letzten Level
    bool isLastLevel = (m_CurrentLevelIndex >= m_LevelConfigurations.Count - 1);
    
    if (isLastLevel && m_CurrentPhase == LevelPhase.WaitingForReturn)
    {
        // Timer stoppt erst hier -> "Aufräumen" ist Teil des Gameplays!
        m_TimerRunning = false;
        m_FinalTime = m_GameTimer;
        ShowFinalScore();
    }
}
```

### 4. Final Score

The final score is calculated from:
- Total time taken
- Accuracy across all levels (if tracked)
- Number of levels completed

## Testing

### Manual Testing
1. Start game from StartScreenUI
2. Build structure - verify validation works
3. Complete build - verify destruction phase starts
4. Destroy structure - verify return phase starts
5. Return blocks - verify level progression
6. Complete all levels - verify final score shows

### Debug
- Check `m_CurrentPhase` to verify phase state
- Monitor `m_GameTimer` for timer value
- Use `m_CurrentLevelIndex` to track level progress

## Key Technical Decisions

### Why Timer Stops Only in Final Level?
**Problem:** Players should be rewarded for fast building, but cleanup shouldn't be rushed.

**Solution:** Timer runs during building and destruction (skill-based phases), but stops when blocks are returned in final level. This makes cleanup part of the gameplay without time pressure, while still rewarding speed in the main phases.

### Why Phase-Based Architecture?
**Problem:** Different phases need different systems active (validation vs destruction vs return tracking).

**Solution:** Use `LevelPhase` enum to track current phase. Each phase activates/deactivates relevant systems. This keeps code organized and prevents conflicts between systems.

### Why Teleportation Between Phases?
**Problem:** VR space may not accommodate all gameplay areas in one location.

**Solution:** Teleport player to appropriate position for each phase (build zone for building, slingshot for destruction). This allows flexible scene layout and better use of VR space.

## Known Limitations

1. **Hardcoded Thresholds:** Completion threshold (100% accuracy) is configurable but may need per-level tuning
2. **Phase Transitions:** Teleportation may be disorienting for some users
3. **Timer Precision:** Timer uses `Time.deltaTime` which may drift slightly over long sessions
4. **Level Configuration:** Levels must be manually added to list in inspector

## Configuration

### Recommended Settings

**Default:**
- `m_CompletionThreshold`: 100% (perfect build required)
- `m_SuccessDisplayDuration`: 3s
- `m_CountdownDuration`: 3s
- `m_ValidationStartDelay`: 1s (delay before validation starts)

**Easy Mode:**
- `m_CompletionThreshold`: 90% (more forgiving)

**Hard Mode:**
- `m_CompletionThreshold`: 100% (perfect builds only)
- Reduce countdown duration for faster pacing

## Scene Setup

### LevelManager Setup
1. Create empty GameObject "LevelManager"
2. Add `LevelManager` component
3. Assign references:
   - `m_ReferenceSpawner` (ReferenceStructureSpawner)
   - `m_ShelfSpawner` (ShelfBlockSpawner)
   - `m_BuildValidator` (BuildValidator)
   - `m_PlacementGuides` (BuildZonePlacementGuides)
   - `m_GameplayHUD` (GameplayHUD)
   - `m_DestructionManager` (DestructionPhaseManager)
   - `m_XRSetup` (BlockBattleXRSetup)
4. Create `BlockSpawnConfiguration` assets for each level
5. Add configurations to `m_LevelConfigurations` list
6. Assign teleport positions for each phase

### Start Screen Setup
1. Create UI Canvas
2. Add `StartScreenUI` component
3. Assign `LevelManager` reference
4. Create start button and wire to `StartGame()` method

## Related Documentation

- [Main Scripts Overview](../README.md)
- [Validation System](../Validation/README.md) - How validation integrates
- [Shelf System](../Shelf/README.md) - Block return tracking
- [Destruction System](../Destruction/README.md) - Destruction phase
- [Game Flow Documentation](../../../Docs/game_flow_shelf_wall.md)
