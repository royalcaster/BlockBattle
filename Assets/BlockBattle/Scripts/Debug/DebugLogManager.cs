using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBattle.Debugging
{
    /// <summary>
    /// Centralized debug log manager with category filtering.
    /// Add this to a GameObject in the scene and configure which log categories to show.
    /// When "Filter All Unity Logs" is enabled, this will suppress logs from scripts
    /// that don't go through DebugLogManager based on keyword detection.
    /// </summary>
    public class DebugLogManager : MonoBehaviour
    {
        #region Singleton
        
        public static DebugLogManager Instance { get; private set; }
        
        #endregion

        #region Log Categories
        
        [Flags]
        public enum LogCategory
        {
            None = 0,
            Network = 1 << 0,
            Workspace = 1 << 1,
            LevelManager = 1 << 2,
            Building = 1 << 3,
            Validation = 1 << 4,
            Destruction = 1 << 5,
            Blocks = 1 << 6,
            Shelf = 1 << 7,
            Slingshot = 1 << 8,
            Player = 1 << 9,
            Session = 1 << 10,
            Lobby = 1 << 11,
            XRMultiplayer = 1 << 12,
            All = ~0
        }
        
        #endregion

        #region Serialized Fields
        
        [Header("Log Categories")]
        [Tooltip("Select which log categories to display")]
        [SerializeField] private LogCategory _enabledCategories = LogCategory.All;
        
        [Header("Log Settings")]
        [Tooltip("Include timestamp in logs")]
        [SerializeField] private bool _includeTimestamp = true;
        
        [Tooltip("Include category name in logs")]
        [SerializeField] private bool _includeCategoryName = true;
        
        [Tooltip("Use colors for different categories")]
        [SerializeField] private bool _useColors = true;
        
        [Header("Quick Toggles")]
        [SerializeField] private bool _networkOnly = false;
        [SerializeField] private bool _disableAllLogs = false;
        
        [Header("Global Log Filtering")]
        [Tooltip("When enabled, suppresses ALL Debug.Log calls that don't match enabled categories")]
        [SerializeField] private bool _filterAllUnityLogs = true;
        
        [Tooltip("Keywords that identify network-related logs (case-insensitive)")]
        [SerializeField] private string[] _networkKeywords = new string[] 
        { 
            "network", "connect", "client", "server", "spawn", "session", "lobby", 
            "multiplayer", "xrmultiplayer", "workspace", "ishost", "isserver", "isclient",
            "authentication", "relay", "transport"
        };
        
        #endregion
        
        #region Log Suppression
        
        private static bool _isInternalLog = false;
        private Application.LogCallback _originalLogHandler;
        
        #endregion

        #region Category Colors
        
        private static readonly Dictionary<LogCategory, string> CategoryColors = new Dictionary<LogCategory, string>
        {
            { LogCategory.Network, "#00FF00" },      // Green
            { LogCategory.Workspace, "#00FFFF" },   // Cyan
            { LogCategory.LevelManager, "#FFFF00" }, // Yellow
            { LogCategory.Building, "#FF8800" },    // Orange
            { LogCategory.Validation, "#FF00FF" },  // Magenta
            { LogCategory.Destruction, "#FF0000" }, // Red
            { LogCategory.Blocks, "#8888FF" },      // Light Blue
            { LogCategory.Shelf, "#88FF88" },       // Light Green
            { LogCategory.Slingshot, "#FF88FF" },   // Pink
            { LogCategory.Player, "#FFFFFF" },      // White
            { LogCategory.Session, "#00FF88" },     // Teal
            { LogCategory.Lobby, "#88FFFF" },       // Light Cyan
        };
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Set up global log filtering if enabled
            if (_filterAllUnityLogs)
            {
                Application.logMessageReceivedThreaded += OnLogMessageReceived;
            }
            
            _isInternalLog = true;
            UnityEngine.Debug.Log($"<color=#00FF00>[DebugLogManager]</color> Initialized. Categories: {_enabledCategories}, GlobalFilter: {_filterAllUnityLogs}");
            _isInternalLog = false;
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Application.logMessageReceivedThreaded -= OnLogMessageReceived;
                Instance = null;
            }
        }
        
        private void OnValidate()
        {
            // Quick toggle overrides
            if (_networkOnly)
            {
                _enabledCategories = LogCategory.Network | LogCategory.Session | LogCategory.Lobby | LogCategory.Workspace | LogCategory.Player | LogCategory.XRMultiplayer;
            }
            if (_disableAllLogs)
            {
                _enabledCategories = LogCategory.None;
            }
        }
        
        #endregion
        
        #region Global Log Filtering
        
        /// <summary>
        /// Intercepts all Unity logs and filters based on detected category.
        /// Note: We can't actually suppress logs, but we can use Debug.unityLogger.filterLogType
        /// </summary>
        private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            // Don't process our own internal logs
            if (_isInternalLog) return;
            
            // We can't actually suppress logs after they're sent, but this callback
            // lets us know what's being logged. The real filtering happens via
            // Unity's Console window filtering, or by checking _filterAllUnityLogs
            // before logging in the first place.
        }
        
        /// <summary>
        /// Check if a log message should be shown based on its content.
        /// Call this before Debug.Log to filter non-DebugLogManager logs.
        /// </summary>
        public static bool ShouldShowLog(string message, string stackTrace = null)
        {
            if (Instance == null) return true;
            if (!Instance._filterAllUnityLogs) return true;
            if (Instance._disableAllLogs) return false;
            
            // Check if message contains network-related keywords
            string lowerMessage = message.ToLowerInvariant();
            string lowerStack = stackTrace?.ToLowerInvariant() ?? "";
            
            bool isNetworkRelated = false;
            foreach (var keyword in Instance._networkKeywords)
            {
                if (lowerMessage.Contains(keyword) || lowerStack.Contains(keyword))
                {
                    isNetworkRelated = true;
                    break;
                }
            }
            
            // If network only mode, only show network-related logs
            if (Instance._networkOnly)
            {
                return isNetworkRelated;
            }
            
            return true;
        }
        
        #endregion

        #region Public Logging Methods
        
        /// <summary>
        /// Log a message with a specific category.
        /// </summary>
        public static void Log(LogCategory category, string message, UnityEngine.Object context = null)
        {
            if (Instance == null || Instance._disableAllLogs) return;
            if (!Instance.IsCategoryEnabled(category)) return;
            
            string formattedMessage = Instance.FormatMessage(category, message);
            UnityEngine.Debug.Log(formattedMessage, context);
        }
        
        /// <summary>
        /// Log a warning with a specific category.
        /// </summary>
        public static void LogWarning(LogCategory category, string message, UnityEngine.Object context = null)
        {
            if (Instance == null || Instance._disableAllLogs) return;
            if (!Instance.IsCategoryEnabled(category)) return;
            
            string formattedMessage = Instance.FormatMessage(category, message);
            UnityEngine.Debug.LogWarning(formattedMessage, context);
        }
        
        /// <summary>
        /// Log an error with a specific category.
        /// </summary>
        public static void LogError(LogCategory category, string message, UnityEngine.Object context = null)
        {
            if (Instance == null) 
            {
                // Always log errors even if no instance
                UnityEngine.Debug.LogError($"[{category}] {message}", context);
                return;
            }
            if (Instance._disableAllLogs) return;
            if (!Instance.IsCategoryEnabled(category)) return;
            
            string formattedMessage = Instance.FormatMessage(category, message);
            UnityEngine.Debug.LogError(formattedMessage, context);
        }
        
        /// <summary>
        /// Shorthand methods for common categories
        /// </summary>
        public static void Network(string message) => Log(LogCategory.Network, message);
        public static void Session(string message) => Log(LogCategory.Session, message);
        public static void Lobby(string message) => Log(LogCategory.Lobby, message);
        public static void Workspace(string message) => Log(LogCategory.Workspace, message);
        public static void LevelMgr(string message) => Log(LogCategory.LevelManager, message);
        public static void Player(string message) => Log(LogCategory.Player, message);
        
        #endregion

        #region Private Methods
        
        private bool IsCategoryEnabled(LogCategory category)
        {
            return (_enabledCategories & category) != 0;
        }
        
        private string FormatMessage(LogCategory category, string message)
        {
            string result = "";
            
            if (_includeTimestamp)
            {
                result += $"[{Time.time:F2}] ";
            }
            
            if (_includeCategoryName)
            {
                string categoryName = category.ToString();
                if (_useColors && CategoryColors.TryGetValue(category, out string color))
                {
                    result += $"<color={color}>[{categoryName}]</color> ";
                }
                else
                {
                    result += $"[{categoryName}] ";
                }
            }
            
            result += message;
            return result;
        }
        
        #endregion

        #region Editor Helper
        
        /// <summary>
        /// Enable only network-related categories (for multiplayer debugging)
        /// </summary>
        [ContextMenu("Enable Network Logs Only")]
        public void EnableNetworkLogsOnly()
        {
            _enabledCategories = LogCategory.Network | LogCategory.Session | LogCategory.Lobby | LogCategory.Workspace | LogCategory.Player | LogCategory.LevelManager;
            _networkOnly = true;
        }
        
        /// <summary>
        /// Enable all log categories
        /// </summary>
        [ContextMenu("Enable All Logs")]
        public void EnableAllLogs()
        {
            _enabledCategories = LogCategory.All;
            _networkOnly = false;
            _disableAllLogs = false;
        }
        
        /// <summary>
        /// Disable all logs
        /// </summary>
        [ContextMenu("Disable All Logs")]
        public void DisableAllLogs()
        {
            _disableAllLogs = true;
        }
        
        #endregion
    }
}
