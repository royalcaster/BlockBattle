using UnityEngine;

namespace BlockBattle
{
    /// <summary>
    /// Safety component that stops block movement after a timeout period.
    /// Prevents blocks from rolling forever after being ejected from the shelf.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BlockMovementTimeout : MonoBehaviour
    {
        [SerializeField, Tooltip("Maximum time in seconds a block can move before being stopped")]
        private float m_TimeoutDuration = 5f;

        [SerializeField, Tooltip("Minimum velocity magnitude to consider the block 'moving'")]
        private float m_MovementThreshold = 0.01f;

        private Rigidbody m_Rigidbody;
        private float m_MovementTimer = 0f;
        private bool m_IsMoving = false;
        private bool m_HasTimedOut = false;

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (m_Rigidbody == null || m_HasTimedOut)
                return;

            // Don't track kinematic objects
            if (m_Rigidbody.isKinematic)
            {
                m_MovementTimer = 0f;
                m_IsMoving = false;
                return;
            }

            // Check if block is moving
            float speed = m_Rigidbody.linearVelocity.magnitude;
            float angularSpeed = m_Rigidbody.angularVelocity.magnitude;
            bool currentlyMoving = speed > m_MovementThreshold || angularSpeed > m_MovementThreshold;

            if (currentlyMoving)
            {
                if (!m_IsMoving)
                {
                    // Just started moving
                    m_IsMoving = true;
                    m_MovementTimer = 0f;
                }

                m_MovementTimer += Time.fixedDeltaTime;

                // Check timeout
                if (m_MovementTimer >= m_TimeoutDuration)
                {
                    StopBlock();
                }
            }
            else
            {
                // Block stopped naturally, reset timer
                m_IsMoving = false;
                m_MovementTimer = 0f;
            }
        }

        /// <summary>
        /// Stops the block by zeroing velocity but keeping it interactable.
        /// </summary>
        private void StopBlock()
        {
            m_HasTimedOut = true;
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            
            Debug.Log($"BlockMovementTimeout: Stopped {gameObject.name} after {m_TimeoutDuration}s of movement");
        }

        /// <summary>
        /// Resets the timeout state. Call this when the block is grabbed or moved intentionally.
        /// </summary>
        public void ResetTimeout()
        {
            m_HasTimedOut = false;
            m_MovementTimer = 0f;
            m_IsMoving = false;
        }

        /// <summary>
        /// Called when grabbed by XR Interactor - resets the timeout.
        /// </summary>
        public void OnSelectEntered()
        {
            ResetTimeout();
        }
    }
}
