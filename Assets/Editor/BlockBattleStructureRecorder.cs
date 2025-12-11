using UnityEngine;
using UnityEditor;
using BlockBattle;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor tool to record structures from GameObjects in the scene and save them as BlockSpawnConfiguration ScriptableObjects.
/// </summary>
public class BlockBattleStructureRecorder : EditorWindow
{
    private BlockSpawnConfiguration m_CurrentSpawnConfiguration;
    private Vector2 m_ScrollPosition;
    private bool m_AutoDetectBlocks = true;
    private string m_StructureName = "New Structure";
    private string m_StructureDescription = "";

    [MenuItem("BlockBattle/Record Structure")]
    public static void ShowWindow()
    {
        GetWindow<BlockBattleStructureRecorder>("Structure Recorder");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("BlockBattle Structure Recorder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Structure name and description
        m_StructureName = EditorGUILayout.TextField("Structure Name", m_StructureName);
        m_StructureDescription = EditorGUILayout.TextArea(m_StructureDescription, GUILayout.Height(60));
        EditorGUILayout.Space();

        // Current spawn configuration asset
        EditorGUILayout.LabelField("Spawn Configuration Asset", EditorStyles.boldLabel);
        m_CurrentSpawnConfiguration = (BlockSpawnConfiguration)EditorGUILayout.ObjectField(
            "Spawn Configuration",
            m_CurrentSpawnConfiguration,
            typeof(BlockSpawnConfiguration),
            false
        );

        EditorGUILayout.Space();

        // Create new configuration button
        if (GUILayout.Button("Create New Spawn Configuration Asset"))
        {
            CreateNewSpawnConfigurationAsset();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Recording options
        EditorGUILayout.LabelField("Recording Options", EditorStyles.boldLabel);
        m_AutoDetectBlocks = EditorGUILayout.Toggle("Auto-detect Block Types", m_AutoDetectBlocks);
        EditorGUILayout.HelpBox(
            "Auto-detect: Determines block type from GameObject name or prefab name.\n" +
            "Manual: You can manually select blocks and assign types.",
            MessageType.Info
        );

        EditorGUILayout.Space();

        // Record from selection
        if (GUILayout.Button("Record Blocks from Selection"))
        {
            RecordFromSelection();
        }

        EditorGUILayout.Space();

        // Display current blocks
        if (m_CurrentSpawnConfiguration != null)
        {
            EditorGUILayout.LabelField("Current Blocks", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Block Count: {m_CurrentSpawnConfiguration.EntryCount}");

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUILayout.Height(200));

            if (m_CurrentSpawnConfiguration.SpawnEntries != null && m_CurrentSpawnConfiguration.SpawnEntries.Count > 0)
            {
                for (int i = 0; i < m_CurrentSpawnConfiguration.SpawnEntries.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Block {i + 1}:", GUILayout.Width(60));
                    EditorGUILayout.EnumPopup(m_CurrentSpawnConfiguration.SpawnEntries[i].BlockType, GUILayout.Width(80));
                    EditorGUILayout.EnumPopup(m_CurrentSpawnConfiguration.SpawnEntries[i].BlockColor, GUILayout.Width(80));
                    EditorGUILayout.LabelField($"Pos: {m_CurrentSpawnConfiguration.SpawnEntries[i].Position}", GUILayout.ExpandWidth(true));

                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        RemoveBlock(i);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No blocks recorded yet. Select GameObjects and click 'Record Blocks from Selection'.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Clear all blocks
            if (GUILayout.Button("Clear All Blocks"))
            {
                if (EditorUtility.DisplayDialog("Clear All Blocks", "Are you sure you want to remove all blocks from this configuration?", "Yes", "No"))
                {
                    m_CurrentSpawnConfiguration.SpawnEntries.Clear();
                    EditorUtility.SetDirty(m_CurrentSpawnConfiguration);
                    AssetDatabase.SaveAssets();
                }
            }

            // Validate structure
            EditorGUILayout.Space();
            if (GUILayout.Button("Validate Configuration"))
            {
                if (m_CurrentSpawnConfiguration.Validate())
                {
                    EditorUtility.DisplayDialog("Validation", "Configuration is valid!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Validation", "Configuration has errors. Check console for details.", "OK");
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No spawn configuration asset selected. Create a new one or assign an existing asset.", MessageType.Warning);
        }
    }

    /// <summary>
    /// Creates a new BlockSpawnConfiguration ScriptableObject asset.
    /// </summary>
    private void CreateNewSpawnConfigurationAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Spawn Configuration",
            m_StructureName,
            "asset",
            "Choose where to save the spawn configuration asset"
        );

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        BlockSpawnConfiguration newConfig = CreateInstance<BlockSpawnConfiguration>();
        newConfig.ConfigurationName = m_StructureName;
        newConfig.Description = m_StructureDescription;
        newConfig.SpawnEntries = new List<BlockSpawnEntry>();

        AssetDatabase.CreateAsset(newConfig, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        m_CurrentSpawnConfiguration = newConfig;
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newConfig;

        Debug.Log($"BlockBattle: Created new spawn configuration asset at {path}");
    }

    /// <summary>
    /// Records blocks from the currently selected GameObjects in the scene.
    /// </summary>
    private void RecordFromSelection()
    {
        if (m_CurrentSpawnConfiguration == null)
        {
            EditorUtility.DisplayDialog("No Configuration Selected", "Please create or select a Spawn Configuration asset first.", "OK");
            return;
        }

        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select GameObjects in the scene to record.", "OK");
            return;
        }

        int recordedCount = 0;
        int skippedCount = 0;

        foreach (GameObject obj in selectedObjects)
        {
            // Check if this looks like a block (has XRGrabInteractable or matches block naming pattern)
            bool isBlock = obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() != null ||
                          obj.name.ToLower().Contains("block");

            if (!isBlock)
            {
                skippedCount++;
                continue;
            }

            BlockReference blockRef = BlockReference.FromGameObject(obj);
            if (blockRef != null)
            {
                // Try to detect block color from material
                BlockColor detectedColor = BlockColor.Natural; // Default
                Transform visuals = obj.transform.Find("Visuals");
                if (visuals != null)
                {
                    MeshRenderer renderer = visuals.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterial != null)
                    {
                        string materialName = renderer.sharedMaterial.name;
                        // Try to match material name to BlockColor
                        foreach (BlockColor color in System.Enum.GetValues(typeof(BlockColor)))
                        {
                            if (materialName.Contains(color.ToString()))
                            {
                                detectedColor = color;
                                break;
                            }
                        }
                    }
                }

                // Create BlockSpawnEntry from BlockReference
                BlockSpawnEntry entry = new BlockSpawnEntry(blockRef.BlockType, detectedColor, blockRef.Position, blockRef.Rotation);
                m_CurrentSpawnConfiguration.SpawnEntries.Add(entry);
                recordedCount++;
            }
            else
            {
                skippedCount++;
                Debug.LogWarning($"BlockBattle: Could not determine block type for {obj.name}. Skipping.");
            }
        }

        EditorUtility.SetDirty(m_CurrentSpawnConfiguration);
        AssetDatabase.SaveAssets();

        Debug.Log($"BlockBattle: Recorded {recordedCount} blocks. Skipped {skippedCount} objects.");
        EditorUtility.DisplayDialog("Recording Complete", $"Recorded {recordedCount} blocks.\nSkipped {skippedCount} objects.", "OK");
    }

    /// <summary>
    /// Removes a block at the specified index.
    /// </summary>
    /// <param name="index">Index of the block to remove</param>
    private void RemoveBlock(int index)
    {
        if (m_CurrentSpawnConfiguration == null || m_CurrentSpawnConfiguration.SpawnEntries == null)
        {
            return;
        }

        if (index >= 0 && index < m_CurrentSpawnConfiguration.SpawnEntries.Count)
        {
            m_CurrentSpawnConfiguration.SpawnEntries.RemoveAt(index);
            EditorUtility.SetDirty(m_CurrentSpawnConfiguration);
            AssetDatabase.SaveAssets();
        }
    }
}

