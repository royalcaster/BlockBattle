using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections;
using System.Collections.Generic;
using BlockBattle;

namespace Suit.Interactions
{
    public class ShapeHoleGuide : MonoBehaviour
    {
        [Header("Block-Typ (Pflicht)")]
        [SerializeField] private BlockType _targetType;

        [Header("Rotation")]
        [SerializeField, Tooltip("Max. Abweichung in Grad (z.B. 15)")]
        private float _rotationToleranceDeg = 15f;

        [Header("Tiefe ins Loch")]
        [SerializeField, Tooltip("Wenn true: transform.forward zeigt ins Loch. Wenn false: -transform.forward zeigt ins Loch.")]
        private bool _forwardPointsIntoHole = true;

        [SerializeField, Tooltip("Mindesttiefe entlang Lochrichtung (Meter), ab der das Snappen startet.")]
        private float _requiredDepth = 0.03f;

        [Header("Snap-Ziel")]
        [SerializeField, Tooltip("Leeres GameObject am Ende des Lochs (SnapAnchor_Cube)")]
        private Transform _finalSnapAnchor;

        [Header("Bewegung")]
        [SerializeField, Tooltip("Zuggeschwindigkeit in m/s")]
        private float _moveSpeed = 0.6f;

        [SerializeField, Tooltip("Rotationsgeschwindigkeit in Grad/s")]
        private float _rotateSpeedDegPerSec = 360f;

        [Header("Nach Erfolg: echter Block soll fallen")]
        [SerializeField, Tooltip("Wie viel Meter unterhalb des Anchors soll der Block freigegeben werden (damit er sichtbar fällt)?")]
        private float _releaseDropOffset = 0.02f;

        [SerializeField, Tooltip("Nach dem Release XRGrab wieder aktivieren? (meist nein)")]
        private bool _reEnableGrabAfterRelease = false;

        [Header("Visual: BlockCube Mesh in der Wand aktivieren")]
        [SerializeField, Tooltip("Das Visual-Objekt in der Wand (Child 'BlockCube'). Wenn leer, wird automatisch nach Child namens 'BlockCube' gesucht.")]
        private GameObject _wallBlockVisual;

        [SerializeField, Tooltip("Wenn true, wird das Visual beim Start deaktiviert.")]
        private bool _disableVisualOnStart = true;

        [Header("Optional: Loch blockieren")]
        [SerializeField] private Collider _holeBlocker;

        [Header("Debug")]
        [SerializeField] private bool _debugLogs = false;

        private MeshRenderer[] _visualRenderers;
        private bool _isProcessing = false;

        private void Awake()
        {
            // Auto-Find für dein Setup: Trigger_Hole... hat Child "BlockCube"
            if (_wallBlockVisual == null)
            {
                var t = transform.Find("BlockCube");
                if (t != null) _wallBlockVisual = t.gameObject;
            }

            if (_wallBlockVisual != null)
                _visualRenderers = _wallBlockVisual.GetComponentsInChildren<MeshRenderer>(true);
        }

        private void Start()
        {
            if (_finalSnapAnchor == null)
                Debug.LogError($"{name}: FinalSnapAnchor fehlt! SnapAnchor_Cube zuweisen.");

            if (_disableVisualOnStart)
                SetWallVisual(false);

            if (_holeBlocker != null)
                _holeBlocker.isTrigger = false;
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isProcessing) return;
            if (_finalSnapAnchor == null) return;

            // Root bestimmen (Collider kann Child sein)
            GameObject shape = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.GetComponentInParent<Rigidbody>()?.gameObject;

            if (shape == null) return;

            // BlockIdentity prüfen
            BlockIdentity blockId = shape.GetComponentInParent<BlockIdentity>();
            if (blockId == null) return;

            if (blockId.blockType != _targetType) return;

            // Rotation + Tiefe prüfen
            if (!RotationOk(shape.transform)) return;
            if (!DepthOk(shape.transform.position)) return;

            // Optional: Loch öffnen, aber erst wenn wirklich alles passt
            if (_holeBlocker != null)
                _holeBlocker.isTrigger = true;

            StartCoroutine(AutoMoveThenDrop(shape));
        }

        private bool RotationOk(Transform shape)
        {
            float angle = Quaternion.Angle(shape.rotation, transform.rotation);
            if (_debugLogs) Debug.Log($"{name}: angle={angle:0.0} tol={_rotationToleranceDeg:0.0}");
            return angle <= _rotationToleranceDeg;
        }

        private bool DepthOk(Vector3 shapePos)
        {
            Vector3 intoHole = _forwardPointsIntoHole ? transform.forward : -transform.forward;
            float depth = Vector3.Dot(shapePos - transform.position, intoHole);

            if (_debugLogs) Debug.Log($"{name}: depth={depth:0.000} req={_requiredDepth:0.000}");
            return depth >= _requiredDepth;
        }

        private IEnumerator AutoMoveThenDrop(GameObject shape)
        {
            _isProcessing = true;
            if (_debugLogs) Debug.Log($"{name}: Snap START {shape.name}");

            // Physik sichern
            Rigidbody rb = shape.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Aus der Hand lösen
            XRGrabInteractable grab = shape.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                XRInteractionManager manager = grab.interactionManager;

                if (manager != null && grab.isSelected)
                {
                    var interactors = new List<IXRSelectInteractor>();
                    interactors.AddRange(grab.interactorsSelecting);

                    foreach (var interactor in interactors)
                        manager.SelectExit(interactor, grab);
                }

                // Damit er während des Einziehens nicht wieder ankoppelt
                grab.enabled = false;
            }

            // Einziehen + Ausrichten
            while (true)
            {
                float dist = Vector3.Distance(shape.transform.position, _finalSnapAnchor.position);
                if (dist <= 0.005f) break;

                shape.transform.position = Vector3.MoveTowards(
                    shape.transform.position,
                    _finalSnapAnchor.position,
                    _moveSpeed * Time.deltaTime
                );

                shape.transform.rotation = Quaternion.RotateTowards(
                    shape.transform.rotation,
                    transform.rotation,
                    _rotateSpeedDegPerSec * Time.deltaTime
                );

                yield return null;
            }

            // Final exakt setzen (kurz)
            shape.transform.position = _finalSnapAnchor.position;
            shape.transform.rotation = transform.rotation;

            // Wand-Visual aktivieren (BlockCube Mesh an)
            SetWallVisual(true);

            // Loch wieder "zu" (falls du das nutzt)
            if (_holeBlocker != null)
                _holeBlocker.isTrigger = false;

            // Jetzt: echter Block soll fallen
            if (rb != null)
            {
                // kleiner Offset nach unten, damit er sichtbar "losfällt" und nicht zittert/clippt
                shape.transform.position = _finalSnapAnchor.position + Vector3.down * _releaseDropOffset;

                rb.isKinematic = false;
                rb.useGravity = true;
            }

            // Optional: Grab wieder erlauben (meist lässt man es aus)
            if (grab != null && _reEnableGrabAfterRelease)
                grab.enabled = true;

            if (_debugLogs) Debug.Log($"{name}: Snap DONE -> drop + visual enabled");

            _isProcessing = false;
        }

        private void SetWallVisual(bool enabled)
        {
            if (_visualRenderers == null || _visualRenderers.Length == 0) return;

            for (int i = 0; i < _visualRenderers.Length; i++)
                _visualRenderers[i].enabled = enabled;
        }
    }
}
