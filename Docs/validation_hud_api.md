# Validation HUD API Documentation

## Overview

The `ValidationHUD` component displays build validation results in real-time as you build. It shows overall accuracy, per-block results, missing blocks, and extra blocks with live updates. The HUD appears at the top of the screen like a typical game HUD.

## Setup

### Quick Setup (Recommended)

1. Open Unity Editor
2. Go to menu: **BlockBattle → Setup Validation HUD**
3. Click **"Create Validation HUD"** button
4. The HUD will be automatically created and configured

The editor script will:
- Create a Screen Space Overlay Canvas (2D HUD at top of screen)
- Set up all UI text elements with larger, readable fonts
- Wire everything to the `ValidationHUD` component
- Auto-find and assign `BuildValidator` if one exists in the scene
- Auto-find and assign `ReferenceStructureSpawner` for automatic configuration sync

### Manual Setup

If you prefer manual setup:

1. Create a Canvas with `RenderMode = WorldSpace`
2. Create TextMeshProUGUI elements for:
   - Accuracy percentage
   - Correct blocks count
   - Block results list
   - Missing blocks list
   - Extra blocks list
3. Add `ValidationHUD` component to the Canvas
4. Assign all text references in the inspector
5. Assign a `BuildValidator` reference

## Component Properties

### References
- **Build Validator**: The `BuildValidator` component to get validation results from
- **Reference Spawner**: Optional `ReferenceStructureSpawner` to auto-sync the reference configuration
- **Accuracy Text**: TextMeshProUGUI for overall accuracy percentage
- **Correct Blocks Text**: TextMeshProUGUI for correct blocks count
- **Block Results Text**: TextMeshProUGUI for per-block validation results
- **Missing Blocks Text**: TextMeshProUGUI for missing blocks list
- **Extra Blocks Text**: TextMeshProUGUI for extra blocks list

### Auto-Sync Feature

When a `ReferenceStructureSpawner` is assigned:
- The HUD automatically syncs the reference configuration when a structure is spawned
- No manual assignment of `ReferenceConfiguration` to `BuildValidator` is needed
- The configuration updates whenever `ReferenceStructureSpawner.SpawnStructure()` is called

### Update Settings
- **Update Interval**: How often to update the HUD (default: 0.5 seconds)
- **Auto Update**: Whether to update automatically (default: true)

### Display Settings
- **Max Detailed Blocks**: Maximum number of blocks to show in detail (default: 10)
- **Correct Color**: Color for correct blocks (default: green)
- **Incorrect Color**: Color for incorrect blocks (default: red)
- **Missing Color**: Color for missing blocks (default: yellow)

### Camera Following
- **Follow Camera**: Whether HUD follows camera (default: true)
- **Follow Speed**: Speed of camera following (default: 5)
- **Lock Pitch**: Keep HUD level horizontally (default: true)
- **Lock Roll**: Keep HUD level (default: true)
- **Distance From Camera**: Distance in forward direction (default: 2m)
- **Height Offset**: Height offset from camera center (default: 0.3m)

## Usage

### Basic Usage

Once set up, the HUD will automatically update as you build:

```csharp
// The HUD updates automatically every UpdateInterval seconds
// No code needed - just assign BuildValidator reference
```

### Manual Updates

You can also trigger manual updates:

```csharp
ValidationHUD hud = GetComponent<ValidationHUD>();
hud.UpdateHUD(); // Force immediate update
```

### Programmatic Control

```csharp
// Enable/disable auto-updates
hud.AutoUpdate = false;

// Change update interval
hud.UpdateInterval = 1.0f; // Update every second

// Change BuildValidator reference
hud.BuildValidator = newValidator;
```

## Display Format

### Accuracy Display
- Shows overall accuracy percentage (0-100%)
- Color-coded:
  - Green: ≥90%
  - Yellow: 70-89%
  - Orange: 50-69%
  - Red: <50%

### Correct Blocks
- Shows count: "Correct: X/Y"
- X = correctly placed blocks
- Y = total reference blocks

### Block Results
- Shows detailed per-block results
- Format: `✓/✗ BlockType (Color) - Correct/Incorrect`
- For incorrect blocks, shows position and rotation errors
- Limited to `MaxDetailedBlocks` entries (shows "... and X more" if exceeded)

### Missing Blocks
- Lists blocks from reference structure that weren't placed
- Format: `• BlockType (Color)`
- Yellow color for visibility

### Extra Blocks
- Lists blocks placed that don't match any reference block
- Format: `• BlockType (Color)`

## Integration Example

```csharp
using BlockBattle;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private BuildValidator m_BuildValidator;
    [SerializeField] private ValidationHUD m_ValidationHUD;
    [SerializeField] private BlockSpawnConfiguration m_ReferenceStructure;

    void Start()
    {
        // Set reference structure
        m_BuildValidator.ReferenceConfiguration = m_ReferenceStructure;
        
        // HUD will automatically update as player builds
        // No additional code needed!
    }

    void OnBuzzerPressed()
    {
        // HUD already shows current state
        // Can also get final result
        BuildValidationResult result = m_BuildValidator.ValidateBuild();
        Debug.Log($"Final Accuracy: {result.AccuracyPercentage}%");
    }
}
```

## Performance Notes

- Updates run every `UpdateInterval` seconds (default 0.5s)
- Validation is performed on each update
- For better performance, increase `UpdateInterval` if needed
- Consider disabling `AutoUpdate` and calling `UpdateHUD()` manually on events

## Customization

### Changing Colors

```csharp
ValidationHUD hud = GetComponent<ValidationHUD>();
hud.CorrectColor = Color.cyan;
hud.IncorrectColor = Color.magenta;
hud.MissingColor = Color.orange;
```

### Adjusting Display Limits

```csharp
// Show more detailed blocks
hud.MaxDetailedBlocks = 20;
```

### Camera Following

```csharp
// Disable camera following (for fixed position HUD)
hud.FollowCamera = false;

// Adjust follow speed
hud.FollowSpeed = 10f; // Faster following

// Adjust position
hud.DistanceFromCamera = 3f; // Further away
hud.HeightOffset = 0.5f; // Higher up
```

## Troubleshooting

### HUD Not Updating
- Check that `BuildValidator` is assigned
- Check that `BuildValidator.ReferenceConfiguration` is set
- Verify `AutoUpdate` is enabled
- Check console for warnings

### HUD Not Visible
- Check Canvas `RenderMode` is set to `WorldSpace`
- Verify Canvas scale is appropriate (usually 0.001 for world space)
- Check camera following settings
- Ensure Canvas is in front of camera

### Performance Issues
- Increase `UpdateInterval` to reduce update frequency
- Reduce `MaxDetailedBlocks` to show fewer details
- Disable `AutoUpdate` and update manually on events

