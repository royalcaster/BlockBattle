using UnityEngine;
using UnityEditor;
using BlockBattle;
using System.Collections.Generic;

/// <summary>
/// Editor helper tool to create and edit BlockSpawnConfiguration assets more easily.
/// </summary>
public class BlockBattleSpawnConfigurationHelper : EditorWindow
{
    private BlockSpawnConfiguration m_Configuration;
    private Vector2 m_ScrollPosition;
    private BlockType m_NewBlockType = BlockType.Cube;
    private BlockColor m_NewBlockColor = BlockColor.Natural;
    private Vector3 m_NewPosition = Vector3.zero;
    private Vector3 m_NewRotationEuler = Vector3.zero;

    [MenuItem("BlockBattle/Spawn Configuration Helper")]
    public static void ShowWindow()
    {
        GetWindow<BlockBattleSpawnConfigurationHelper>("Spawn Config Helper");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("BlockBattle Spawn Configuration Helper", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Load or create configuration
        EditorGUILayout.LabelField("Configuration Asset", EditorStyles.boldLabel);
        m_Configuration = (BlockSpawnConfiguration)EditorGUILayout.ObjectField(
            "Configuration",
            m_Configuration,
            typeof(BlockSpawnConfiguration),
            false
        );

        if (m_Configuration == null)
        {
            EditorGUILayout.HelpBox("No configuration selected. Create a new one or load an existing asset.", MessageType.Info);
            
            if (GUILayout.Button("Create New Configuration"))
            {
                CreateNewConfiguration();
            }
            
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Configuration info
        EditorGUILayout.LabelField("Configuration Info", EditorStyles.boldLabel);
        m_Configuration.ConfigurationName = EditorGUILayout.TextField("Name", m_Configuration.ConfigurationName);
        m_Configuration.Description = EditorGUILayout.TextArea(m_Configuration.Description, GUILayout.Height(60));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Add new block
        EditorGUILayout.LabelField("Add New Block", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        m_NewBlockType = (BlockType)EditorGUILayout.EnumPopup("Type", m_NewBlockType);
        m_NewBlockColor = (BlockColor)EditorGUILayout.EnumPopup("Color", m_NewBlockColor);
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        m_NewPosition = EditorGUILayout.Vector3Field("Position", m_NewPosition);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        m_NewRotationEuler = EditorGUILayout.Vector3Field("Rotation (Euler)", m_NewRotationEuler);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Add Block"))
        {
            AddBlock();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // List existing blocks
        EditorGUILayout.LabelField($"Blocks ({m_Configuration.EntryCount})", EditorStyles.boldLabel);
        
        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUILayout.Height(300));

        if (m_Configuration.SpawnEntries != null && m_Configuration.SpawnEntries.Count > 0)
        {
            for (int i = 0; i < m_Configuration.SpawnEntries.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(30));
                
                BlockSpawnEntry entry = m_Configuration.SpawnEntries[i];
                entry.BlockType = (BlockType)EditorGUILayout.EnumPopup(entry.BlockType, GUILayout.Width(100));
                entry.BlockColor = (BlockColor)EditorGUILayout.EnumPopup(entry.BlockColor, GUILayout.Width(100));
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                entry.Position = EditorGUILayout.Vector3Field("Pos", entry.Position, GUILayout.Width(200));
                Vector3 euler = entry.Rotation.eulerAngles;
                euler = EditorGUILayout.Vector3Field("Rot", euler, GUILayout.Width(200));
                entry.Rotation = Quaternion.Euler(euler);
                EditorGUILayout.EndHorizontal();
                
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    RemoveBlock(i);
                    break; // Exit loop since list changed
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No blocks added yet. Use 'Add Block' to add blocks to this configuration.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // Actions
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Clear All"))
        {
            if (EditorUtility.DisplayDialog("Clear All Blocks", "Are you sure you want to remove all blocks?", "Yes", "No"))
            {
                m_Configuration.SpawnEntries.Clear();
                EditorUtility.SetDirty(m_Configuration);
                AssetDatabase.SaveAssets();
            }
        }

        if (GUILayout.Button("Save Configuration"))
        {
            EditorUtility.SetDirty(m_Configuration);
            AssetDatabase.SaveAssets();
            Debug.Log($"BlockBattle: Saved configuration '{m_Configuration.ConfigurationName}'");
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Creates a new BlockSpawnConfiguration asset.
    /// </summary>
    private void CreateNewConfiguration()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Spawn Configuration",
            "NewSpawnConfiguration",
            "asset",
            "Choose where to save the spawn configuration"
        );

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        BlockSpawnConfiguration newConfig = CreateInstance<BlockSpawnConfiguration>();
        newConfig.ConfigurationName = "New Configuration";
        newConfig.Description = "";
        newConfig.SpawnEntries = new List<BlockSpawnEntry>();

        AssetDatabase.CreateAsset(newConfig, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        m_Configuration = newConfig;
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newConfig;

        Debug.Log($"BlockBattle: Created new spawn configuration at {path}");
    }

    /// <summary>
    /// Adds a new block entry to the configuration.
    /// </summary>
    private void AddBlock()
    {
        if (m_Configuration.SpawnEntries == null)
        {
            m_Configuration.SpawnEntries = new List<BlockSpawnEntry>();
        }

        Quaternion rotation = Quaternion.Euler(m_NewRotationEuler);
        BlockSpawnEntry entry = new BlockSpawnEntry(m_NewBlockType, m_NewBlockColor, m_NewPosition, rotation);
        m_Configuration.SpawnEntries.Add(entry);

        EditorUtility.SetDirty(m_Configuration);
        AssetDatabase.SaveAssets();

        // Reset form
        m_NewPosition = Vector3.zero;
        m_NewRotationEuler = Vector3.zero;
    }

    /// <summary>
    /// Removes a block entry at the specified index.
    /// </summary>
    /// <param name="index">Index of the block to remove</param>
    private void RemoveBlock(int index)
    {
        if (m_Configuration.SpawnEntries != null && index >= 0 && index < m_Configuration.SpawnEntries.Count)
        {
            m_Configuration.SpawnEntries.RemoveAt(index);
            EditorUtility.SetDirty(m_Configuration);
            AssetDatabase.SaveAssets();
        }
    }
}

