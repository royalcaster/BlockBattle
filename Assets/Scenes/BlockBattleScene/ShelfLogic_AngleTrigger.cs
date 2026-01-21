using System.Collections.Generic;
using UnityEngine;

namespace Scenes.Sandbox
{
    public class ShelfLogic_TwoDoors : MonoBehaviour
    {
        [Header("Zufall & Chaos")]
        public float spreadAmount = 0.3f; // Wie stark streuen sie? (0 = Strahl, 1 = Schrotflinte)
        public float tumbleForce = 10f;   // Wie stark drehen sie sich?
        
        [Header("Einstellungen Blöcke")]
        public float ejectionForce = 15f;
        public Transform ejectionDirection;

        [Header("Tür Einstellungen")]
        public HingeJoint leftDoor;  
        public HingeJoint rightDoor;
        
        [Range(0f, 120f)]
        public float triggerAngle = 70f; // Ab hier fliegt alles raus
        
        [Range(0f, 120f)]
        public float resetAngle = 60f;   // Ab hier wird "nachgeladen" (Muss kleiner sein als Trigger!)
        
        public float doorKickForce = 30f; 

        // Zustand
        private bool hasTriggered = false; 
        private List<Rigidbody> storedBlocks = new List<Rigidbody>();

        // --- Trigger Logik (Bleibt gleich) ---
        private void OnTriggerEnter(Collider other)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && !storedBlocks.Contains(rb))
            {
                storedBlocks.Add(rb);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && storedBlocks.Contains(rb))
            {
                storedBlocks.Remove(rb);
            }
        }
        // -------------------------------------

        private void Update()
        {
            if (leftDoor == null || rightDoor == null) return;

            float angleL = Mathf.Abs(leftDoor.angle);
            float angleR = Mathf.Abs(rightDoor.angle);

            // 1. FEUERN: Wenn eine Tür weit genug offen ist
            if ((angleL >= triggerAngle || angleR >= triggerAngle) && !hasTriggered)
            {
                FireEverything();
                hasTriggered = true; // Sicherung rein: Jetzt ist "leergeschossen"
            }

            // 2. NACHLADEN: Wenn BEIDE Türen wieder unter dem Reset-Winkel sind
            // Vorher stand hier < 5f (fast zu). Jetzt nehmen wir den resetAngle (z.B. 60).
            if (angleL < resetAngle && angleR < resetAngle && hasTriggered)
            {
                hasTriggered = false; // Klick! System ist wieder bereit.
                Debug.Log("System neu geladen!"); 
            }
        }

        private void FireEverything()
        {
            Debug.Log($"Feuere {storedBlocks.Count} Blöcke ab!");

            for (int i = storedBlocks.Count - 1; i >= 0; i--)
            {
                Rigidbody rb = storedBlocks[i];
                if (rb != null)
                {
                    // 1. Basis-Richtung holen (Der blaue Pfeil)
                    Vector3 baseDir = ejectionDirection != null ? ejectionDirection.forward : transform.forward;

                    // 2. ZUFALLS-RICHTUNG (Streuung)
                    // Wir würfeln kleine Werte für X, Y und Z
                    float randomX = Random.Range(-spreadAmount, spreadAmount);
                    float randomY = Random.Range(-spreadAmount, spreadAmount) + 0.1f; // +0.1f damit sie eher leicht nach oben tendieren
                    float randomZ = Random.Range(-spreadAmount, spreadAmount);

                    // Wir addieren den Zufall auf die Basis-Richtung
                    Vector3 randomDir = (baseDir + new Vector3(randomX, randomY, randomZ)).normalized;

                    // 3. ZUFALLS-KRAFT (Damit nicht alle gleich weit fliegen)
                    // Mal 80% Kraft, mal 120% Kraft
                    float randomPower = ejectionForce * Random.Range(0.8f, 1.2f);

                    // 4. Wachrütteln & Feuern
                    rb.WakeUp();
                    rb.isKinematic = false;
                    rb.linearVelocity = randomDir * randomPower;

                    // 5. TRUDELN (Wichtig für Realismus!)
                    // Gibt dem Block einen zufälligen Drehwurm
                    rb.AddTorque(Random.insideUnitSphere * tumbleForce, ForceMode.Impulse);
                }
            }

            // Türen kicken (bleibt gleich)
            KickDoor(leftDoor);
            KickDoor(rightDoor);
        }

        private void KickDoor(HingeJoint door)
        {
            Rigidbody rb = door.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float direction = Mathf.Sign(door.angle); 
                if (direction == 0) direction = 1;
                rb.AddRelativeTorque(Vector3.up * doorKickForce * direction, ForceMode.Impulse);
            }
        }
    }
}