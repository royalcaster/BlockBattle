using UnityEngine;
using TMPro;
using System.Collections;

namespace Suit.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("UI Anzeige")]
        [SerializeField] private TextMeshProUGUI _displayLabel; 
        
        [Header("Einstellungen")]
        [SerializeField] private int _targetScore = 2; // Ziel: 2 Quadrate
        [SerializeField] private string _victoryMessage = "GESCHAFFT!";
        [SerializeField] private Color _victoryColor = Color.yellow;

        private int _currentScore = 0;

        private void Start()
        {
            UpdateUI();
        }

        public void AddScore()
        {
            _currentScore++;
            UpdateUI(); 
            
            if (_currentScore >= _targetScore)
            {
                StartCoroutine(ShowVictoryRoutine());
            }
        }

        private void UpdateUI()
        {
            if (_displayLabel != null)
            {
                // Zeigt "0 von 2", "1 von 2", "2 von 2"
                _displayLabel.text = _currentScore.ToString() + " von " + _targetScore.ToString();
            }
        }

        private IEnumerator ShowVictoryRoutine()
        {
            yield return new WaitForSeconds(0.8f); // Kurze Pause nach dem letzten Einrasten

            if (_displayLabel != null)
            {
                _displayLabel.text = _victoryMessage;
                _displayLabel.color = _victoryColor;
                _displayLabel.transform.localScale = Vector3.one * 1.2f;
            }
        }
    }
}