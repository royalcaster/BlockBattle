# BlockBattle Development Session Summary
**Date:** December 4, 2025  
**Focus:** Phase 1 & 2 Implementation, Build Issues, and Material Rendering

## Session Overview

This session focused on implementing Phase 1 and Phase 2 of the BlockBattle project, setting up the VR environment with XR Interaction Toolkit, creating grabbable block prefabs, and resolving critical build and rendering issues that prevented blocks from appearing in headset builds.

---

## Major Issues Solved

### 1. Blocks Not Visible in Headset Build (Editor vs. Build Discrepancy)

**Problem:**  
Blocks were visible in the Unity editor when testing with the XR Device Simulator, but completely invisible when building and running on the Quest headset. Only shadows were visible.

**Root Cause:**  
The `BlockBattleTestSetup.cs` script that spawned blocks was an **editor-only script** (located in `Assets/Editor/`). Editor scripts do not execute in builds, so blocks were never instantiated at runtime.

**Solution:**  
- Created a new runtime script `BlockSpawner.cs` in `Assets/BlockBattle/Scripts/` that spawns blocks at game start
- Created an editor helper script `BlockBattleBlockSpawnerSetup.cs` to automatically configure the `BlockSpawner` component in the scene
- The `BlockSpawner` uses `Start()` to spawn blocks, ensuring they appear in both editor and builds

**Key Learning:**  
- **Editor scripts (`Assets/Editor/`) do not run in builds** - only runtime scripts are included
- Always use runtime scripts for functionality that must work in builds
- Editor scripts should only be used for setup/configuration that happens in the Unity editor

**Files Created:**
- `Assets/BlockBattle/Scripts/BlockSpawner.cs`
- `Assets/Editor/BlockBattleBlockSpawnerSetup.cs`

---

### 2. Material Not Rendering in Builds (URP Shader Mismatch)

**Problem:**  
After fixing the spawner issue, blocks were still not visible in the headset build. The material appeared to be missing or not rendering.

**Root Cause:**  
The `BlockMaterial` was using the **Standard shader** (Built-in Render Pipeline), but the project is configured to use **Universal Render Pipeline (URP)**. The Standard shader does not render in URP projects, causing materials to appear invisible or render incorrectly.

**Solution:**  
- Updated `BlockBattleBlockCreator.cs` to use URP shaders when creating materials:
  - Changed from `Shader.Find("Standard")` to `Shader.Find("Universal Render Pipeline/Lit")`
  - Updated material property names:
    - `_Color` → `_BaseColor`
    - `_Glossiness` → `_Smoothness`
  - Created `BlockBattleFixBlockMaterials.cs` to convert existing Standard materials to URP
- Ensured material references use `sharedMaterial` instead of `material` to properly save asset references in prefabs

**Key Learning:**  
- **Always check the render pipeline** before creating materials
- URP projects require URP-compatible shaders
- Standard shader properties differ from URP shader properties:
  - Standard: `_Color`, `_Glossiness`
  - URP Lit: `_BaseColor`, `_Smoothness`
- Use `sharedMaterial` when assigning materials in editor scripts to ensure proper asset references

**Files Modified:**
- `Assets/Editor/BlockBattleBlockCreator.cs`
- `Assets/Editor/BlockBattleFixBlockMaterials.cs` (new)

---

### 3. Material Culling Issue (Faces Not Rendering Correctly)

**Problem:**  
After fixing the URP shader issue, blocks were visible but rendered incorrectly - all sides were visible except the face the user was looking at, creating a "hollow" appearance.

**Root Cause:**  
The material's culling mode was potentially set incorrectly, or mesh normals were flipped, causing front-face culling instead of back-face culling.

**Solution:**  
- Created `BlockBattleFixMaterialCulling.cs` to explicitly set material culling to Back (value 2)
- Created `BlockBattleFixMeshNormals.cs` to recalculate mesh normals if they're flipped
- Updated material creation code to explicitly set `_Cull` to 2 (Back-face culling)

**Key Learning:**  
- **Culling modes in Unity:**
  - `0` = Off (Double-sided, no culling)
  - `1` = Front (Culls faces facing the camera - wrong for normal objects)
  - `2` = Back (Culls faces facing away from camera - correct for normal objects)
- URP materials use the `_Cull` property to control face culling
- Mesh normals must point outward for proper rendering
- `RecalculateNormals()` should be called after creating meshes programmatically

**Files Created:**
- `Assets/Editor/BlockBattleFixMaterialCulling.cs`
- `Assets/Editor/BlockBattleFixMeshNormals.cs`

---

## Technical Learnings

### Unity Editor vs. Runtime Scripts

**Editor Scripts (`Assets/Editor/`):**
- Only execute in the Unity Editor
- Useful for: setup automation, asset creation, scene configuration
- **Never included in builds**
- Use `[MenuItem]` for menu items
- Use `EditorUtility`, `AssetDatabase`, `PrefabUtility` for editor operations

**Runtime Scripts:**
- Execute in both editor and builds
- Must be in folders outside `Assets/Editor/`
- Use standard Unity APIs (`GameObject`, `Transform`, `MonoBehaviour`, etc.)

### Universal Render Pipeline (URP) Materials

**URP Shader Properties:**
- Base Color: `_BaseColor` (not `_Color`)
- Smoothness: `_Smoothness` (not `_Glossiness`)
- Metallic: `_Metallic` (same as Standard)
- Culling: `_Cull` (0=Off, 1=Front, 2=Back)

**Finding URP Shaders:**
```csharp
Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
// Fallback:
Shader urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
```

**Checking Render Pipeline:**
- Check `ProjectSettings/GraphicsSettings.asset` for `m_CustomRenderPipeline`
- Look for `UniversalRenderPipeline` references

### Material Assignment Best Practices

**In Editor Scripts:**
```csharp
// Use sharedMaterial to assign asset references (not instances)
meshRenderer.sharedMaterial = materialAsset;
```

**In Runtime Scripts:**
```csharp
// Can use either, but sharedMaterial is more efficient
meshRenderer.sharedMaterial = materialAsset; // Shared across instances
meshRenderer.material = materialAsset; // Creates instance per object
```

### Mesh Creation and Normals

**When Creating Meshes Programmatically:**
1. Define vertices
2. Define triangles (winding order matters!)
3. Define UVs
4. **Always call:**
   - `mesh.RecalculateNormals()` - Calculates normals from triangle winding
   - `mesh.RecalculateBounds()` - Updates bounding box
   - `mesh.RecalculateTangents()` - Needed for proper lighting

**Triangle Winding Order:**
- Counter-clockwise (CCW) when viewed from outside = front face
- Clockwise (CW) = back face (will be culled if back-face culling is enabled)

### XR Development Best Practices

**Testing in Editor:**
- Use XR Device Simulator for quick testing
- Editor scripts can spawn objects for testing
- Remember: editor-only functionality won't work in builds!

**Testing in Build:**
- Always test on actual hardware
- Use runtime scripts for spawning objects
- Check logs for runtime errors
- Verify all assets are included in build

---

## Files Created/Modified

### New Runtime Scripts
- `Assets/BlockBattle/Scripts/BlockSpawner.cs` - Spawns blocks at runtime

### New Editor Scripts
- `Assets/Editor/BlockBattleBlockSpawnerSetup.cs` - Sets up BlockSpawner component
- `Assets/Editor/BlockBattleFixBlockMaterials.cs` - Converts Standard to URP materials
- `Assets/Editor/BlockBattleFixMaterialCulling.cs` - Fixes material culling mode
- `Assets/Editor/BlockBattleFixMeshNormals.cs` - Recalculates mesh normals

### Modified Scripts
- `Assets/Editor/BlockBattleBlockCreator.cs` - Updated to use URP shaders and `sharedMaterial`

---

## Workflow Improvements

### Setup Process
1. **Phase 1 Setup:** `BlockBattle > Setup Phase 1 Scene`
2. **Create Blocks:** `BlockBattle > Create Block Prefabs`
3. **Setup Spawner:** `BlockBattle > Setup Block Spawner`
4. **Fix Materials (if needed):** `BlockBattle > Fix Block Material References`
5. **Fix Culling (if needed):** `BlockBattle > Fix Material Culling`
6. **Fix Normals (if needed):** `BlockBattle > Fix Mesh Normals`

### Debugging Checklist
When objects don't appear in builds:
1. ✅ Check if spawning happens at runtime (not just in editor)
2. ✅ Verify material uses correct shader for render pipeline
3. ✅ Ensure material references are properly saved in prefabs
4. ✅ Check culling mode is set correctly
5. ✅ Verify mesh normals are correct
6. ✅ Check console logs for errors

---

## Remaining Tasks

- [ ] Test all block types (Cube, Cylinder, Triangle) in headset build
- [ ] Verify material culling fix resolves the rendering issue
- [ ] Consider creating additional block shapes (arch, etc.) as per Phase 2 plan
- [ ] Optimize block spawning if performance issues arise

---

## Key Takeaways

1. **Editor vs. Runtime:** Always distinguish between editor-only and runtime functionality
2. **Render Pipeline Awareness:** Check render pipeline before creating materials
3. **Asset References:** Use `sharedMaterial` in editor scripts for proper asset references
4. **Testing:** Test in both editor and builds - they can behave differently
5. **Debugging:** When objects don't appear, check: spawning, materials, shaders, culling, normals

---

## Next Steps

1. Run the material culling fix script and test in headset
2. If culling issue persists, run mesh normals fix
3. Continue with Phase 3 implementation once rendering is confirmed working
4. Document any additional issues encountered during testing



