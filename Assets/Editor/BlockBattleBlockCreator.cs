using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.IO;

/// <summary>
/// Editor script to create grabbable block prefabs for BlockBattle Phase 2.
/// Creates blocks with Rigidbody, XRGrabInteractable, and proper collision detection.
/// </summary>
public static class BlockBattleBlockCreator
{
    private const string BlocksFolderPath = "Assets/BlockBattle/Prefabs/Blocks";
    private const string MaterialsFolderPath = "Assets/BlockBattle/Materials";
    private const string MeshesFolderPath = "Assets/BlockBattle/Meshes";
    
    /// <summary>
    /// Creates all block prefabs for BlockBattle.
    /// </summary>
    [MenuItem("BlockBattle/Create Block Prefabs")]
    public static void CreateAllBlocks()
    {
        // Ensure folders exist
        EnsureFolderStructure();
        
        // Create wooden material
        Material blockMaterial = CreateWoodenMaterial();
        
        // Create block prefabs
        CreateCubeBlock(blockMaterial);
        CreateCylinderBlock(blockMaterial);
        CreateTriangleBlock(blockMaterial);
        CreateRectangleBlock(blockMaterial);
        CreateArchBlock(blockMaterial);
        CreateBigTriangleBlock(blockMaterial);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("BlockBattle: All block prefabs created successfully!");
    }

    /// <summary>
    /// Force recreates only the Arch block prefab (useful for fixing arch issues).
    /// </summary>
    [MenuItem("BlockBattle/Recreate Arch Block Only")]
    public static void CreateArchBlockOnly()
    {
        // Delete existing arch prefab if it exists
        string prefabPath = $"{BlocksFolderPath}/Block_Arch.prefab";
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
            Debug.Log($"Deleted existing Block_Arch.prefab at {prefabPath}");
        }

        // Delete existing arch mesh if it exists
        string meshPath = $"{MeshesFolderPath}/ArchMesh.asset";
        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existingMesh != null)
        {
            AssetDatabase.DeleteAsset(meshPath);
            Debug.Log($"Deleted existing ArchMesh.asset at {meshPath}");
        }

        // Create wooden material
        Material blockMaterial = CreateWoodenMaterial();
        
        // Recreate arch block
        CreateArchBlock(blockMaterial);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("BlockBattle: Arch block recreated! Check the prefab in the scene to verify it looks correct.");
    }

    /// <summary>
    /// Creates all block prefabs for BlockBattle.
    /// </summary>
    [MenuItem("BlockBattle/Create All Block Prefabs")]
    public static void CreateAllBlocksFull()
    {
        // Ensure folders exist
        EnsureFolderStructure();
        
        // Create wooden material
        Material blockMaterial = CreateWoodenMaterial();
        
        // Create block prefabs
        CreateCubeBlock(blockMaterial);
        CreateCylinderBlock(blockMaterial);
        CreateTriangleBlock(blockMaterial);
        CreateRectangleBlock(blockMaterial);
        CreateArchBlock(blockMaterial);
        CreateBigTriangleBlock(blockMaterial);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("BlockBattle: All block prefabs created successfully!");
    }
    
    /// <summary>
    /// Ensures the folder structure exists for blocks and materials.
    /// </summary>
    private static void EnsureFolderStructure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/BlockBattle"))
        {
            AssetDatabase.CreateFolder("Assets", "BlockBattle");
        }
        
        if (!AssetDatabase.IsValidFolder("Assets/BlockBattle/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/BlockBattle", "Prefabs");
        }
        
        if (!AssetDatabase.IsValidFolder(BlocksFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/BlockBattle/Prefabs", "Blocks");
        }
        
        if (!AssetDatabase.IsValidFolder(MaterialsFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/BlockBattle", "Materials");
        }
        
        if (!AssetDatabase.IsValidFolder(MeshesFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/BlockBattle", "Meshes");
        }
        
        if (!AssetDatabase.IsValidFolder("Assets/BlockBattle/Scripts"))
        {
            AssetDatabase.CreateFolder("Assets/BlockBattle", "Scripts");
        }
    }
    
    /// <summary>
    /// Creates a wooden material for blocks.
    /// </summary>
    private static Material CreateWoodenMaterial()
    {
        string materialPath = $"{MaterialsFolderPath}/BlockMaterial.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        
        if (material == null)
        {
            // Use URP shader instead of Standard shader (URP doesn't support Standard shader)
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                // Fallback to Simple Lit if Lit is not available
                urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            
            if (urpShader == null)
            {
                Debug.LogError("Could not find URP shader! Please ensure Universal Render Pipeline is installed.");
                return null;
            }
            
            material = new Material(urpShader);
            material.name = "BlockMaterial";
            
            // Set color using URP property names (ensure alpha is 1.0 for solid opaque)
            material.SetColor("_BaseColor", new Color(0.6f, 0.4f, 0.2f, 1f)); // Brown wooden color, fully opaque
            material.SetFloat("_Smoothness", 0.3f); // Slightly glossy (URP uses _Smoothness instead of _Glossiness)
            material.SetFloat("_Metallic", 0f); // Non-metallic
            
            // Ensure solid opaque rendering properties
            material.SetFloat("_Surface", 0f); // 0 = Opaque, 1 = Transparent
            material.SetFloat("_Blend", 0f); // Alpha blend mode
            material.SetFloat("_SrcBlend", 1f); // One
            material.SetFloat("_DstBlend", 0f); // Zero
            material.SetFloat("_SrcBlendAlpha", 1f); // One
            material.SetFloat("_DstBlendAlpha", 0f); // Zero
            material.SetFloat("_ZWrite", 1f); // Enable Z-write for opaque
            material.SetFloat("_AlphaClip", 0f); // Disable alpha clipping
            material.SetFloat("_Cutoff", 0.5f); // Alpha cutoff (not used when AlphaClip is 0)
            
            // Ensure back-face culling is enabled (2 = Back, which culls faces facing away from camera)
            // 0 = Off (double-sided), 1 = Front, 2 = Back (default and correct)
            material.SetFloat("_Cull", 2f);
            
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            // Update existing material to use URP shader if it's still using Standard
            if (material.shader.name == "Standard" || material.shader.name.Contains("Standard"))
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader == null)
                {
                    urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                }
                
                if (urpShader != null)
                {
                    // Convert Standard material properties to URP
                    Color oldColor = material.color;
                    float oldGlossiness = material.GetFloat("_Glossiness");
                    float oldMetallic = material.GetFloat("_Metallic");
                    
                    material.shader = urpShader;
                    material.SetColor("_BaseColor", oldColor);
                    material.SetFloat("_Smoothness", oldGlossiness);
                    material.SetFloat("_Metallic", oldMetallic);
                    
                    EditorUtility.SetDirty(material);
                    Debug.Log("Updated BlockMaterial to use URP shader");
                }
            }
        }
        
        return material;
    }
    
    /// <summary>
    /// Creates a cube block prefab.
    /// </summary>
    private static void CreateCubeBlock(Material material)
    {
        string prefabPath = $"{BlocksFolderPath}/Block_Cube.prefab";
        
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.LogWarning($"Block_Cube.prefab already exists at {prefabPath}. Skipping creation.");
            return;
        }
        
        // Create root GameObject
        GameObject block = new GameObject("Block_Cube");
        
        // Add components to root
        Rigidbody rigidbody = block.AddComponent<Rigidbody>();
        ConfigureRigidbody(rigidbody);
        
        XRGrabInteractable grabInteractable = block.AddComponent<XRGrabInteractable>();
        ConfigureGrabInteractable(grabInteractable);
        
        // Add collision controller for dynamic collision detection switching
        block.AddComponent<BlockCollisionController>();
        
        // Create Visuals child
        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(block.transform);
        visuals.transform.localPosition = Vector3.zero;
        visuals.transform.localRotation = Quaternion.identity;
        visuals.transform.localScale = Vector3.one;
        
        MeshFilter meshFilter = visuals.AddComponent<MeshFilter>();
        Mesh cubeMesh = GetOrCreateMesh("CubeMesh", () => CreateCubeMesh(0.1f)); // 10cm cube
        meshFilter.sharedMesh = cubeMesh;
        
        MeshRenderer meshRenderer = visuals.AddComponent<MeshRenderer>();
        // Use sharedMaterial to assign the material asset reference (not an instance)
        // This ensures the material reference is properly saved in the prefab
        meshRenderer.sharedMaterial = material;
        
        // Create Collider child
        GameObject colliderObj = new GameObject("Collider");
        colliderObj.transform.SetParent(block.transform);
        colliderObj.transform.localPosition = Vector3.zero;
        colliderObj.transform.localRotation = Quaternion.identity;
        colliderObj.transform.localScale = Vector3.one;
        
        BoxCollider boxCollider = colliderObj.AddComponent<BoxCollider>();
        boxCollider.size = Vector3.one * 0.1f;
        
        // Set predicted visuals transform for XR Grab
        grabInteractable.predictedVisualsTransform = visuals.transform;
        
        // Create prefab
        PrefabUtility.SaveAsPrefabAsset(block, prefabPath);
        Object.DestroyImmediate(block);
        
        Debug.Log($"Created Block_Cube.prefab at {prefabPath}");
    }
    
    /// <summary>
    /// Creates a cylinder block prefab.
    /// </summary>
    private static void CreateCylinderBlock(Material material)
    {
        string prefabPath = $"{BlocksFolderPath}/Block_Cylinder.prefab";
        
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.LogWarning($"Block_Cylinder.prefab already exists at {prefabPath}. Skipping creation.");
            return;
        }
        
        // Create root GameObject
        GameObject block = new GameObject("Block_Cylinder");
        
        // Add components to root
        Rigidbody rigidbody = block.AddComponent<Rigidbody>();
        ConfigureRigidbody(rigidbody);
        
        XRGrabInteractable grabInteractable = block.AddComponent<XRGrabInteractable>();
        ConfigureGrabInteractable(grabInteractable);
        
        // Add collision controller for dynamic collision detection switching
        block.AddComponent<BlockCollisionController>();
        
        // Create Visuals child
        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(block.transform);
        visuals.transform.localPosition = Vector3.zero;
        visuals.transform.localRotation = Quaternion.identity;
        visuals.transform.localScale = Vector3.one;
        
        MeshFilter meshFilter = visuals.AddComponent<MeshFilter>();
        Mesh cylinderMesh = GetOrCreateMesh("CylinderMesh", () => CreateCylinderMesh(0.05f, 0.1f)); // 5cm radius, 10cm height
        meshFilter.sharedMesh = cylinderMesh;
        
        MeshRenderer meshRenderer = visuals.AddComponent<MeshRenderer>();
        // Use sharedMaterial to assign the material asset reference (not an instance)
        // This ensures the material reference is properly saved in the prefab
        meshRenderer.sharedMaterial = material;
        
        // Create Collider child
        GameObject colliderObj = new GameObject("Collider");
        colliderObj.transform.SetParent(block.transform);
        colliderObj.transform.localPosition = Vector3.zero;
        colliderObj.transform.localRotation = Quaternion.identity;
        colliderObj.transform.localScale = Vector3.one;
        
        CapsuleCollider capsuleCollider = colliderObj.AddComponent<CapsuleCollider>();
        capsuleCollider.radius = 0.05f;
        capsuleCollider.height = 0.1f;
        capsuleCollider.direction = 1; // Y-axis
        
        // Set predicted visuals transform for XR Grab
        grabInteractable.predictedVisualsTransform = visuals.transform;
        
        // Create prefab
        PrefabUtility.SaveAsPrefabAsset(block, prefabPath);
        Object.DestroyImmediate(block);
        
        Debug.Log($"Created Block_Cylinder.prefab at {prefabPath}");
    }
    
    /// <summary>
    /// Creates a triangular prism block prefab.
    /// </summary>
    private static void CreateTriangleBlock(Material material)
    {
        string prefabPath = $"{BlocksFolderPath}/Block_Triangle.prefab";
        
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.LogWarning($"Block_Triangle.prefab already exists at {prefabPath}. Skipping creation.");
            return;
        }
        
        // Create root GameObject
        GameObject block = new GameObject("Block_Triangle");
        
        // Add components to root
        Rigidbody rigidbody = block.AddComponent<Rigidbody>();
        ConfigureRigidbody(rigidbody);
        
        XRGrabInteractable grabInteractable = block.AddComponent<XRGrabInteractable>();
        ConfigureGrabInteractable(grabInteractable);
        
        // Add collision controller for dynamic collision detection switching
        block.AddComponent<BlockCollisionController>();
        
        // Create Visuals child
        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(block.transform);
        visuals.transform.localPosition = Vector3.zero;
        visuals.transform.localRotation = Quaternion.identity;
        visuals.transform.localScale = Vector3.one;
        
        MeshFilter meshFilter = visuals.AddComponent<MeshFilter>();
        Mesh triangleMesh = GetOrCreateMesh("TriangleMesh", () => CreateTriangularPrismMesh(0.1f, 0.1f)); // 10cm base, 10cm height
        meshFilter.sharedMesh = triangleMesh;
        
        MeshRenderer meshRenderer = visuals.AddComponent<MeshRenderer>();
        // Use sharedMaterial to assign the material asset reference (not an instance)
        // This ensures the material reference is properly saved in the prefab
        meshRenderer.sharedMaterial = material;
        
        // Create Collider child - use MeshCollider for triangle
        GameObject colliderObj = new GameObject("Collider");
        colliderObj.transform.SetParent(block.transform);
        colliderObj.transform.localPosition = Vector3.zero;
        colliderObj.transform.localRotation = Quaternion.identity;
        colliderObj.transform.localScale = Vector3.one;
        
        MeshCollider meshCollider = colliderObj.AddComponent<MeshCollider>();
        // Reload mesh from asset path to ensure proper serialization in prefab
        string meshPath = $"{MeshesFolderPath}/TriangleMesh.asset";
        Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (meshAsset != null)
        {
            meshCollider.sharedMesh = meshAsset;
        }
        else
        {
            meshCollider.sharedMesh = triangleMesh; // Fallback to in-memory mesh
        }
        meshCollider.convex = true; // Required for physics
        
        // Set predicted visuals transform for XR Grab
        grabInteractable.predictedVisualsTransform = visuals.transform;
        
        // Create prefab
        PrefabUtility.SaveAsPrefabAsset(block, prefabPath);
        Object.DestroyImmediate(block);
        
        Debug.Log($"Created Block_Triangle.prefab at {prefabPath}");
    }
    
    /// <summary>
    /// Configures Rigidbody with realistic physics properties for wooden blocks.
    /// </summary>
    private static void ConfigureRigidbody(Rigidbody rigidbody)
    {
        rigidbody.mass = 0.5f; // 0.5 kg - realistic for small wooden blocks
        rigidbody.linearDamping = 0f; // No air resistance
        rigidbody.angularDamping = 0.05f; // Low angular drag for realistic rotation
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate; // Smooth movement
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // For stable stacking
    }
    
    /// <summary>
    /// Configures XRGrabInteractable for hand interaction.
    /// </summary>
    private static void ConfigureGrabInteractable(XRGrabInteractable grabInteractable)
    {
        grabInteractable.interactionLayers = 1; // Default interaction layer
        grabInteractable.selectMode = InteractableSelectMode.Multiple; // Allow two-handed grabs
        grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking; // Realistic physics
        grabInteractable.throwOnDetach = true; // Allow throwing
        grabInteractable.throwVelocityScale = 1.5f;
        grabInteractable.throwAngularVelocityScale = 1f;
        grabInteractable.useDynamicAttach = true;
        grabInteractable.matchAttachPosition = true;
        grabInteractable.matchAttachRotation = true;
    }
    
    /// <summary>
    /// Gets an existing mesh asset or creates a new one if it doesn't exist.
    /// </summary>
    private static Mesh GetOrCreateMesh(string meshName, System.Func<Mesh> createMesh)
    {
        string meshPath = $"{MeshesFolderPath}/{meshName}.asset";
        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        
        if (existingMesh != null)
        {
            return existingMesh;
        }
        
        Mesh newMesh = createMesh();
        newMesh.name = meshName;
        AssetDatabase.CreateAsset(newMesh, meshPath);
        AssetDatabase.SaveAssets();
        
        return newMesh;
    }
    
    /// <summary>
    /// Creates a cube mesh with the specified size.
    /// </summary>
    private static Mesh CreateCubeMesh(float size)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CubeMesh";
        
        float halfSize = size * 0.5f;
        
        Vector3[] vertices = new Vector3[]
        {
            // Front face
            new Vector3(-halfSize, -halfSize, halfSize),
            new Vector3(halfSize, -halfSize, halfSize),
            new Vector3(halfSize, halfSize, halfSize),
            new Vector3(-halfSize, halfSize, halfSize),
            // Back face
            new Vector3(-halfSize, -halfSize, -halfSize),
            new Vector3(-halfSize, halfSize, -halfSize),
            new Vector3(halfSize, halfSize, -halfSize),
            new Vector3(halfSize, -halfSize, -halfSize),
            // Top face
            new Vector3(-halfSize, halfSize, -halfSize),
            new Vector3(-halfSize, halfSize, halfSize),
            new Vector3(halfSize, halfSize, halfSize),
            new Vector3(halfSize, halfSize, -halfSize),
            // Bottom face
            new Vector3(-halfSize, -halfSize, -halfSize),
            new Vector3(halfSize, -halfSize, -halfSize),
            new Vector3(halfSize, -halfSize, halfSize),
            new Vector3(-halfSize, -halfSize, halfSize),
            // Right face
            new Vector3(halfSize, -halfSize, -halfSize),
            new Vector3(halfSize, halfSize, -halfSize),
            new Vector3(halfSize, halfSize, halfSize),
            new Vector3(halfSize, -halfSize, halfSize),
            // Left face
            new Vector3(-halfSize, -halfSize, -halfSize),
            new Vector3(-halfSize, -halfSize, halfSize),
            new Vector3(-halfSize, halfSize, halfSize),
            new Vector3(-halfSize, halfSize, -halfSize)
        };
        
        // Triangle indices with correct counter-clockwise winding (when viewed from outside)
        int[] triangles = new int[]
        {
            // Front face (viewed from +Z): bottom-left -> bottom-right -> top-right, then bottom-left -> top-right -> top-left
            0, 1, 2, 0, 2, 3,
            // Back face (viewed from -Z): bottom-left -> top-left -> top-right, then bottom-left -> top-right -> bottom-right
            4, 5, 6, 4, 6, 7,
            // Top face (viewed from +Y): bottom-left -> top-right -> top-left, then bottom-left -> bottom-right -> top-right
            8, 9, 10, 8, 10, 11,
            // Bottom face (viewed from -Y): bottom-left -> bottom-right -> top-right, then bottom-left -> top-right -> top-left
            12, 13, 14, 12, 14, 15,
            // Right face (viewed from +X): bottom-left -> top-left -> top-right, then bottom-left -> top-right -> bottom-right
            16, 17, 18, 16, 18, 19,
            // Left face (viewed from -X): bottom-left -> bottom-right -> top-right, then bottom-left -> top-right -> top-left
            20, 21, 22, 20, 22, 23
        };
        
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x + 0.5f, vertices[i].y + 0.5f);
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Creates a cylinder mesh with the specified radius and height.
    /// </summary>
    private static Mesh CreateCylinderMesh(float radius, float height)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CylinderMesh";
        
        int segments = 16;
        float halfHeight = height * 0.5f;
        
        Vector3[] vertices = new Vector3[segments * 2 + 2];
        int[] triangles = new int[segments * 12];
        
        // Top and bottom center vertices
        vertices[0] = new Vector3(0, halfHeight, 0);
        vertices[1] = new Vector3(0, -halfHeight, 0);
        
        // Create side vertices
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            
            vertices[i + 2] = new Vector3(x, halfHeight, z);
            vertices[i + segments + 2] = new Vector3(x, -halfHeight, z);
        }
        
        // Top face
        for (int i = 0; i < segments; i++)
        {
            int v1 = 0;
            int v2 = i + 2;
            int v3 = ((i + 1) % segments) + 2;
            
            triangles[i * 3] = v1;
            triangles[i * 3 + 1] = v3;
            triangles[i * 3 + 2] = v2;
        }
        
        // Bottom face
        int bottomOffset = segments * 3;
        for (int i = 0; i < segments; i++)
        {
            int v1 = 1;
            int v2 = ((i + 1) % segments) + segments + 2;
            int v3 = i + segments + 2;
            
            triangles[bottomOffset + i * 3] = v1;
            triangles[bottomOffset + i * 3 + 1] = v2;
            triangles[bottomOffset + i * 3 + 2] = v3;
        }
        
        // Side faces
        int sideOffset = segments * 6;
        for (int i = 0; i < segments; i++)
        {
            int v1 = i + 2;
            int v2 = ((i + 1) % segments) + 2;
            int v3 = i + segments + 2;
            int v4 = ((i + 1) % segments) + segments + 2;
            
            triangles[sideOffset + i * 6] = v1;
            triangles[sideOffset + i * 6 + 1] = v3;
            triangles[sideOffset + i * 6 + 2] = v2;
            triangles[sideOffset + i * 6 + 3] = v2;
            triangles[sideOffset + i * 6 + 4] = v3;
            triangles[sideOffset + i * 6 + 5] = v4;
        }
        
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x / radius * 0.5f + 0.5f, vertices[i].z / radius * 0.5f + 0.5f);
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Creates a triangular prism mesh (triangle base extruded along Y-axis).
    /// </summary>
    private static Mesh CreateTriangularPrismMesh(float baseSize, float height)
    {
        Mesh mesh = new Mesh();
        mesh.name = "TriangularPrismMesh";
        
        float halfHeight = height * 0.5f;
        float halfBase = baseSize * 0.5f;
        
        // Equilateral triangle base vertices
        Vector3[] vertices = new Vector3[]
        {
            // Bottom triangle
            new Vector3(0, -halfHeight, halfBase * 0.866f), // Top vertex
            new Vector3(-halfBase, -halfHeight, -halfBase * 0.433f), // Bottom left
            new Vector3(halfBase, -halfHeight, -halfBase * 0.433f), // Bottom right
            // Top triangle
            new Vector3(0, halfHeight, halfBase * 0.866f),
            new Vector3(-halfBase, halfHeight, -halfBase * 0.433f),
            new Vector3(halfBase, halfHeight, -halfBase * 0.433f)
        };
        
        // Triangle indices with correct counter-clockwise winding (when viewed from outside)
        int[] triangles = new int[]
        {
            // Bottom face (viewed from -Y): counter-clockwise
            0, 1, 2,
            // Top face (viewed from +Y): counter-clockwise
            3, 5, 4,
            // Left side (viewed from -X): counter-clockwise
            0, 3, 1, 1, 3, 4,
            // Back side (viewed from -Z): counter-clockwise
            1, 4, 2, 2, 4, 5,
            // Right side (viewed from +X): counter-clockwise
            2, 5, 0, 0, 5, 3
        };
        
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x / baseSize + 0.5f, vertices[i].y / height + 0.5f);
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }

    /// <summary>
    /// Creates a rectangular block prefab (long flat block).
    /// </summary>
    private static void CreateRectangleBlock(Material material)
    {
        string prefabPath = $"{BlocksFolderPath}/Block_Rectangle.prefab";
        
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.LogWarning($"Block_Rectangle.prefab already exists at {prefabPath}. Skipping creation.");
            return;
        }
        
        // Create root GameObject
        GameObject block = new GameObject("Block_Rectangle");
        
        // Add components to root
        Rigidbody rigidbody = block.AddComponent<Rigidbody>();
        ConfigureRigidbody(rigidbody);
        
        XRGrabInteractable grabInteractable = block.AddComponent<XRGrabInteractable>();
        ConfigureGrabInteractable(grabInteractable);
        
        // Add collision controller for dynamic collision detection switching
        block.AddComponent<BlockCollisionController>();
        
        // Create Visuals child
        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(block.transform);
        visuals.transform.localPosition = Vector3.zero;
        visuals.transform.localRotation = Quaternion.identity;
        visuals.transform.localScale = Vector3.one;
        
        MeshFilter meshFilter = visuals.AddComponent<MeshFilter>();
        // Rectangle: 20cm long, 5cm wide, 5cm tall (like a plank)
        Mesh rectangleMesh = GetOrCreateMesh("RectangleMesh", () => CreateRectangleMesh(0.2f, 0.05f, 0.05f));
        meshFilter.sharedMesh = rectangleMesh;
        
        MeshRenderer meshRenderer = visuals.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        
        // Create Collider child
        GameObject colliderObj = new GameObject("Collider");
        colliderObj.transform.SetParent(block.transform);
        colliderObj.transform.localPosition = Vector3.zero;
        colliderObj.transform.localRotation = Quaternion.identity;
        colliderObj.transform.localScale = Vector3.one;
        
        BoxCollider boxCollider = colliderObj.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(0.2f, 0.05f, 0.05f);
        
        // Set predicted visuals transform for XR Grab
        grabInteractable.predictedVisualsTransform = visuals.transform;
        
        // Create prefab
        PrefabUtility.SaveAsPrefabAsset(block, prefabPath);
        Object.DestroyImmediate(block);
        
        Debug.Log($"Created Block_Rectangle.prefab at {prefabPath}");
    }

    /// <summary>
    /// Creates an archway block prefab (block with arch cutout).
    /// </summary>
    private static void CreateArchBlock(Material material)
    {
        string prefabPath = $"{BlocksFolderPath}/Block_Arch.prefab";
        
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.LogWarning($"Block_Arch.prefab already exists at {prefabPath}. Skipping creation.");
            return;
        }
        
        // Create root GameObject
        GameObject block = new GameObject("Block_Arch");
        
        // Add components to root
        Rigidbody rigidbody = block.AddComponent<Rigidbody>();
        ConfigureRigidbody(rigidbody);
        
        XRGrabInteractable grabInteractable = block.AddComponent<XRGrabInteractable>();
        ConfigureGrabInteractable(grabInteractable);
        
        // Add collision controller for dynamic collision detection switching
        block.AddComponent<BlockCollisionController>();
        
        // Create Visuals child
        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(block.transform);
        visuals.transform.localPosition = Vector3.zero;
        visuals.transform.localRotation = Quaternion.identity;
        visuals.transform.localScale = Vector3.one;
        
        MeshFilter meshFilter = visuals.AddComponent<MeshFilter>();
        // Arch: 10cm tall, 10cm wide, 5cm deep with arch cutout
        Mesh archMesh = GetOrCreateMesh("ArchMesh", () => CreateArchMesh(0.1f, 0.1f, 0.05f));
        meshFilter.sharedMesh = archMesh;
        
        MeshRenderer meshRenderer = visuals.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        
        // Create Collider child - use MeshCollider for arch
        GameObject colliderObj = new GameObject("Collider");
        colliderObj.transform.SetParent(block.transform);
        colliderObj.transform.localPosition = Vector3.zero;
        colliderObj.transform.localRotation = Quaternion.identity;
        colliderObj.transform.localScale = Vector3.one;
        
        MeshCollider meshCollider = colliderObj.AddComponent<MeshCollider>();
        string meshPath = $"{MeshesFolderPath}/ArchMesh.asset";
        Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (meshAsset != null)
        {
            meshCollider.sharedMesh = meshAsset;
        }
        else
        {
            meshCollider.sharedMesh = archMesh;
        }
        meshCollider.convex = true;
        
        // Set predicted visuals transform for XR Grab
        grabInteractable.predictedVisualsTransform = visuals.transform;
        
        // Create prefab
        PrefabUtility.SaveAsPrefabAsset(block, prefabPath);
        Object.DestroyImmediate(block);
        
        Debug.Log($"Created Block_Arch.prefab at {prefabPath}");
    }

    /// <summary>
    /// Creates a big triangular prism block prefab (larger than the regular triangle).
    /// </summary>
    private static void CreateBigTriangleBlock(Material material)
    {
        string prefabPath = $"{BlocksFolderPath}/Block_BigTriangle.prefab";
        
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.LogWarning($"Block_BigTriangle.prefab already exists at {prefabPath}. Skipping creation.");
            return;
        }
        
        // Create root GameObject
        GameObject block = new GameObject("Block_BigTriangle");
        
        // Add components to root
        Rigidbody rigidbody = block.AddComponent<Rigidbody>();
        ConfigureRigidbody(rigidbody);
        
        XRGrabInteractable grabInteractable = block.AddComponent<XRGrabInteractable>();
        ConfigureGrabInteractable(grabInteractable);
        
        // Add collision controller for dynamic collision detection switching
        block.AddComponent<BlockCollisionController>();
        
        // Create Visuals child
        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(block.transform);
        visuals.transform.localPosition = Vector3.zero;
        visuals.transform.localRotation = Quaternion.identity;
        visuals.transform.localScale = Vector3.one;
        
        MeshFilter meshFilter = visuals.AddComponent<MeshFilter>();
        // Big Triangle: 20cm base, 10cm height (twice as wide as regular triangle)
        Mesh bigTriangleMesh = GetOrCreateMesh("BigTriangleMesh", () => CreateTriangularPrismMesh(0.2f, 0.1f));
        meshFilter.sharedMesh = bigTriangleMesh;
        
        MeshRenderer meshRenderer = visuals.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        
        // Create Collider child - use MeshCollider for triangle
        GameObject colliderObj = new GameObject("Collider");
        colliderObj.transform.SetParent(block.transform);
        colliderObj.transform.localPosition = Vector3.zero;
        colliderObj.transform.localRotation = Quaternion.identity;
        colliderObj.transform.localScale = Vector3.one;
        
        MeshCollider meshCollider = colliderObj.AddComponent<MeshCollider>();
        string meshPath = $"{MeshesFolderPath}/BigTriangleMesh.asset";
        Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (meshAsset != null)
        {
            meshCollider.sharedMesh = meshAsset;
        }
        else
        {
            meshCollider.sharedMesh = bigTriangleMesh;
        }
        meshCollider.convex = true;
        
        // Set predicted visuals transform for XR Grab
        grabInteractable.predictedVisualsTransform = visuals.transform;
        
        // Create prefab
        PrefabUtility.SaveAsPrefabAsset(block, prefabPath);
        Object.DestroyImmediate(block);
        
        Debug.Log($"Created Block_BigTriangle.prefab at {prefabPath}");
    }

    /// <summary>
    /// Creates a rectangular block mesh (box with custom dimensions).
    /// </summary>
    private static Mesh CreateRectangleMesh(float length, float width, float height)
    {
        Mesh mesh = new Mesh();
        mesh.name = "RectangleMesh";
        
        float halfLength = length * 0.5f;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        
        Vector3[] vertices = new Vector3[]
        {
            // Front face
            new Vector3(-halfLength, -halfHeight, halfWidth),
            new Vector3(halfLength, -halfHeight, halfWidth),
            new Vector3(halfLength, halfHeight, halfWidth),
            new Vector3(-halfLength, halfHeight, halfWidth),
            // Back face
            new Vector3(-halfLength, -halfHeight, -halfWidth),
            new Vector3(-halfLength, halfHeight, -halfWidth),
            new Vector3(halfLength, halfHeight, -halfWidth),
            new Vector3(halfLength, -halfHeight, -halfWidth),
            // Top face
            new Vector3(-halfLength, halfHeight, -halfWidth),
            new Vector3(-halfLength, halfHeight, halfWidth),
            new Vector3(halfLength, halfHeight, halfWidth),
            new Vector3(halfLength, halfHeight, -halfWidth),
            // Bottom face
            new Vector3(-halfLength, -halfHeight, -halfWidth),
            new Vector3(halfLength, -halfHeight, -halfWidth),
            new Vector3(halfLength, -halfHeight, halfWidth),
            new Vector3(-halfLength, -halfHeight, halfWidth),
            // Right face
            new Vector3(halfLength, -halfHeight, -halfWidth),
            new Vector3(halfLength, halfHeight, -halfWidth),
            new Vector3(halfLength, halfHeight, halfWidth),
            new Vector3(halfLength, -halfHeight, halfWidth),
            // Left face
            new Vector3(-halfLength, -halfHeight, -halfWidth),
            new Vector3(-halfLength, -halfHeight, halfWidth),
            new Vector3(-halfLength, halfHeight, halfWidth),
            new Vector3(-halfLength, halfHeight, -halfWidth)
        };
        
        int[] triangles = new int[]
        {
            // Front face
            0, 1, 2, 0, 2, 3,
            // Back face
            4, 5, 6, 4, 6, 7,
            // Top face
            8, 9, 10, 8, 10, 11,
            // Bottom face
            12, 13, 14, 12, 14, 15,
            // Right face
            16, 17, 18, 16, 18, 19,
            // Left face
            20, 21, 22, 20, 22, 23
        };
        
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x / length + 0.5f, vertices[i].y / height + 0.5f);
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }

    /// <summary>
    /// Creates an archway mesh (rectangular block with semicircular arch cutout at top).
    /// Simplified and corrected version.
    /// </summary>
    private static Mesh CreateArchMesh(float width, float height, float depth)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ArchMesh";
        
        // Arch parameters - arch spans most of the width
        float archRadius = width * 0.4f; // Arch radius is 40% of width
        float archStartHeight = height * 0.5f; // Arch starts at middle of height
        int archSegments = 16; // Number of segments for the arch curve
        
        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;
        float halfHeight = height * 0.5f;
        
        // Calculate vertices
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        
        // === FRONT FACE ===
        // Bottom rectangle vertices
        int frontBottomLeft = vertices.Count;
        vertices.Add(new Vector3(-halfWidth, -halfHeight, halfDepth));
        int frontBottomRight = vertices.Count;
        vertices.Add(new Vector3(halfWidth, -halfHeight, halfDepth));
        int frontTopRight = vertices.Count;
        vertices.Add(new Vector3(halfWidth, archStartHeight, halfDepth));
        int frontTopLeft = vertices.Count;
        vertices.Add(new Vector3(-halfWidth, archStartHeight, halfDepth));
        
        // Arch curve vertices (semicircle from -archRadius to +archRadius)
        int frontArchStart = vertices.Count;
        for (int i = 0; i <= archSegments; i++)
        {
            float angle = Mathf.PI * (i / (float)archSegments); // 0 to PI
            float x = Mathf.Cos(angle) * archRadius;
            float y = archStartHeight + Mathf.Sin(angle) * archRadius;
            vertices.Add(new Vector3(x, y, halfDepth));
        }
        
        // Top flat section
        int frontTopFlatLeft = vertices.Count;
        vertices.Add(new Vector3(-halfWidth, halfHeight, halfDepth));
        int frontTopFlatRight = vertices.Count;
        vertices.Add(new Vector3(halfWidth, halfHeight, halfDepth));
        
        // === BACK FACE ===
        int backStart = vertices.Count;
        // Bottom rectangle
        int backBottomLeft = vertices.Count;
        vertices.Add(new Vector3(-halfWidth, -halfHeight, -halfDepth));
        int backBottomRight = vertices.Count;
        vertices.Add(new Vector3(halfWidth, -halfHeight, -halfDepth));
        int backTopRight = vertices.Count;
        vertices.Add(new Vector3(halfWidth, archStartHeight, -halfDepth));
        int backTopLeft = vertices.Count;
        vertices.Add(new Vector3(-halfWidth, archStartHeight, -halfDepth));
        
        // Arch curve
        int backArchStart = vertices.Count;
        for (int i = 0; i <= archSegments; i++)
        {
            float angle = Mathf.PI * (i / (float)archSegments);
            float x = Mathf.Cos(angle) * archRadius;
            float y = archStartHeight + Mathf.Sin(angle) * archRadius;
            vertices.Add(new Vector3(x, y, -halfDepth));
        }
        
        // Top flat section
        int backTopFlatLeft = vertices.Count;
        vertices.Add(new Vector3(-halfWidth, halfHeight, -halfDepth));
        int backTopFlatRight = vertices.Count;
        vertices.Add(new Vector3(halfWidth, halfHeight, -halfDepth));
        
        // === FRONT FACE TRIANGLES ===
        // Bottom rectangle
        triangles.Add(frontBottomLeft); triangles.Add(frontBottomRight); triangles.Add(frontTopRight);
        triangles.Add(frontBottomLeft); triangles.Add(frontTopRight); triangles.Add(frontTopLeft);
        
        // Left pillar (between arch start and left edge)
        triangles.Add(frontTopLeft); triangles.Add(frontArchStart); triangles.Add(frontArchStart + archSegments);
        triangles.Add(frontTopLeft); triangles.Add(frontArchStart + archSegments); triangles.Add(frontTopFlatLeft);
        
        // Right pillar (between arch end and right edge)
        triangles.Add(frontTopRight); triangles.Add(frontTopFlatRight); triangles.Add(frontArchStart + archSegments);
        triangles.Add(frontTopRight); triangles.Add(frontArchStart + archSegments); triangles.Add(frontBottomRight);
        
        // Arch curve (connect arch points to top flat section)
        for (int i = 0; i < archSegments; i++)
        {
            triangles.Add(frontArchStart + i);
            triangles.Add(frontArchStart + i + 1);
            triangles.Add(frontTopFlatLeft);
            triangles.Add(frontArchStart + i + 1);
            triangles.Add(frontTopFlatRight);
            triangles.Add(frontTopFlatLeft);
        }
        
        // === BACK FACE TRIANGLES (reversed winding) ===
        // Bottom rectangle
        triangles.Add(backBottomLeft); triangles.Add(backTopRight); triangles.Add(backBottomRight);
        triangles.Add(backBottomLeft); triangles.Add(backTopLeft); triangles.Add(backTopRight);
        
        // Left pillar
        triangles.Add(backTopLeft); triangles.Add(backArchStart + archSegments); triangles.Add(backArchStart);
        triangles.Add(backTopLeft); triangles.Add(backTopFlatLeft); triangles.Add(backArchStart + archSegments);
        
        // Right pillar
        triangles.Add(backTopRight); triangles.Add(backArchStart + archSegments); triangles.Add(backTopFlatRight);
        triangles.Add(backTopRight); triangles.Add(backBottomRight); triangles.Add(backArchStart + archSegments);
        
        // Arch curve
        for (int i = 0; i < archSegments; i++)
        {
            triangles.Add(backArchStart + i);
            triangles.Add(backTopFlatLeft);
            triangles.Add(backArchStart + i + 1);
            triangles.Add(backArchStart + i + 1);
            triangles.Add(backTopFlatLeft);
            triangles.Add(backTopFlatRight);
        }
        
        // === SIDE FACES ===
        // Bottom left
        triangles.Add(frontBottomLeft); triangles.Add(backBottomLeft); triangles.Add(backTopLeft);
        triangles.Add(frontBottomLeft); triangles.Add(backTopLeft); triangles.Add(frontTopLeft);
        
        // Bottom right
        triangles.Add(frontBottomRight); triangles.Add(backTopRight); triangles.Add(backBottomRight);
        triangles.Add(frontBottomRight); triangles.Add(frontTopRight); triangles.Add(backTopRight);
        
        // Top left (flat section)
        triangles.Add(frontTopFlatLeft); triangles.Add(backTopFlatLeft); triangles.Add(backTopLeft);
        triangles.Add(frontTopFlatLeft); triangles.Add(backTopLeft); triangles.Add(frontTopLeft);
        
        // Top right (flat section)
        triangles.Add(frontTopFlatRight); triangles.Add(backTopRight); triangles.Add(backTopFlatRight);
        triangles.Add(frontTopFlatRight); triangles.Add(frontTopRight); triangles.Add(backTopRight);
        
        // Arch sides (connect front and back arch curves)
        for (int i = 0; i < archSegments; i++)
        {
            int front1 = frontArchStart + i;
            int front2 = frontArchStart + i + 1;
            int back1 = backArchStart + i;
            int back2 = backArchStart + i + 1;
            
            triangles.Add(front1); triangles.Add(back1); triangles.Add(front2);
            triangles.Add(front2); triangles.Add(back1); triangles.Add(back2);
        }
        
        // === UVs ===
        Vector2[] uvs = new Vector2[vertices.Count];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x / width + 0.5f, vertices[i].y / height + 0.5f);
        }
        
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
}

