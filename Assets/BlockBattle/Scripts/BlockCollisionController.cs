using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Manages collision detection mode for blocks to optimize performance.
/// Uses ContinuousDynamic when held for stable stacking, switches to Discrete when placed.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class BlockCollisionController : MonoBehaviour
{
    private Rigidbody _rigidbody;
    private XRGrabInteractable _grabInteractable;
    private bool _isHeld;
    private float _settleTimer;
    private const float SettleTime = 0.5f; // Time in seconds before switching to Discrete
    
    /// <summary>
    /// Initializes component references.
    /// </summary>
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _grabInteractable = GetComponent<XRGrabInteractable>();
        
        // Subscribe to grab events
        _grabInteractable.selectEntered.AddListener(OnGrabbed);
        _grabInteractable.selectExited.AddListener(OnReleased);
        
        // Start with ContinuousDynamic for initial placement stability
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }
    
    /// <summary>
    /// Called when the block is grabbed.
    /// </summary>
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _isHeld = true;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _settleTimer = 0f;
    }
    
    /// <summary>
    /// Called when the block is released.
    /// </summary>
    private void OnReleased(SelectExitEventArgs args)
    {
        _isHeld = false;
        _settleTimer = 0f;
    }
    
    /// <summary>
    /// Updates collision detection mode based on block state.
    /// </summary>
    private void Update()
    {
        if (!_isHeld && _rigidbody.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic)
        {
            // Check if block has settled (low velocity)
            if (_rigidbody.linearVelocity.magnitude < 0.1f && _rigidbody.angularVelocity.magnitude < 0.1f)
            {
                _settleTimer += Time.deltaTime;
                
                if (_settleTimer >= SettleTime)
                {
                    // Switch to Discrete for better performance
                    _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }
            }
            else
            {
                // Reset timer if block is still moving
                _settleTimer = 0f;
            }
        }
    }
    
    /// <summary>
    /// Cleanup event subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        if (_grabInteractable != null)
        {
            _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            _grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
}

