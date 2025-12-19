# Manual Arch Block Creation Guide

This guide shows you how to manually create an arch block in Unity using ProBuilder (recommended) or Blender.

---

## Method 1: Using Unity ProBuilder (Recommended - Easiest)

### Prerequisites
- Unity ProBuilder package installed (Window → Package Manager → Unity Registry → ProBuilder)

### Steps

1. **Create a new GameObject**
   - Right-click in Hierarchy → Create Empty
   - Name it `Block_Arch`

2. **Add Components**
   - Add `Rigidbody` component
   - Add `XRGrabInteractable` component (from XR Interaction Toolkit)
   - Add `BlockCollisionController` component

3. **Create Visuals Child**
   - Right-click `Block_Arch` → Create Empty
   - Name it `Visuals`
   - Reset its transform (right-click Transform → Reset)

4. **Create Arch with ProBuilder**
   - Select the `Visuals` GameObject
   - Open ProBuilder: Tools → ProBuilder → ProBuilder Window
   - In ProBuilder window, click **New Shape** → **Arch**
   - Set parameters:
     - **Radius**: 0.05 (5cm - half the arch width)
     - **Thickness**: 0.05 (5cm depth)
     - **Height**: 0.1 (10cm total height)
     - **Sides**: 16 (smoothness)
   - Click **Build**

5. **Position the Arch**
   - The arch will be created at origin
   - Move it so the bottom sits at Y = 0 (or adjust in ProBuilder)
   - The arch should span from Y = -0.05 to Y = 0.05 (centered)

6. **Add Material**
   - Select `Visuals` → Add Component → Mesh Renderer
   - Assign your block material (e.g., `BlockMaterial`)

7. **Create Collider Child**
   - Right-click `Block_Arch` → Create Empty
   - Name it `Collider`
   - Reset transform
   - Add Component → Mesh Collider
   - Set Mesh Collider:
     - **Mesh**: Drag the mesh from Visuals MeshFilter
     - **Convex**: ✅ Checked (required for physics)

8. **Configure XR Grab**
   - Select `Block_Arch`
   - In XRGrabInteractable component:
     - **Predicted Visuals Transform**: Drag `Visuals` GameObject here

9. **Save as Prefab**
   - Drag `Block_Arch` from Hierarchy to `Assets/BlockBattle/Prefabs/Blocks/`
   - Name it `Block_Arch.prefab`
   - Delete the instance from the scene

---

## Method 2: Using Blender (More Control)

### Steps

1. **Create Arch in Blender**
   - Open Blender
   - Delete default cube (X → Delete)
   - Add → Mesh → Arch
   - In bottom-left panel, set:
     - **Size**: 0.1 (10cm)
     - **Depth**: 0.05 (5cm)
     - **Resolution**: 16
     - **Start/End Angle**: 0° to 180° (semicircle)

2. **Adjust Dimensions**
   - Select the arch
   - Tab to Edit Mode
   - Scale to match:
     - Width (X): 0.1m (10cm)
     - Height (Y): 0.1m (10cm) 
     - Depth (Z): 0.05m (5cm)
   - Position so bottom is at Y = 0

3. **Export**
   - File → Export → FBX
   - Settings:
     - ✅ Apply Transform
     - ✅ Forward: -Z Forward, Y Up
     - Scale: 1.0
   - Save as `Arch.fbx`

4. **Import to Unity**
   - Drag `Arch.fbx` into `Assets/BlockBattle/Meshes/` or `Assets/BlockBattle/Prefabs/Blocks/`
   - In Import Settings:
     - **Scale Factor**: 1
     - ✅ Generate Colliders (or create manually)
     - ✅ Generate Lightmap UVs

5. **Create Prefab**
   - Follow steps 1-3 and 6-9 from Method 1
   - In step 4, instead of ProBuilder:
     - Add Component → Mesh Filter
     - Assign the imported `Arch` mesh
     - Add Component → Mesh Renderer
     - Assign material

---

## Method 3: Using Unity Primitives (Quick but Less Accurate)

### Steps

1. **Create Base Block**
   - Create Empty GameObject → `Block_Arch`
   - Add components (Rigidbody, XRGrabInteractable, etc.)

2. **Create Visuals**
   - Create Empty child → `Visuals`
   - Create Cube primitive (GameObject → 3D Object → Cube)
   - Name it `ArchBase`
   - Scale: (0.1, 0.05, 0.05) - 10cm wide, 5cm tall, 5cm deep
   - Position: (0, -0.025, 0) - bottom half

3. **Create Arch Top**
   - Create Cylinder primitive
   - Name it `ArchTop`
   - Scale: (0.1, 0.05, 0.1) - matches width
   - Position: (0, 0.025, 0) - top half
   - Rotation: (90, 0, 0) - rotate to horizontal

4. **Combine Meshes**
   - Select both `ArchBase` and `ArchTop`
   - Use ProBuilder: Tools → ProBuilder → ProBuilder Window → **Merge Objects**
   - Or use a mesh combiner script

5. **Create Collider**
   - Add Mesh Collider to `Collider` child
   - Assign combined mesh
   - ✅ Convex

6. **Continue with steps 6-9 from Method 1**

---

## Recommended Arch Dimensions

- **Total Width**: 0.1m (10cm)
- **Total Height**: 0.1m (10cm)
- **Depth**: 0.05m (5cm)
- **Arch Radius**: 0.05m (5cm) - spans full width
- **Arch Start Height**: Middle (0.05m from bottom)
- **Arch Shape**: Semicircle (0° to 180°)

---

## Troubleshooting

### Arch looks wrong
- Check that arch radius matches half the width
- Ensure arch starts at the middle height
- Verify mesh normals are correct (ProBuilder: Object → Normals → Recalculate)

### Collider issues
- Mesh Collider must be **Convex** for physics
- If convex fails, try increasing mesh resolution
- Alternative: Use multiple Box Colliders for pillars + Capsule Collider for arch

### Material not showing
- Ensure Mesh Renderer is on `Visuals` GameObject
- Check material shader (should be URP Lit)
- Verify material is assigned in Mesh Renderer component

### XR Grab not working
- Ensure `Predicted Visuals Transform` points to `Visuals` GameObject
- Check that `Visuals` has MeshRenderer component
- Verify XR Interaction Layer is set correctly

---

## Quick Reference: Arch Block Structure

```
Block_Arch (Root)
├── Visuals (MeshRenderer + MeshFilter)
│   └── [Arch Mesh]
└── Collider (MeshCollider)
    └── [Arch Mesh - Convex]
```

**Components on Root:**
- Rigidbody
- XRGrabInteractable
- BlockCollisionController

---

## After Creation

1. **Test in Scene**
   - Place arch in scene
   - Test grabbing with VR controllers
   - Verify physics (falls, stacks correctly)

2. **Assign to BlockSpawner**
   - Select your `BlockSpawner` GameObject
   - Drag `Block_Arch.prefab` to `Arch Block Prefab` field

3. **Use in Structures**
   - Create or edit `BlockSpawnConfiguration` asset
   - Add entry with `BlockType.Arch`
   - Set position, rotation, and color

---

## ProBuilder Arch Settings (Detailed)

When using ProBuilder Arch tool:

- **Radius**: 0.05 (half the total width - creates 10cm wide arch)
- **Thickness**: 0.05 (depth of the block)
- **Height**: 0.1 (total height including arch)
- **Sides**: 16-32 (more = smoother, but heavier)
- **Start Angle**: 0
- **End Angle**: 180 (semicircle)

**Positioning Tip**: After creating, move the arch so:
- Bottom of pillars: Y = -0.05
- Top of arch: Y = 0.05
- Center: X = 0, Z = 0

This ensures the arch sits correctly on the table surface.

