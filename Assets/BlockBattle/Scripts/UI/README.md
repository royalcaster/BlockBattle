# UI System

## Purpose

The UI System provides real-time feedback to players during gameplay. It includes HUDs for validation feedback, gameplay progress, and shelf status. All UI elements are designed to work in VR with camera-following behavior.

## Core Classes

### ValidationHUD.cs
Real-time validation feedback HUD showing build accuracy and per-block errors.

**Key Features:**
- Live accuracy percentage updates
- Per-block position/rotation error display
- Missing/extra block lists
- Camera-following behavior (optional)
- Color-coded feedback (green=correct, yellow=partial, red=incorrect)

**Usage:**
```csharp
ValidationHUD hud = GetComponent<ValidationHUD>();
hud.BuildValidator = validator; // Assign validator reference
hud.AutoUpdate = true; // Enable automatic updates
```

### GameplayHUD.cs
Main gameplay HUD with block indicators and animated accuracy progress bar.

**Key Features:**
- Block indicators that "light up" when blocks are present/correct
- Animated progress bar showing build accuracy
- Level and timer display
- Smooth color transitions and animations

**Usage:**
```csharp
GameplayHUD hud = GetComponent<GameplayHUD>();
hud.BuildValidator = validator;
hud.ReferenceSpawner = referenceSpawner;
hud.LevelManager = levelManager;
// HUD automatically updates based on validation results
```

### ShelfProgressUI.cs
UI showing shelf door status and block return progress.

**Key Features:**
- Door open/closed status indicators
- Block return count display
- Visual feedback for level completion

**Usage:**
```csharp
ShelfProgressUI ui = GetComponent<ShelfProgressUI>();
ui.ShelfSpawner = shelfSpawner;
// UI automatically updates based on shelf state
```

### StartScreenUI.cs
Main menu and game start UI.

**Key Features:**
- Start game button
- Level selection (if implemented)
- Settings menu

## How It Works

### 1. Validation HUD Updates

The ValidationHUD periodically queries the BuildValidator and updates display:

```csharp
private void Update()
{
    if (!m_AutoUpdate) return;
    
    m_UpdateTimer += Time.deltaTime;
    if (m_UpdateTimer >= m_UpdateInterval)
    {
        m_UpdateTimer = 0f;
        UpdateDisplay();
    }
}

private void UpdateDisplay()
{
    if (m_BuildValidator == null) return;
    
    BuildValidationResult result = m_BuildValidator.ValidateBuild();
    m_LastResult = result;
    
    // Update accuracy text
    m_AccuracyText.text = $"Accuracy: {result.AccuracyPercentage:F1}%";
    
    // Update block results
    UpdateBlockResults(result);
    
    // Update missing/extra blocks
    UpdateMissingBlocks(result.MissingBlocks);
    UpdateExtraBlocks(result.ExtraBlocks);
}
```

### 2. Camera Following

UI elements can follow the camera for VR comfort:

```csharp
private void Update()
{
    if (m_FollowCamera && m_Camera != null)
    {
        // Calculate position in front of camera
        Vector3 targetPos = m_Camera.transform.position + 
                           m_Camera.transform.forward * m_DistanceFromCamera +
                           Vector3.up * m_HeightOffset;
        
        // Smoothly move towards target
        transform.position = Vector3.Lerp(
            transform.position, 
            targetPos, 
            Time.deltaTime * m_FollowSpeed
        );
        
        // Lock rotation to camera (with pitch/roll locks)
        Quaternion targetRot = m_Camera.transform.rotation;
        if (m_LockPitch) targetRot = Quaternion.Euler(0, targetRot.eulerAngles.y, targetRot.eulerAngles.z);
        if (m_LockRoll) targetRot = Quaternion.Euler(targetRot.eulerAngles.x, targetRot.eulerAngles.y, 0);
        
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * m_FollowSpeed
        );
    }
}
```

### 3. Block Indicators

GameplayHUD creates visual indicators for each block in the reference structure:

```csharp
private void CreateBlockIndicators()
{
    if (m_ReferenceSpawner?.CurrentSpawnConfiguration == null) return;
    
    foreach (BlockSpawnEntry entry in m_ReferenceSpawner.CurrentSpawnConfiguration.SpawnEntries)
    {
        // Create indicator GameObject
        GameObject indicator = CreateIndicator(entry);
        
        // Store reference
        m_BlockIndicators.Add(new BlockIndicator
        {
            Container = indicator,
            BlockType = entry.BlockType,
            BlockColor = entry.BlockColor,
            IsActive = false
        });
    }
}

private void UpdateIndicators(BuildValidationResult result)
{
    foreach (var indicator in m_BlockIndicators)
    {
        // Find matching validation result
        BlockValidationResult blockResult = result.BlockResults
            .FirstOrDefault(r => r.BlockType == indicator.BlockType && 
                                 r.BlockColor == indicator.BlockColor);
        
        if (blockResult != null)
        {
            // Update color based on correctness
            if (blockResult.IsCorrect)
                SetIndicatorColor(indicator, m_CorrectColor);
            else if (blockResult.IsPresent)
                SetIndicatorColor(indicator, m_WrongPositionColor);
            else
                SetIndicatorColor(indicator, m_MissingColor);
        }
    }
}
```

## Testing

### Manual Testing
1. Place blocks in build zone
2. Verify ValidationHUD updates in real-time
3. Check GameplayHUD block indicators light up correctly
4. Test camera following (move head in VR)
5. Verify ShelfProgressUI updates when blocks returned

### Debug
- Check `AutoUpdate` property to enable/disable updates
- Adjust `m_UpdateInterval` to change update frequency
- Use `m_FollowCamera` to toggle camera following

## Key Technical Decisions

### Why Camera Following?
**Problem:** Fixed UI positions are hard to see in VR when player moves.

**Solution:** UI elements follow camera with smooth interpolation. This keeps UI visible and comfortable without being too distracting. Optional pitch/roll locks prevent UI from tilting with head movement.

### Why Periodic Updates?
**Problem:** Updating every frame is expensive and unnecessary.

**Solution:** Update at intervals (0.3s default). This balances responsiveness with performance. Validation itself may be expensive, so batching updates prevents frame drops.

### Why Color-Coded Feedback?
**Problem:** Players need quick visual feedback on build progress.

**Solution:** Use color coding (green=correct, yellow=partial, red=incorrect, gray=missing). This provides instant visual feedback without reading numbers.

## Known Limitations

1. **Update Frequency:** Periodic updates may feel laggy for very fast builders
2. **Camera Following:** Smooth following may cause motion sickness for some users
3. **Text Readability:** Small text may be hard to read in VR
4. **Performance:** Multiple HUDs updating simultaneously can impact performance

## Configuration

### Recommended Settings

**ValidationHUD:**
- `m_UpdateInterval`: 0.3s (good balance)
- `m_FollowCamera`: true (for VR comfort)
- `m_DistanceFromCamera`: 2m
- `m_HeightOffset`: 0.3m

**GameplayHUD:**
- `m_UpdateInterval`: 0.3s
- `m_ProgressAnimSpeed`: 5 (smooth animation)
- `m_IndicatorSize`: 60px (readable in VR)

**ShelfProgressUI:**
- Update on events (no polling needed)

## Scene Setup

### ValidationHUD Setup
1. Create UI Canvas (World Space)
2. Add `ValidationHUD` component
3. Assign `BuildValidator` reference
4. Assign UI Text elements (accuracy, blocks, missing, extra)
5. Configure camera following settings

### GameplayHUD Setup
1. Create UI Canvas (World Space)
2. Add `GameplayHUD` component
3. Assign references (BuildValidator, ReferenceSpawner, LevelManager)
4. Create block indicator container (RectTransform)
5. Create progress bar (RectTransform with fill image)
6. Assign TextMeshPro elements (percentage, level, timer)

### ShelfProgressUI Setup
1. Create UI Canvas (World Space or Screen Space)
2. Add `ShelfProgressUI` component
3. Assign `ShelfBlockSpawner` reference
4. Create door status indicators
5. Create block count text

## Related Documentation

- [Main Scripts Overview](../README.md)
- [Validation System](../Validation/README.md) - How validation results are generated
- [Validation HUD API](../../../Docs/validation_hud_api.md)
