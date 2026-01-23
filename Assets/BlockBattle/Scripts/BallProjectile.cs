using UnityEngine;

namespace BlockBattle
{
    /// <summary>
    /// Simple ball projectile that auto-destroys after a time or on certain conditions.
    /// Used by the VRSlingshot to knock blocks off the table.
    /// </summary>
    public class BallProjectile : MonoBehaviour
    {
        [Header("Lifetime")]
        [SerializeField, Tooltip("Time in seconds before the ball auto-destroys")]
        private float m_Lifetime = 10f;

        [SerializeField, Tooltip("Destroy on first collision")]
        private bool m_DestroyOnCollision = false;

        [SerializeField, Tooltip("Minimum velocity to stay alive (destroys if ball stops moving)")]
        private float m_MinVelocity = 0.1f;

        [SerializeField, Tooltip("Time ball must be below min velocity before destroying")]
        private float m_StoppedTimeThreshold = 2f;

        [Header("Visual Effects")]
        [SerializeField, Tooltip("Trail renderer (optional)")]
        private TrailRenderer m_Trail;

        [SerializeField, Tooltip("Particle effect on impact (optional)")]
        private GameObject m_ImpactEffectPrefab;

        [Header("Audio")]
        [SerializeField, Tooltip("Sound on impact")]
        private AudioClip m_ImpactSound;

        // Runtime
        private float _spawnTime;
        private float _stoppedTime = 0f;
        private Rigidbody _rigidbody;
        private bool _hasCollided = false;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _spawnTime = Time.time;

            // Ensure proper physics setup for collision with blocks
            if (_rigidbody != null)
            {
                // Use ContinuousDynamic for best collision detection with fast-moving objects
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                
                // Make sure it's not kinematic
                _rigidbody.isKinematic = false;
                _rigidbody.useGravity = true;
            }

            // Ensure collider is set up properly
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                // Must NOT be a trigger to physically collide with blocks
                col.isTrigger = false;
            }

            // Get trail reference if not assigned
            if (m_Trail == null)
            {
                m_Trail = GetComponent<TrailRenderer>();
            }
        }

        private void Update()
        {
            // Check lifetime
            if (Time.time - _spawnTime >= m_Lifetime)
            {
                DestroyBall();
                return;
            }

            // Check if ball has stopped moving
            if (_rigidbody != null && _hasCollided)
            {
                if (_rigidbody.linearVelocity.magnitude < m_MinVelocity)
                {
                    _stoppedTime += Time.deltaTime;
                    if (_stoppedTime >= m_StoppedTimeThreshold)
                    {
                        DestroyBall();
                    }
                }
                else
                {
                    _stoppedTime = 0f;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            _hasCollided = true;

            // Spawn impact effect
            if (m_ImpactEffectPrefab != null)
            {
                ContactPoint contact = collision.contacts[0];
                GameObject effect = Instantiate(m_ImpactEffectPrefab, contact.point, Quaternion.LookRotation(contact.normal));
                Destroy(effect, 2f);
            }

            // Play impact sound
            if (m_ImpactSound != null)
            {
                AudioSource.PlayClipAtPoint(m_ImpactSound, transform.position, 0.5f);
            }

            // Destroy on collision if enabled
            if (m_DestroyOnCollision)
            {
                DestroyBall();
            }

            Debug.Log($"BallProjectile: Hit {collision.gameObject.name}");
        }

        /// <summary>
        /// Destroys the ball with optional effects.
        /// </summary>
        private void DestroyBall()
        {
            // Disable trail before destroying so it fades out nicely
            if (m_Trail != null)
            {
                m_Trail.transform.SetParent(null);
                m_Trail.autodestruct = true;
            }

            Destroy(gameObject);
        }

        #region Static Factory

        /// <summary>
        /// Creates a ball projectile at the specified position with velocity.
        /// </summary>
        /// <param name="prefab">The ball prefab to instantiate</param>
        /// <param name="position">Spawn position</param>
        /// <param name="velocity">Initial velocity</param>
        /// <returns>The spawned ball GameObject</returns>
        public static GameObject Create(GameObject prefab, Vector3 position, Vector3 velocity)
        {
            if (prefab == null)
            {
                Debug.LogError("BallProjectile.Create: Prefab is null!");
                return null;
            }

            GameObject ball = Instantiate(prefab, position, Quaternion.identity);
            
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = velocity;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            return ball;
        }

        /// <summary>
        /// Creates a simple ball projectile without a prefab (for testing).
        /// </summary>
        public static GameObject CreateSimple(Vector3 position, Vector3 velocity, float radius = 0.05f)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "BallProjectile";
            ball.transform.position = position;
            ball.transform.localScale = Vector3.one * radius * 2f;

            // Add rigidbody
            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.linearVelocity = velocity;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.mass = 0.1f;

            // Add this component
            BallProjectile bp = ball.AddComponent<BallProjectile>();
            bp.m_Lifetime = 10f;

            // Set material color
            Renderer renderer = ball.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.8f, 0.2f, 0.2f);
            }

            return ball;
        }

        #endregion
    }
}
