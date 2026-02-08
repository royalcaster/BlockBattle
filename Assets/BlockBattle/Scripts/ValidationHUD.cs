using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;

namespace BlockBattle
{
    /// <summary>
    /// HUD component that displays build validation results with live updates.
    /// Shows overall accuracy, per-block position/rotation errors, and missing/extra blocks.
    /// </summary>
    public class ValidationHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BuildValidator m_BuildValidator;
        [SerializeField] private ReferenceStructureSpawner m_ReferenceSpawner;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI m_AccuracyText;
        [SerializeField] private TextMeshProUGUI m_CorrectBlocksText;
        [SerializeField] private TextMeshProUGUI m_BlockResultsText;
        [SerializeField] private TextMeshProUGUI m_MissingBlocksText;
        [SerializeField] private TextMeshProUGUI m_ExtraBlocksText;

        [Header("Update Settings")]
        [SerializeField] private float m_UpdateInterval = 0.3f;
        [SerializeField] private bool m_AutoUpdate = true;

        [Header("Display Settings")]
        [SerializeField] private int m_MaxDetailedBlocks = 12;
        [SerializeField] private bool m_ShowPositionErrors = true;
        [SerializeField] private Color m_CorrectColor = new Color(0.2f, 0.9f, 0.2f);
        [SerializeField] private Color m_PartialColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color m_IncorrectColor = new Color(0.9f, 0.3f, 0.2f);
        [SerializeField] private Color m_MissingColor = new Color(0.5f, 0.5f, 0.5f);

        [Header("Camera Following")]
        [SerializeField] private bool m_FollowCamera = true;
        [SerializeField] private float m_FollowSpeed = 5f;
        [SerializeField] private bool m_LockPitch = true;
        [SerializeField] private bool m_LockRoll = true;
        [SerializeField] private float m_DistanceFromCamera = 2f;
        [SerializeField] private float m_HeightOffset = 0.3f;

        private float m_UpdateTimer;
        private BuildValidationResult m_LastResult;
        private Camera m_Camera;
        private Transform m_Transform;

        public BuildValidator BuildValidator
        {
            get => m_BuildValidator;
            set => m_BuildValidator = value;
        }

        public bool AutoUpdate
        {
            get => m_AutoUpdate;
            set => m_AutoUpdate = value;
        }

        private void Start()
        {
            m_Transform = transform;
            m_Camera = Camera.main ?? FindAnyObjectByType<Camera>();

            if (m_BuildValidator == null)
                m_BuildValidator = FindAnyObjectByType<BuildValidator>();

            if (m_ReferenceSpawner == null)
                m_ReferenceSpawner = FindAnyObjectByType<ReferenceStructureSpawner>();

            if (m_ReferenceSpawner != null)
            {
                m_ReferenceSpawner.OnStructureSpawned += OnReferenceStructureSpawned;
                
                if (m_ReferenceSpawner.CurrentSpawnConfiguration != null && m_BuildValidator != null)
                {
                    m_BuildValidator.ReferenceConfiguration = m_ReferenceSpawner.CurrentSpawnConfiguration;
                }
            }

            UpdateHUD();
        }

        private void OnReferenceStructureSpawned(BlockSpawnConfiguration configuration)
        {
            if (m_BuildValidator != null && configuration != null)
            {
                m_BuildValidator.ReferenceConfiguration = configuration;
                UpdateHUD();
            }
        }

        private void OnDestroy()
        {
            if (m_ReferenceSpawner != null)
                m_ReferenceSpawner.OnStructureSpawned -= OnReferenceStructureSpawned;
        }

        private void Update()
        {
            if (m_AutoUpdate && m_BuildValidator != null)
            {
                m_UpdateTimer += Time.deltaTime;
                if (m_UpdateTimer >= m_UpdateInterval)
                {
                    m_UpdateTimer = 0f;
                    UpdateHUD();
                }
            }

            if (m_FollowCamera && m_Camera != null)
                UpdateCameraFollowing();
        }

        private void UpdateCameraFollowing()
        {
            if (m_Camera == null || m_Transform == null) return;

            Transform cameraTransform = m_Camera.transform;
            Vector3 targetPosition = cameraTransform.position + 
                                    cameraTransform.forward * m_DistanceFromCamera + 
                                    Vector3.up * m_HeightOffset;

            m_Transform.position = Vector3.Lerp(m_Transform.position, targetPosition, Time.deltaTime * m_FollowSpeed);

            Quaternion lookRot = Quaternion.LookRotation(cameraTransform.forward);
            Vector3 euler = lookRot.eulerAngles;
            if (m_LockPitch) euler.x = 0f;
            if (m_LockRoll) euler.z = 0f;
            lookRot = Quaternion.Euler(euler);
            m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, lookRot, Time.deltaTime * m_FollowSpeed);
        }

        public void UpdateHUD()
        {
            if (m_BuildValidator == null)
            {
                DisplayNoValidator();
                return;
            }

            if (m_BuildValidator.ReferenceConfiguration == null)
            {
                DisplayNoConfiguration();
                return;
            }

            m_LastResult = m_BuildValidator.ValidateBuild();
            DisplayResults(m_LastResult);
        }

        private void DisplayNoValidator()
        {
            SetText(m_AccuracyText, "<color=#FF6666>No Validator</color>");
            SetText(m_CorrectBlocksText, "Assign BuildValidator");
            SetText(m_BlockResultsText, "");
            SetText(m_MissingBlocksText, "");
            SetText(m_ExtraBlocksText, "");
        }

        private void DisplayNoConfiguration()
        {
            SetText(m_AccuracyText, "<color=#FFAA00>No Reference</color>");
            SetText(m_CorrectBlocksText, "Waiting for structure...");
            SetText(m_BlockResultsText, "");
            SetText(m_MissingBlocksText, "");
            SetText(m_ExtraBlocksText, "");
        }

        private void SetText(TextMeshProUGUI textComponent, string text)
        {
            if (textComponent != null) textComponent.text = text;
        }

        private void DisplayResults(BuildValidationResult result)
        {
            // Main accuracy display with presence info
            if (m_AccuracyText != null)
            {
                Color accuracyColor = GetAccuracyColor(result.AccuracyPercentage);
                string hex = ColorUtility.ToHtmlStringRGB(accuracyColor);
                
                // Show both accuracy (position-based) and presence
                StringBuilder sb = new StringBuilder();
                sb.Append($"<color=#{hex}><size=120%>ACCURACY: {result.AccuracyPercentage:F0}%</size></color>");
                sb.Append($"\n<size=80%>Presence: {result.PresencePercentage:F0}%</size>");
                m_AccuracyText.text = sb.ToString();
            }

            // Correct blocks count with breakdown
            if (m_CorrectBlocksText != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"<b>Correct: {result.CorrectBlocks}/{result.TotalReferenceBlocks}</b>");
                sb.Append($" | In Zone: {result.PresentBlocks}/{result.TotalReferenceBlocks}");
                m_CorrectBlocksText.text = sb.ToString();
            }

            // Per-block results with position errors
            SetText(m_BlockResultsText, FormatBlockResults(result));
            SetText(m_MissingBlocksText, FormatMissingBlocks(result));
            SetText(m_ExtraBlocksText, FormatExtraBlocks(result));
        }

        private string FormatBlockResults(BuildValidationResult result)
        {
            if (result.BlockResults == null || result.BlockResults.Count == 0)
                return "No blocks to validate";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Block Status:</b>");

            int displayCount = Mathf.Min(result.BlockResults.Count, m_MaxDetailedBlocks);
            
            for (int i = 0; i < displayCount; i++)
            {
                BlockValidationResult br = result.BlockResults[i];
                sb.Append(FormatSingleBlockResult(br));
            }

            if (result.BlockResults.Count > m_MaxDetailedBlocks)
            {
                int remaining = result.BlockResults.Count - m_MaxDetailedBlocks;
                sb.AppendLine($"<color=#888888>... +{remaining} more</color>");
            }

            return sb.ToString();
        }

        private string FormatSingleBlockResult(BlockValidationResult br)
        {
            StringBuilder sb = new StringBuilder();
            
            // Determine status and color
            Color statusColor;
            string statusIcon;

            if (!br.IsPresent)
            {
                statusColor = m_MissingColor;
                statusIcon = "[--]";
            }
            else if (br.IsCorrect)
            {
                statusColor = m_CorrectColor;
                statusIcon = "[OK]";
            }
            else if (br.IsPositionCorrect && !br.IsRotationCorrect)
            {
                statusColor = m_PartialColor;
                statusIcon = "[~R]";
            }
            else
            {
                statusColor = m_IncorrectColor;
                statusIcon = "[~P]";
            }

            string hex = ColorUtility.ToHtmlStringRGB(statusColor);
            
            // Block type and color
            string blockName = $"{br.BlockType}";
            string blockColor = $"({br.BlockColor})";
            
            sb.Append($"<color=#{hex}>{statusIcon}</color> ");
            sb.Append($"{blockName} {blockColor}");

            // Show position error details if block is present but not correct
            if (br.IsPresent && !br.IsCorrect && m_ShowPositionErrors)
            {
                float posCm = br.PositionError * 100f; // Convert to cm
                sb.Append($" <color=#888888>| {posCm:F1}cm");
                if (br.RotationError < 999f)
                {
                    sb.Append($", {br.RotationError:F0}°");
                }
                sb.Append("</color>");
            }
            else if (br.IsCorrect && m_ShowPositionErrors)
            {
                float posCm = br.PositionError * 100f;
                sb.Append($" <color=#666666>({posCm:F1}cm)</color>");
            }

            sb.AppendLine();
            return sb.ToString();
        }

        private string FormatMissingBlocks(BuildValidationResult result)
        {
            if (result.MissingBlocks == null || result.MissingBlocks.Count == 0)
                return $"<color=#{ColorUtility.ToHtmlStringRGB(m_CorrectColor)}>All blocks in zone!</color>";

            StringBuilder sb = new StringBuilder();
            string hex = ColorUtility.ToHtmlStringRGB(m_MissingColor);
            sb.AppendLine($"<color=#{hex}><b>MISSING ({result.MissingBlocks.Count}):</b></color>");

            // Group by type+color for cleaner display
            var grouped = new Dictionary<string, int>();
            foreach (var block in result.MissingBlocks)
            {
                if (block == null) continue;
                string key = $"{block.BlockType} ({block.BlockColor})";
                grouped[key] = grouped.ContainsKey(key) ? grouped[key] + 1 : 1;
            }

            foreach (var kvp in grouped)
            {
                sb.Append($"<color=#{hex}>  • {kvp.Key}");
                if (kvp.Value > 1) sb.Append($" x{kvp.Value}");
                sb.AppendLine("</color>");
            }

            return sb.ToString();
        }

        private string FormatExtraBlocks(BuildValidationResult result)
        {
            if (result.ExtraBlocks == null || result.ExtraBlocks.Count == 0)
                return "";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<color=#AAAAAA>Extra blocks: {result.ExtraBlocks.Count}</color>");

            // Group by type for cleaner display
            var grouped = new Dictionary<string, int>();
            foreach (var block in result.ExtraBlocks)
            {
                if (block == null) continue;
                BlockType type = BlockReference.FromGameObject(block)?.BlockType ?? BlockType.Cube;
                string key = type.ToString();
                grouped[key] = grouped.ContainsKey(key) ? grouped[key] + 1 : 1;
            }

            foreach (var kvp in grouped)
            {
                sb.AppendLine($"<color=#888888>  • {kvp.Key} x{kvp.Value}</color>");
            }

            return sb.ToString();
        }

        private Color GetAccuracyColor(float accuracy)
        {
            if (accuracy >= 100f) return new Color(0.2f, 1f, 0.4f); // Bright green
            if (accuracy >= 80f) return m_CorrectColor;
            if (accuracy >= 60f) return m_PartialColor;
            if (accuracy >= 40f) return new Color(1f, 0.5f, 0.2f); // Orange
            return m_IncorrectColor;
        }
    }
}
