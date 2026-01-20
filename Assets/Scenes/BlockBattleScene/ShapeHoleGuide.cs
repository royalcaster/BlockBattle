using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;
using Suit.Core;

namespace Suit.Interactions
{
    public class ShapeHoleGuide : MonoBehaviour
    {
        [Header("Validierung")]
        [SerializeField] private string _requiredTag = "Shape_Cube"; // Bei Loch 2 "Shape_Cylinder"
        [SerializeField] private float _rotationTolerance = 15f;
        
        [Header("Einrast-Logik")]
        [SerializeField] private float _alignmentSpeed = 10f; 
        [SerializeField] private float _autoMoveSpeed = 0.8f; 
        [SerializeField] private float _triggerDepth = -0.1f; 

        [Header("Die Barriere")]
        [SerializeField] private Collider _holeBlocker;

        [Header("Referenzen")]
        [SerializeField] private Transform _finalSnapAnchor; 
        [SerializeField] private GameManager _gameManager; 

        private MeshRenderer _blockerRenderer;
        private bool _isProcessing = false;

        private void Start()
        {
            if (_holeBlocker != null)
            {
                _holeBlocker.isTrigger = false;
                if (_holeBlocker.TryGetComponent<MeshRenderer>(out _blockerRenderer))
                {
                    _blockerRenderer.enabled = false;
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isProcessing) return;
            if (other.CompareTag(_requiredTag))
            {
                if (_holeBlocker != null) _holeBlocker.isTrigger = true;
                HandleGuiding(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(_requiredTag) && !_isProcessing)
            {
                if (_holeBlocker != null) _holeBlocker.isTrigger = false;
            }
        }

        private void HandleGuiding(GameObject shape)
        {
            float angle = Quaternion.Angle(shape.transform.rotation, transform.rotation);
            Vector3 localPos = transform.InverseTransformPoint(shape.transform.position);

            if (angle < _rotationTolerance && localPos.z > _triggerDepth)
            {
                StartCoroutine(AutoMoveAndRelease(shape));
            }
        }

        private IEnumerator AutoMoveAndRelease(GameObject shape)
        {
            _isProcessing = true;

            if (shape.TryGetComponent<XRGrabInteractable>(out var interactable))
            {
                var interactor = interactable.firstInteractorSelecting;
                if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor input) input.SendHapticImpulse(0.5f, 0.15f);
                interactable.enabled = false;
            }

            Rigidbody rb = shape.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            if (_gameManager != null) _gameManager.AddScore();

            while (Vector3.Distance(shape.transform.position, _finalSnapAnchor.position) > 0.01f)
            {
                shape.transform.position = Vector3.MoveTowards(shape.transform.position, _finalSnapAnchor.position, _autoMoveSpeed * Time.deltaTime);
                shape.transform.rotation = Quaternion.Slerp(shape.transform.rotation, _finalSnapAnchor.rotation, _alignmentSpeed * Time.deltaTime);
                yield return null;
            }

            if (_holeBlocker != null)
            {
                _holeBlocker.isTrigger = false;
                if (_blockerRenderer != null) _blockerRenderer.enabled = true;
            }

            if (rb != null) rb.isKinematic = true;
        }
    }
}