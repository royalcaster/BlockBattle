using UnityEngine;
using UnityEditor;

namespace BlockBattle.Editor
{
    /// <summary>
    /// Editor menu for adding BlockBattle debug tools to the scene.
    /// </summary>
    public static class BlockBattleDebugToolsMenu
    {
        [MenuItem("BlockBattle/Debug/Add Debug Logger Manager", false, 100)]
        public static void AddDebugLogManager()
        {
            var debugLogManagerType = System.Type.GetType("BlockBattle.Debugging.DebugLogManager, Assembly-CSharp");
            if (debugLogManagerType == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "DebugLogManager script not found. Make sure the script exists and Unity has compiled it.", "OK");
                return;
            }
            
            // Check if already exists
            var existing = Object.FindAnyObjectByType(debugLogManagerType) as MonoBehaviour;
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Already Exists", 
                    $"DebugLogManager already exists on '{existing.gameObject.name}'", "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            
            // Create new
            var go = new GameObject("DebugLogManager");
            go.AddComponent(debugLogManagerType);
            Undo.RegisterCreatedObjectUndo(go, "Add DebugLogManager");
            
            Selection.activeGameObject = go;
            UnityEngine.Debug.Log("<color=#00FF00>DebugLogManager added to scene. Configure log categories in the Inspector.</color>");
        }
        
        [MenuItem("BlockBattle/Debug/Add Network Debug Logger", false, 101)]
        public static void AddNetworkDebugLogger()
        {
            var networkDebugLoggerType = System.Type.GetType("BlockBattle.Debugging.NetworkDebugLogger, Assembly-CSharp");
            if (networkDebugLoggerType == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "NetworkDebugLogger script not found. Make sure the script exists and Unity has compiled it.", "OK");
                return;
            }
            
            // Check if already exists
            var existing = Object.FindAnyObjectByType(networkDebugLoggerType) as MonoBehaviour;
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Already Exists", 
                    $"NetworkDebugLogger already exists on '{existing.gameObject.name}'", "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            
            // Create new
            var go = new GameObject("NetworkDebugLogger");
            go.AddComponent(networkDebugLoggerType);
            Undo.RegisterCreatedObjectUndo(go, "Add NetworkDebugLogger");
            
            Selection.activeGameObject = go;
            UnityEngine.Debug.Log("<color=#00FF00>NetworkDebugLogger added to scene.</color>");
        }
        
        [MenuItem("BlockBattle/Debug/Add Multiplayer Diagnostics", false, 102)]
        public static void AddMultiplayerDiagnostics()
        {
            var multiplayerDiagnosticsType = System.Type.GetType("BlockBattle.Debugging.MultiplayerDiagnostics, Assembly-CSharp");
            if (multiplayerDiagnosticsType == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "MultiplayerDiagnostics script not found. Make sure the script exists and Unity has compiled it.", "OK");
                return;
            }
            
            // Check if already exists
            var existing = Object.FindAnyObjectByType(multiplayerDiagnosticsType) as MonoBehaviour;
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Already Exists", 
                    $"MultiplayerDiagnostics already exists on '{existing.gameObject.name}'", "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            
            // Create new
            var go = new GameObject("MultiplayerDiagnostics");
            go.AddComponent(multiplayerDiagnosticsType);
            Undo.RegisterCreatedObjectUndo(go, "Add MultiplayerDiagnostics");
            
            Selection.activeGameObject = go;
            UnityEngine.Debug.Log("<color=#00FF00>MultiplayerDiagnostics added to scene. Run diagnostics from context menu or at runtime.</color>");
        }
        
        [MenuItem("BlockBattle/Debug/Add All Debug Tools", false, 110)]
        public static void AddAllDebugTools()
        {
            var debugLogManagerType = System.Type.GetType("BlockBattle.Debugging.DebugLogManager, Assembly-CSharp");
            var networkDebugLoggerType = System.Type.GetType("BlockBattle.Debugging.NetworkDebugLogger, Assembly-CSharp");
            var multiplayerDiagnosticsType = System.Type.GetType("BlockBattle.Debugging.MultiplayerDiagnostics, Assembly-CSharp");
            
            if (debugLogManagerType == null || networkDebugLoggerType == null || multiplayerDiagnosticsType == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "One or more debug scripts not found. Make sure all Debug scripts exist and Unity has compiled them.", "OK");
                return;
            }
            
            // Create a single parent object
            var existing = GameObject.Find("BlockBattle Debug Tools");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Already Exists", 
                    "Debug tools container already exists. Replace it?", "Yes", "No"))
                {
                    Selection.activeGameObject = existing;
                    return;
                }
                Undo.DestroyObjectImmediate(existing);
            }
            
            var parent = new GameObject("BlockBattle Debug Tools");
            Undo.RegisterCreatedObjectUndo(parent, "Add BlockBattle Debug Tools");
            
            // Add DebugLogManager if not exists
            if (Object.FindAnyObjectByType(debugLogManagerType) == null)
            {
                var logMgr = new GameObject("DebugLogManager");
                logMgr.transform.SetParent(parent.transform);
                var dlm = logMgr.AddComponent(debugLogManagerType);
                
                // Set to network-only by default for multiplayer debugging
                var so = new SerializedObject(dlm);
                so.FindProperty("_networkOnly").boolValue = true;
                so.ApplyModifiedProperties();
            }
            
            // Add NetworkDebugLogger if not exists
            if (Object.FindAnyObjectByType(networkDebugLoggerType) == null)
            {
                var netLog = new GameObject("NetworkDebugLogger");
                netLog.transform.SetParent(parent.transform);
                netLog.AddComponent(networkDebugLoggerType);
            }
            
            // Add MultiplayerDiagnostics if not exists
            if (Object.FindAnyObjectByType(multiplayerDiagnosticsType) == null)
            {
                var diag = new GameObject("MultiplayerDiagnostics");
                diag.transform.SetParent(parent.transform);
                diag.AddComponent(multiplayerDiagnosticsType);
            }
            
            Selection.activeGameObject = parent;
            UnityEngine.Debug.Log("<color=#00FF00>All BlockBattle debug tools added to scene!</color>");
            UnityEngine.Debug.Log("<color=#FFFF00>TIP: DebugLogManager is set to 'Network Only' mode. Expand to see settings.</color>");
        }
        
        [MenuItem("BlockBattle/Debug/Run Scene Diagnostics (Play Mode Only)", false, 200)]
        public static void RunDiagnostics()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Play Mode Required", 
                    "This command only works in Play Mode. Start the game first.", "OK");
                return;
            }
            
            var multiplayerDiagnosticsType = System.Type.GetType("BlockBattle.Debugging.MultiplayerDiagnostics, Assembly-CSharp");
            if (multiplayerDiagnosticsType == null)
            {
                UnityEngine.Debug.LogWarning("MultiplayerDiagnostics script not found. Add it first.");
                return;
            }
            
            var diag = Object.FindAnyObjectByType(multiplayerDiagnosticsType) as MonoBehaviour;
            if (diag == null)
            {
                UnityEngine.Debug.LogWarning("MultiplayerDiagnostics not found in scene. Add it first.");
                return;
            }
            
            // Use reflection to call RunFullDiagnostics since we don't have the type at compile time
            var method = multiplayerDiagnosticsType.GetMethod("RunFullDiagnostics");
            if (method != null)
            {
                method.Invoke(diag, null);
            }
            else
            {
                UnityEngine.Debug.LogWarning("RunFullDiagnostics method not found on MultiplayerDiagnostics.");
            }
        }
    }
}
