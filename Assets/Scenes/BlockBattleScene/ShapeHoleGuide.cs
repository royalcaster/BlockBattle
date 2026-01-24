using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;
using Suit.Core;
using BlockBattle; // WICHTIG: Damit wir BlockType und BlockIdentity finden

namespace Suit.Interactions
{
    public class ShapeHoleGuide : MonoBehaviour
    {
        [Header("Validierung (Neu mit Identity)")]
        [SerializeField] private BlockType _targetType; // Statt String-Tag jetzt das Dropdown
        [SerializeField] private float _rotationTolerance = 15f;
        
        [Header("Einrast-Logik")]
        [SerializeField] private float _alignmentSpeed = 10f; 
        [SerializeField] private float _autoMoveSpeed = 0.8f; 
        [SerializeField] private float _triggerDepth = 0.05f; // Positiver Wert ist oft intuitiver für "Tiefe"

        [Header("Die Barriere (Dein unsichtbarer Cube)")]
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
                    _blockerRenderer.enabled = false; // Blocker am Anfang unsichtbar
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isProcessing) return;

            // Suche nach der Identity-Komponente (unser neuer "Ausweis")
            BlockIdentity blockId = other.GetComponentInParent<BlockIdentity>();

            // Prüfen, ob der Block den richtigen Typ hat
            if (blockId != null && blockId.blockType == _targetType)
            {
                if (_holeBlocker != null) _holeBlocker.isTrigger = true;
                HandleGuiding(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Wir prüfen auch hier über die Identity, um konsistent zu bleiben
            BlockIdentity blockId = other.GetComponentInParent<BlockIdentity>();
            if (blockId != null && blockId.blockType == _targetType && !_isProcessing)
            {
                if (_holeBlocker != null) _holeBlocker.isTrigger = false;
            }
        }

        private void HandleGuiding(GameObject shape)
        {
            // Rotation prüfen (relativ zur Wand)
            float angle = Quaternion.Angle(shape.transform.rotation, transform.rotation);
            Vector3 localPos = transform.InverseTransformPoint(shape.transform.position);

            // Wenn Rotation passt und der Block tief genug drin ist
            if (angle < _rotationTolerance && localPos.z > _triggerDepth)
            {
                StartCoroutine(AutoMoveAndRelease(shape));
            }
        }

        private IEnumerator AutoMoveAndRelease(GameObject shape)
        {
            _isProcessing = true;

            // Haptisches Feedback und Loslassen (Dein VR-Code)
            if (shape.TryGetComponent<XRGrabInteractable>(out var interactable))
            {
                var interactor = interactable.firstInteractorSelecting;
                if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor input) 
                    input.SendHapticImpulse(0.5f, 0.15f);
                
                interactable.enabled = false;
            }

            Rigidbody rb = shape.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            if (_gameManager != null) _gameManager.AddScore();

            // Sanftes Einziehen zum Anker
            while (Vector3.Distance(shape.transform.position, _finalSnapAnchor.position) > 0.01f)
            {
                shape.transform.position = Vector3.MoveTowards(shape.transform.position, _finalSnapAnchor.position, _autoMoveSpeed * Time.deltaTime);
                shape.transform.rotation = Quaternion.Slerp(shape.transform.rotation, transform.rotation, _alignmentSpeed * Time.deltaTime);
                yield return null;
            }

            // Finale Positionierung
            shape.transform.position = _finalSnapAnchor.position;
            shape.transform.rotation = transform.rotation;

            // Loch schließen: Blocker sichtbar machen
            if (_holeBlocker != null)
            {
                _holeBlocker.isTrigger = false;
                if (_blockerRenderer != null) _blockerRenderer.enabled = true;
            }

            // Den originalen Block verschwinden lassen, damit nur der saubere Blocker bleibt
            shape.SetActive(false); 
        }
    }
}