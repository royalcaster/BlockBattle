# Level2Structure Validation Issue - Debugging Guide

## Problem
Level2Structure has 1 yellow cube at position (0, 0, 0), but it's not being detected as valid (0% accuracy).

## Root Cause Analysis

### The Issue: Height Offset Mismatch

When you have a single block in the structure file:

1. **Reference Center Calculation**: With only 1 block at `Position: {x: 0, y: 0, z: 0}`, the reference center is also `(0, 0, 0)`.

2. **Relative Position**: 
   - Relative position = `(0, 0, 0) - (0, 0, 0) = (0, 0, 0)`

3. **Height Offset Applied**:
   - Expected relative position = `(0, 0, 0) + (0, 0.12, 0) = (0, 0.12, 0)`
   - Height offset default is **0.12m** (12cm)

4. **Expected World Position**:
   - Expected = `BuildZoneCenter + (0, 0.12, 0)`

5. **Actual Block Position**:
   - If block sits on table surface, its center is at `BuildZoneCenter.y + 0.05` (half cube height)
   - Actual = `BuildZoneCenter + (0, 0.05, 0)`

6. **Position Error**:
   - Error = `|0.12 - 0.05| = 0.07m = 7cm`
   - If position tolerance is 10cm, this should work... **BUT** if tolerance is smaller or there's rounding, it might fail.

## Solutions

### Solution 1: Adjust Y Position in Structure File (Recommended)

Set the Y position to account for the height offset:

**Current:**
```yaml
Position: {x: 0, y: 0, z: 0}
```

**Fixed:**
```yaml
Position: {x: 0, y: -0.07, z: 0}
```

This compensates for the height offset:
- Reference center: `(0, -0.07, 0)`
- Relative position: `(0, 0, 0) - (0, -0.07, 0) = (0, 0.07, 0)`
- With height offset: `(0, 0.07, 0) + (0, 0.12, 0) = (0, 0.19, 0)` ❌ **Wait, this is wrong!**

Actually, let me recalculate:

If block is at Y = -0.07 in structure file:
- Reference center = -0.07
- Relative position = 0 - (-0.07) = 0.07
- With height offset = 0.07 + 0.12 = 0.19 ❌ Still wrong

**Better approach**: Set Y to match where the block actually sits:

If block center is at table + 0.05m, and build zone center is at table level:
- We want: Expected Y = BuildZoneCenter.y + 0.05
- Currently: Expected Y = BuildZoneCenter.y + 0.12 (from height offset)
- So we need: Relative Y + 0.12 = 0.05
- Therefore: Relative Y = -0.07
- So structure file Y should be: ReferenceCenter.y - 0.07

But with single block, reference center = block Y, so:
- Block Y in file = -0.07

**Actually, simplest fix**: Set Y = -0.07 in structure file.

### Solution 2: Adjust Height Offset in BuildValidator

If you want to keep Y = 0 in structure file:

1. Select your `BuildValidator` GameObject
2. In Inspector, find **Height Offset**
3. Change from `0.12` to `0.05` (or whatever matches your table setup)

**Note**: This affects ALL structures, so only do this if all your structures need the same adjustment.

### Solution 3: Check Position Tolerance

1. Select `BuildValidator` GameObject
2. Check **Position Tolerance** value
3. If it's less than 0.07m (7cm), increase it to at least 0.10m (10cm)

## Debugging Steps

### Step 1: Check Console Logs

Look for these debug messages when you place the block:

```
=== BUILD VALIDATION ===
Placed blocks: 1, Reference blocks: 1
Build center (zone): (x, y, z)
Reference center (config): (0, 0, 0)
--- Expected positions (relative to zone center, with height offset 0.12m) ---
  Cube (Yellow): RelPos=(0, 0.12, 0), WorldPos=(x, y+0.12, z)
```

### Step 2: Check Actual Block Position

1. Select the placed yellow cube in the scene
2. Check its Transform position
3. Compare Y coordinate to expected Y from console logs

### Step 3: Check Block Color Detection

Verify the block color is detected correctly:

1. The structure file has `BlockColor: 3` (Yellow)
2. Check console for: `GetBlockColor` logs
3. Ensure your yellow cube has the correct material assigned

### Step 4: Check Build Zone

1. Select `BuildZone` GameObject
2. Verify the block is actually inside the zone
3. Check `BuildZone.transform.position` matches what's logged

## Quick Fix Checklist

- [ ] **Structure File**: Set Y position to `-0.07` (or adjust based on your setup)
- [ ] **BuildValidator**: Check Height Offset is appropriate (default 0.12m)
- [ ] **BuildValidator**: Check Position Tolerance is at least 0.10m
- [ ] **Block Color**: Verify yellow cube has `BlockMaterial_Yellow` material
- [ ] **Build Zone**: Ensure block is inside the green zone
- [ ] **Console**: Check debug logs for position errors

## Expected Values for Single Block at Center

If your structure file has:
```yaml
Position: {x: 0, y: 0, z: 0}
```

**And** your block sits on the table with center at table + 0.05m:

**Option A - Adjust Structure File:**
```yaml
Position: {x: 0, y: -0.07, z: 0}  # Compensates for height offset
```

**Option B - Adjust Height Offset:**
- Set Height Offset to `0.05` in BuildValidator

**Option C - Match Level1Structure:**
- Look at Level1Structure - the yellow cube has `y: 0.15`
- This works because other blocks define a different reference center
- For single block, you need Y = -0.07 or adjust height offset

## Testing

After making changes:

1. Place yellow cube in build zone
2. Check console for validation logs
3. Verify accuracy percentage increases
4. Check HUD shows correct block as "present"


