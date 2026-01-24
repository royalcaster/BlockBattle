using UnityEngine;
using System;

namespace BlockBattle.Debugging
{
    /// <summary>
    /// Documents and handles known harmless exceptions from Unity Services.
    /// 
    /// NOTE: This is a known Unity Services issue where ObjectDisposedException occurs
    /// during shutdown when the access token changes and triggers cleanup on an already-disposed lobby.
    /// This is harmless and can be safely ignored.
    /// 
    /// Unfortunately, we cannot prevent Unity from logging this error since it occurs
    /// in Unity's internal package code (LobbyHandler.OnAccessTokenChanged).
    /// </summary>
    public class ExceptionFilter : MonoBehaviour
    {
        private static ExceptionFilter _instance;
        private static bool _hasLoggedWarning = false;
        
        [Header("Settings")]
        [Tooltip("Log a one-time warning about known Unity Services exceptions")]
        [SerializeField] private bool _logKnownIssuesWarning = true;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Subscribe to log messages to detect and document known issues
            Application.logMessageReceived += OnLogMessageReceived;
            
            if (_logKnownIssuesWarning && !_hasLoggedWarning)
            {
                _hasLoggedWarning = true;
                UnityEngine.Debug.LogWarning(
                    "<color=#FFFF00>[ExceptionFilter]</color> " +
                    "If you see 'ObjectDisposedException' errors related to Unity Services Lobby during shutdown, " +
                    "this is a known harmless Unity Services cleanup race condition and can be safely ignored."
                );
            }
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                Application.logMessageReceived -= OnLogMessageReceived;
                _instance = null;
            }
        }
        
        private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            // Detect and document known harmless Unity Services cleanup exceptions
            if (type == LogType.Exception)
            {
                // Check if it's the ObjectDisposedException from Unity Services Lobby cleanup
                if (logString.Contains("ObjectDisposedException") && 
                    (logString.Contains("$lobby") || logString.Contains("LobbyHandler") || 
                     stackTrace.Contains("LobbyHandler") || stackTrace.Contains("LobbyChannel") ||
                     stackTrace.Contains("LobbyUnsubscribeCallbacksAsync")))
                {
                    // This is a known harmless cleanup race condition in Unity Services
                    // It happens during shutdown when the lobby is already disposed but 
                    // Unity's internal code (LobbyHandler.OnAccessTokenChanged) still tries to unsubscribe
                    
                    // We can't prevent Unity from logging this, but we can at least
                    // add a helpful comment here for developers
                    
                    // Note: Application.logMessageReceived is read-only - we can't suppress logs
                    // This callback is just for monitoring/documentation purposes
                }
            }
        }
    }
}
