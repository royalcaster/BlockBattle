using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlockBattle
{
    /// <summary>
    /// Simple start screen UI that shows a "Start" button.
    /// The game loop only begins when the player presses this button.
    /// Also displays personal best time after completing all levels.
    /// </summary>
    public class StartScreenUI : MonoBehaviour
    {
        private const string PERSONAL_BEST_KEY = "BlockBattle_PersonalBest";

        [Header("UI Elements")]
        [SerializeField, Tooltip("The start button")]
        private Button m_StartButton;

        [SerializeField, Tooltip("The start screen panel (will be hidden after starting)")]
        private GameObject m_StartPanel;

        [SerializeField, Tooltip("Optional title text")]
        private TextMeshProUGUI m_TitleText;

        [SerializeField, Tooltip("Personal best time display")]
        private TextMeshProUGUI m_PersonalBestText;

        [SerializeField, Tooltip("Last completion time display")]
        private TextMeshProUGUI m_LastTimeText;

        [Header("References")]
        [SerializeField, Tooltip("Reference to LevelManager")]
        private LevelManager m_LevelManager;

        /// <summary>
        /// Event fired when the game is started.
        /// </summary>
        public event System.Action OnGameStarted;

        /// <summary>
        /// Gets whether the game has been started.
        /// </summary>
        public bool HasStarted { get; private set; } = false;

        /// <summary>
        /// Gets the personal best time in seconds. Returns -1 if no personal best exists.
        /// </summary>
        public float PersonalBest => PlayerPrefs.GetFloat(PERSONAL_BEST_KEY, -1f);

        private void Start()
        {
            // Find LevelManager if not assigned
            if (m_LevelManager == null)
            {
                m_LevelManager = FindAnyObjectByType<LevelManager>();
            }

            // Subscribe to level manager events
            if (m_LevelManager != null)
            {
                m_LevelManager.OnAllLevelsCompleted += OnAllLevelsCompleted;
            }

            // Set up button click listener
            if (m_StartButton != null)
            {
                m_StartButton.onClick.AddListener(OnStartButtonClicked);
            }

            // Update personal best display
            UpdatePersonalBestDisplay();

            // Ensure start panel is visible
            if (m_StartPanel != null)
            {
                m_StartPanel.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (m_StartButton != null)
            {
                m_StartButton.onClick.RemoveListener(OnStartButtonClicked);
            }

            if (m_LevelManager != null)
            {
                m_LevelManager.OnAllLevelsCompleted -= OnAllLevelsCompleted;
            }
        }

        /// <summary>
        /// Called when the start button is clicked.
        /// </summary>
        private void OnStartButtonClicked()
        {
            if (HasStarted)
                return;

            HasStarted = true;
            Debug.Log("StartScreenUI: Game started!");

            // Hide the start panel
            if (m_StartPanel != null)
            {
                m_StartPanel.SetActive(false);
            }

            // Start the game via LevelManager
            if (m_LevelManager != null)
            {
                m_LevelManager.StartGame();
            }

            // Fire event
            OnGameStarted?.Invoke();
        }

        /// <summary>
        /// Called when all levels are completed.
        /// Saves the new personal best if applicable, resets the game, and shows the start screen.
        /// </summary>
        private void OnAllLevelsCompleted()
        {
            if (m_LevelManager == null)
                return;

            float completionTime = m_LevelManager.FinalCompletionTime;
            float currentBest = PersonalBest;
            bool isNewBest = currentBest < 0 || completionTime < currentBest;

            // Check if this is a new personal best
            if (isNewBest)
            {
                PlayerPrefs.SetFloat(PERSONAL_BEST_KEY, completionTime);
                PlayerPrefs.Save();
                Debug.Log($"StartScreenUI: New personal best! {LevelManager.FormatTime(completionTime)}");
            }

            // Update last time display
            UpdateLastTimeDisplay(completionTime, isNewBest);

            // Update personal best display
            UpdatePersonalBestDisplay();

            // Reset and show start screen after a short delay
            StartCoroutine(ShowStartScreenAfterDelay(3f));
        }

        /// <summary>
        /// Shows the start screen after a delay.
        /// </summary>
        private System.Collections.IEnumerator ShowStartScreenAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Reset the game state
            if (m_LevelManager != null)
            {
                m_LevelManager.ResetGame();
            }

            // Show the start screen
            ShowStartScreen();
        }

        /// <summary>
        /// Updates the last completion time display.
        /// </summary>
        /// <param name="time">The completion time</param>
        /// <param name="isNewBest">Whether this was a new personal best</param>
        private void UpdateLastTimeDisplay(float time, bool isNewBest)
        {
            if (m_LastTimeText == null)
                return;

            if (isNewBest)
            {
                m_LastTimeText.text = $"Last Time: {LevelManager.FormatTime(time)} (NEW BEST!)";
                m_LastTimeText.color = new Color(0.3f, 1f, 0.4f); // Green for new best
            }
            else
            {
                m_LastTimeText.text = $"Last Time: {LevelManager.FormatTime(time)}";
                m_LastTimeText.color = new Color(0.8f, 0.85f, 0.9f); // Light gray
            }
            m_LastTimeText.gameObject.SetActive(true);
        }

        /// <summary>
        /// Updates the personal best text display.
        /// </summary>
        private void UpdatePersonalBestDisplay()
        {
            if (m_PersonalBestText == null)
                return;

            float best = PersonalBest;
            if (best > 0)
            {
                m_PersonalBestText.text = $"Personal Best: {LevelManager.FormatTime(best)}";
                m_PersonalBestText.gameObject.SetActive(true);
            }
            else
            {
                m_PersonalBestText.text = "";
                m_PersonalBestText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Shows the start screen again (useful for restarting).
        /// </summary>
        public void ShowStartScreen()
        {
            HasStarted = false;
            UpdatePersonalBestDisplay();
            
            if (m_StartPanel != null)
            {
                m_StartPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Clears the personal best (for debugging/testing).
        /// </summary>
        public void ClearPersonalBest()
        {
            PlayerPrefs.DeleteKey(PERSONAL_BEST_KEY);
            PlayerPrefs.Save();
            UpdatePersonalBestDisplay();
            Debug.Log("StartScreenUI: Personal best cleared.");
        }
    }
}
