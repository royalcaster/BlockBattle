# Destruction System

## Purpose

The Destruction System manages the destruction phase of BlockBattle where players use a VR slingshot to destroy their built structures. The system handles slingshot mechanics (pull-back-and-release), projectile physics, and detection of when all blocks have been cleared from the build zone.

## Core Classes

### VRSlingshot.cs
VR slingshot with pull-back-and-release mechanics. The slingshot frame is fixed in place, and players grab the pouch to pull back and aim.

**Key Features:**
- Pull-distance-based force calculation
- Visual rubber band rendering (LineRenderer)
- Audio feedback on fire
- Collision detection with ignore list

**Usage:**
```csharp
VRSlingshot slingshot = GetComponent<VRSlingshot>();
// Player grabs pouch and pulls back
// On release, projectile fires with force = pullDistance * m_LaunchForceMultiplier
```

### DestructionPhaseManager.cs
Manages the destruction phase lifecycle and detects completion.

**Key Features:**
- Periodically checks build zone for remaining blocks
- Teleports player to shooting position
- Fires completion event when all blocks cleared
- Disables block movement during destruction phase

**Usage:**
```csharp
DestructionPhaseManager manager = GetComponent<DestructionPhaseManager>();
manager.StartDestructionPhase(); // Teleports player, enables slingshot
// System automatically detects when all blocks cleared
manager.OnDestructionComplete += HandleDestructionComplete;
```

### BallProjectile.cs
Projectile component attached to slingshot projectiles.

**Key Features:**
- Trail renderer for visual feedback
- Collision handling
- Auto-destruction after timeout

## How It Works

### 1. Slingshot Pull Mechanics

The slingshot tracks pouch position when grabbed:

```csharp
private void OnPouchGrabbed(SelectEnterEventArgs args)
{
    _isPouchGrabbed = true;
    _currentInteractor = args.interactorObject;
}

private void Update()
{
    if (_isPouchGrabbed)
    {
        // Get current pouch position from interactor
        _currentPouchPosition = _currentInteractor.GetAttachTransform(_pouchInteractable).position;
        
        // Calculate pull distance from rest position
        _currentPullDistance = Vector3.Distance(
            _currentPouchPosition, 
            m_PouchRestPosition.position
        );
        
        // Clamp to max pull distance
        _currentPullDistance = Mathf.Min(_currentPullDistance, m_MaxPullDistance);
        
        // Update visual rubber bands
        UpdateRubberBands();
    }
}
```

### 2. Projectile Launch

On release, projectile is fired with force based on pull distance:

```csharp
private void FireProjectile()
{
    // Berechne Geschwindigkeit basierend auf Zugdistanz
    float launchSpeed = _savedPullDistance * m_LaunchForceMultiplier;

    // Erzeuge Projektil
    GameObject projectile = Instantiate(m_ProjectilePrefab, _savedLaunchPosition, Quaternion.identity);
    Rigidbody rb = projectile.GetComponent<Rigidbody>();

    // Wende Geschwindigkeit direkt an (präziser als AddForce)
    rb.linearVelocity = _savedLaunchDirection * launchSpeed;
    
    // Visuelles Feedback (Trail, Sound)
    SetupTrailRenderer(projectile);
    m_AudioSource.PlayOneShot(m_FireSound);
    
    OnProjectileFired?.Invoke(projectile);
}
```

### 3. Destruction Phase Detection

The system periodically checks for remaining blocks:

```csharp
private void Update()
{
    if (!_isActive) return;
    
    _checkTimer += Time.deltaTime;
    if (_checkTimer >= m_CheckInterval)
    {
        _checkTimer = 0f;
        CheckRemainingBlocks();
    }
}

private void CheckRemainingBlocks()
{
    // Find all blocks in build zone
    Collider[] colliders = Physics.OverlapBox(
        m_BuildZone.ZoneBounds.center,
        m_BuildZone.ZoneBounds.extents,
        m_BuildZone.transform.rotation
    );
    
    int blockCount = 0;
    foreach (Collider col in colliders)
    {
        if (col.GetComponent<BlockReference>() != null)
        {
            blockCount++;
        }
    }
    
    if (blockCount != _lastBlockCount)
    {
        _lastBlockCount = blockCount;
        OnBlockCountChanged?.Invoke(blockCount);
    }
    
    // Check for completion
    if (blockCount == 0 && !_completionPending)
    {
        _completionPending = true;
        _completionTimer = 0f;
    }
    
    if (_completionPending)
    {
        _completionTimer += Time.deltaTime;
        if (_completionTimer >= m_CompletionDelay)
        {
            CompleteDestructionPhase();
        }
    }
}
```

## Testing

### Manual Testing
1. Build a structure in build zone
2. Enter destruction phase (via LevelManager)
3. Grab slingshot pouch and pull back
4. Release to fire projectile
5. Verify force scales with pull distance
6. Destroy all blocks - phase should complete automatically

### Debug
- Check `DestructionPhaseManager.IsActive` to verify phase state
- Monitor `RemainingBlocks` property for block count
- Use `OnBlockCountChanged` event for real-time updates

## Key Technical Decisions

### Why Pull-Distance-Based Force?
**Problem:** Simple button press is not engaging in VR.

**Solution:** Calculate launch velocity from pull distance: `velocity = pullDistance * multiplier`. This gives players control over shot power and makes the mechanic more physical and satisfying.

### Why Direct Velocity Instead of AddForce?
**Problem:** `AddForce` can be inconsistent with varying frame rates.

**Solution:** Set `Rigidbody.linearVelocity` directly. This gives precise, frame-rate-independent control over projectile speed.

### Why Periodic Block Checking?
**Problem:** Continuous checking every frame is expensive.

**Solution:** Check at intervals (0.5s default) using timer. This balances responsiveness with performance. Use delay before completion to prevent false positives from physics settling.

## Known Limitations

1. **Pull Distance Limits:** Max pull distance (0.4m default) may feel restrictive
2. **Collision Detection:** Projectiles may tunnel through thin blocks at high speeds
3. **Performance:** Block checking uses `Physics.OverlapBox` which can be expensive with many colliders
4. **Teleportation:** Player teleport may be disorienting for some users

## Configuration

### Recommended Settings

**Default (Balanced):**
- `m_MaxPullDistance`: 0.4m
- `m_LaunchForceMultiplier`: 40 (gives 16 m/s at max pull)
- `m_MinPullDistance`: 0.05m (prevents accidental fires)
- `m_CheckInterval`: 0.5s
- `m_CompletionDelay`: 1.0s

**Easy Mode:**
- `m_LaunchForceMultiplier`: 60 (easier to destroy blocks)

**Hard Mode:**
- `m_LaunchForceMultiplier`: 25 (requires more precision)

## Scene Setup

### Slingshot Setup
1. Create slingshot frame (fixed GameObject)
2. Add left/right fork tips (Transform references)
3. Create pouch GameObject with `XRGrabInteractable`
4. Assign `m_PouchRestPosition` (between forks)
5. Add `LineRenderer` components for rubber bands
6. Assign projectile prefab

### Destruction Phase Manager Setup
1. Assign `VRSlingshot` reference
2. Assign `BuildZone` reference
3. Create shooting position Transform (where player teleports)
4. Configure check interval and completion delay

## Related Documentation

- [Main Scripts Overview](../README.md)
- [Game Flow System](../GameFlow/README.md) - How destruction phase integrates with level flow
- [VR Slingshot Implementation](../../../Docs/core_implementation.md)
