# Philip Poplutz - BlockBattle Project Diary

## Overview

Main contributions focused on environment design, level creation, UI polish, bug fixes, and gameplay refinement. Worked on making the game visually appealing and ensuring smooth gameplay experience.

## Development Log

### January 2026 - Environment & Assets
**Status**: Implemented

**What was done:**
- Added player area environment with furniture and props
- Integrated assets from Furniture Mega Pack
- Created hangar environment with appropriate colliders
- Adjusted car size for proper scale in VR
- Added environmental assets to enhance immersion

**Problems & Solutions:**
- **Problem**: Environment needed to feel immersive but not distract from gameplay
- **Solution**: Added furniture and props around play area while keeping main focus on build zone and shelf. Used appropriate scale for VR comfort.

- **Problem**: Hangar collider was interfering with gameplay
- **Solution**: Adjusted collider boundaries to prevent blocks from getting stuck while maintaining environmental boundaries.

**Decisions:**
- Used existing asset packs (Furniture Mega Pack) for rapid environment creation
- Kept environment minimal around play area to maintain focus
- Ensured all environmental objects have appropriate scale for VR

**References**: 
- Environment assets in `Assets/Furniture Mega Pack/`
- Hangar assets in `Assets/Old Hangar/`

---

### January 2026 - Level 3 Creation
**Status**: Implemented

**What was done:**
- Created Level 3 structure configuration
- Designed block layout for third level
- Tested and verified level progression through all three levels

**Problems & Solutions:**
- **Problem**: Level 3 needed to be challenging but not frustrating
- **Solution**: Designed structure with moderate complexity, building on lessons from levels 1 and 2. Ensured proper block placement and rotation requirements.

**Decisions:**
- Level 3 uses more complex structure than previous levels
- Maintained consistency with level 1 and 2 in terms of block types and colors used

**References**: 
- Level configurations in `Assets/BlockBattle/Structures/`
- [Block System Documentation](../Assets/BlockBattle/Scripts/Blocks/README.md)

---

### January 2026 - UI Fixes & Polish
**Status**: Implemented

**What was done:**
- Fixed UI timing issues (time display after game finish)
- Improved UI visibility and readability
- Fixed block movement restrictions before game start
- Ensured UI elements display correctly at game completion

**Problems & Solutions:**
- **Problem**: UI was showing incorrect information after game completion
- **Solution**: Fixed timer display logic to show final time correctly. Ensured UI updates properly at game end.

- **Problem**: Players could move blocks before game officially started
- **Solution**: Added block movement restrictions that are lifted only when game starts. Prevents accidental block manipulation during setup.

**Decisions:**
- UI should remain visible and informative throughout entire game flow
- Block interaction should be disabled until game officially starts

**References**: 
- [UI System Documentation](../Assets/BlockBattle/Scripts/UI/README.md)

---

### January 2026 - Shelf System Fixes
**Status**: Implemented

**What was done:**
- Fixed shelf door behavior (doors now closed at start)
- Fixed block spawning in shelf (2 rows of blocks)
- Inserted holes in shelf for proper block storage
- Adjusted shelf logic for reliable block ejection

**Problems & Solutions:**
- **Problem**: Shelf doors were opening at game start, causing premature block ejection
- **Solution**: Ensured doors start in closed position and only open when player interacts. Fixed door initialization logic.

- **Problem**: Blocks were spawning in single row, causing overlap and physics issues
- **Solution**: Implemented 2-row spawning system. Blocks now spawn in organized rows within shelf interior, preventing overlap.

- **Problem**: Shelf needed holes for blocks to properly enter storage area
- **Solution**: Added collider holes in shelf geometry. Blocks can now properly enter shelf interior for storage tracking.

**Decisions:**
- Shelf doors should always start closed for consistent gameplay
- Two-row block spawning provides better organization and prevents physics conflicts
- Shelf geometry must allow blocks to enter storage area for return tracking

**References**: 
- [Shelf System Documentation](../Assets/BlockBattle/Scripts/Shelf/README.md)
- [Shelf System Documentation](shelf_system.md)

---

### January 2026 - Destruction Phase Fixes
**Status**: Implemented

**What was done:**
- Fixed teleportation to slingshot position
- Disabled block movement during shooting mode
- Ensured destruction phase transitions work correctly

**Problems & Solutions:**
- **Problem**: Player wasn't teleporting to correct slingshot position
- **Solution**: Fixed teleport position reference and ensured player is correctly positioned for shooting. Verified teleport works reliably.

- **Problem**: Players could still move blocks during destruction phase, breaking gameplay flow
- **Solution**: Disabled block interaction (XRGrabInteractable) during destruction phase. Blocks become static targets for slingshot.

**Decisions:**
- Destruction phase should have clear boundaries (no building during destruction)
- Teleportation should be smooth and reliable for good UX

**References**: 
- [Destruction System Documentation](../Assets/BlockBattle/Scripts/Destruction/README.md)
- [Game Flow Documentation](../Assets/BlockBattle/Scripts/GameFlow/README.md)

---

### January 2026 - Audio System
**Status**: Implemented

**What was done:**
- Added sound effects when finishing building phase
- Added sound effects when completing game
- Integrated audio feedback for key gameplay moments

**Problems & Solutions:**
- **Problem**: Game needed audio feedback for completion events
- **Solution**: Added sound effects that play when building phase completes and when game finishes. Provides satisfying audio feedback for player achievements.

**Decisions:**
- Audio should enhance gameplay without being distracting
- Sound effects should play at key moments (phase completion, game end)

**References**: 
- Audio assets in `Assets/BlockBattle/Resources/`

---

### January 2026 - Door Mechanics Refinement
**Status**: Implemented

**What was done:**
- Fixed door kick force for better haptic feedback
- Adjusted door motor behavior
- Ensured doors respond correctly to player interaction

**Problems & Solutions:**
- **Problem**: Door kick force wasn't providing satisfying haptic feedback
- **Solution**: Adjusted `m_DoorKickForce` value to provide better impulse when blocks eject. Doors now feel more responsive and physical.

**Decisions:**
- Door mechanics should feel responsive and provide good haptic feedback
- Motor behavior should assist player without feeling automatic

**References**: 
- [Shelf System Documentation](../Assets/BlockBattle/Scripts/Shelf/README.md)

---

## Key Technical Achievements

1. **Environment Design**: Created immersive VR environment that enhances gameplay without distraction
2. **Level Creation**: Designed and implemented Level 3 with appropriate complexity progression
3. **UI Polish**: Fixed timing and display issues to ensure smooth user experience
4. **Gameplay Refinement**: Fixed numerous bugs and edge cases to make gameplay smooth and reliable

## Lessons Learned

1. **Environment Scale**: VR environments require careful attention to scale for comfort and immersion
2. **UI Timing**: UI updates must be carefully synchronized with game state changes
3. **Physics Interactions**: Shelf and door mechanics require careful tuning for good feel
4. **Block Organization**: Proper block spawning organization prevents physics conflicts

## Known Issues & Future Work

- Some environmental assets could be optimized for better performance
- Audio system could be expanded with more sound effects
- Level 3 could be further refined based on playtesting feedback
- Door mechanics could benefit from additional haptic feedback options
