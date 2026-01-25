using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor tool to swap the table prefab while preserving all components,
    /// children (BuildZone, PlacementGuides, etc.), and references.
    /// </summary>
    public class TableSwapTool : EditorWindow
    {
        private string m_OldTableName = "Table";
        private string m_NewTablePrefabName = "NewTable";
        private GameObject m_NewTablePrefab;

        [MenuItem("BlockBattle/Swap Table Prefab")]
        public static void ShowWindow()
        {
            GetWindow<TableSwapTool>("Swap Table Prefab");
        }

        private void OnGUI()
        {
            GUILayout.Label("Table Swap Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool swaps the table mesh/model while preserving:\n" +
                "• All child objects (BuildZone, PlacementGuides, etc.)\n" +
                "• All components on the table\n" +
                "• Position, rotation, and scale\n" +
                "• All serialized references", 
                MessageType.Info);

            EditorGUILayout.Space();

            m_OldTableName = EditorGUILayout.TextField("Current Table Name", m_OldTableName);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("New Table Prefab", EditorStyles.boldLabel);
            
            m_NewTablePrefabName = EditorGUILayout.TextField("Prefab Name to Search", m_NewTablePrefabName);
            m_NewTablePrefab = (GameObject)EditorGUILayout.ObjectField("Or Drag Prefab Here", m_NewTablePrefab, typeof(GameObject), false);

            EditorGUILayout.Space();

            if (GUILayout.Button("Find New Table Prefab by Name"))
            {
                FindPrefabByName();
            }

            EditorGUILayout.Space();

            GUI.enabled = m_NewTablePrefab != null;
            if (GUILayout.Button("Swap Table", GUILayout.Height(40)))
            {
                SwapTable();
            }
            GUI.enabled = true;

            if (m_NewTablePrefab == null)
            {
                EditorGUILayout.HelpBox("Please assign or find a new table prefab first.", MessageType.Warning);
            }
        }

        /// <summary>
        /// Searches for a prefab by name in the project.
        /// </summary>
        private void FindPrefabByName()
        {
            string[] guids = AssetDatabase.FindAssets($"{m_NewTablePrefabName} t:Prefab");
            
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Not Found", 
                    $"Could not find a prefab named '{m_NewTablePrefabName}'.\n\nTry a different name or drag the prefab directly.", 
                    "OK");
                return;
            }

            if (guids.Length > 1)
            {
                Debug.Log($"Found {guids.Length} prefabs matching '{m_NewTablePrefabName}':");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Debug.Log($"  - {path}");
                }
            }

            string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            m_NewTablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (m_NewTablePrefab != null)
            {
                Debug.Log($"TableSwapTool: Found prefab at {prefabPath}");
                EditorUtility.DisplayDialog("Found", $"Found prefab: {prefabPath}", "OK");
            }
        }

        /// <summary>
        /// Performs the table swap operation.
        /// </summary>
        private void SwapTable()
        {
            // Find the old table in the scene
            GameObject oldTable = GameObject.Find(m_OldTableName);
            if (oldTable == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    $"Could not find '{m_OldTableName}' in the scene.\n\nMake sure the table exists with that exact name.", 
                    "OK");
                return;
            }

            // Record undo
            Undo.RegisterCompleteObjectUndo(oldTable, "Swap Table Prefab");

            // Store old table's transform
            Vector3 oldPosition = oldTable.transform.position;
            Quaternion oldRotation = oldTable.transform.rotation;
            Vector3 oldScale = oldTable.transform.localScale;
            Transform oldParent = oldTable.transform.parent;
            int siblingIndex = oldTable.transform.GetSiblingIndex();

            // Collect all children from the old table (we'll reparent them)
            List<Transform> childrenToMove = new List<Transform>();
            foreach (Transform child in oldTable.transform)
            {
                childrenToMove.Add(child);
            }

            // Collect all components from the old table (excluding Transform)
            List<Component> componentsToClone = new List<Component>();
            foreach (Component comp in oldTable.GetComponents<Component>())
            {
                if (comp is Transform) continue;
                if (comp is MeshFilter) continue;  // Will come from new prefab
                if (comp is MeshRenderer) continue; // Will come from new prefab
                componentsToClone.Add(comp);
            }

            // Instantiate the new table prefab
            GameObject newTable = (GameObject)PrefabUtility.InstantiatePrefab(m_NewTablePrefab);
            if (newTable == null)
            {
                // Fallback to regular instantiate
                newTable = Instantiate(m_NewTablePrefab);
            }

            newTable.name = m_OldTableName; // Keep the same name
            Undo.RegisterCreatedObjectUndo(newTable, "Swap Table Prefab");

            // Apply old transform
            newTable.transform.SetParent(oldParent);
            newTable.transform.position = oldPosition;
            newTable.transform.rotation = oldRotation;
            newTable.transform.localScale = oldScale;
            newTable.transform.SetSiblingIndex(siblingIndex);

            // Reparent all children from old table to new table
            foreach (Transform child in childrenToMove)
            {
                // Store local position/rotation before reparenting
                Vector3 localPos = child.localPosition;
                Quaternion localRot = child.localRotation;
                Vector3 localScale = child.localScale;

                Undo.SetTransformParent(child, newTable.transform, "Move child to new table");
                
                // Restore local transform
                child.localPosition = localPos;
                child.localRotation = localRot;
                child.localScale = localScale;
            }

            // Copy components from old table to new table
            foreach (Component sourceComp in componentsToClone)
            {
                System.Type compType = sourceComp.GetType();
                
                // Check if new table already has this component type
                Component existingComp = newTable.GetComponent(compType);
                
                if (existingComp != null)
                {
                    // Copy values to existing component
                    EditorUtility.CopySerialized(sourceComp, existingComp);
                    Debug.Log($"TableSwapTool: Updated existing {compType.Name} on new table");
                }
                else
                {
                    // Add new component and copy values
                    Component newComp = Undo.AddComponent(newTable, compType);
                    EditorUtility.CopySerialized(sourceComp, newComp);
                    Debug.Log($"TableSwapTool: Copied {compType.Name} to new table");
                }
            }

            // Update any references in the scene that point to the old table
            UpdateSceneReferences(oldTable, newTable);

            // Destroy the old table
            Undo.DestroyObjectImmediate(oldTable);

            // Select the new table
            Selection.activeGameObject = newTable;
            EditorGUIUtility.PingObject(newTable);

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"TableSwapTool: Successfully swapped table!");
            Debug.Log($"  - Moved {childrenToMove.Count} children to new table");
            Debug.Log($"  - Copied {componentsToClone.Count} components to new table");

            EditorUtility.DisplayDialog("Success", 
                $"Table swapped successfully!\n\n" +
                $"• {childrenToMove.Count} children moved\n" +
                $"• {componentsToClone.Count} components copied\n\n" +
                $"Please verify the setup and save the scene.", 
                "OK");
        }

        /// <summary>
        /// Updates references throughout the scene that pointed to the old table.
        /// </summary>
        private void UpdateSceneReferences(GameObject oldTable, GameObject newTable)
        {
            // Find all MonoBehaviours in the scene
            MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (MonoBehaviour behaviour in allBehaviours)
            {
                if (behaviour == null) continue;

                SerializedObject so = new SerializedObject(behaviour);
                SerializedProperty prop = so.GetIterator();
                bool modified = false;

                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (prop.objectReferenceValue == oldTable)
                        {
                            prop.objectReferenceValue = newTable;
                            modified = true;
                            Debug.Log($"TableSwapTool: Updated reference in {behaviour.gameObject.name}.{behaviour.GetType().Name}.{prop.propertyPath}");
                        }
                        else if (prop.objectReferenceValue is Transform oldTransform && oldTransform != null && oldTransform.gameObject == oldTable)
                        {
                            prop.objectReferenceValue = newTable.transform;
                            modified = true;
                            Debug.Log($"TableSwapTool: Updated transform reference in {behaviour.gameObject.name}.{behaviour.GetType().Name}.{prop.propertyPath}");
                        }
                    }
                }

                if (modified)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(behaviour);
                }
            }
        }
    }
}
