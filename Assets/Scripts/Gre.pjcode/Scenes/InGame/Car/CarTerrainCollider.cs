using System.Collections.Generic;
using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class CarTerrainCollider : MonoBehaviour
    {
        readonly HashSet<Collider> contacts = new HashSet<Collider>();
        readonly List<Collider> terrains = new List<Collider>();
        public System.Action<Collider> TriggerEntered;
        public System.Action<Collider> TriggerExited;
        public bool IsGrounded => contacts.Count > 0;
        public TerrainType Terrain
        {
            get
            {
                for (int i = terrains.Count - 1; i >= 0; i--)
                {
                    Collider c = terrains[i];
                    if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                    if (c.CompareTag("Water")) return TerrainType.Water;
                    if (c.CompareTag("Dirt")) return TerrainType.Dirt;
                }
                // Playable stage also uses tagged solid water surfaces.
                foreach (Collider c in contacts)
                    if (c != null && c.CompareTag("Water")) return TerrainType.Water;
                foreach (Collider c in contacts)
                    if (c != null && c.CompareTag("Dirt")) return TerrainType.Dirt;
                return TerrainType.Default;
            }
        }

        void OnCollisionEnter(Collision collision) { contacts.Add(collision.collider); }
        void OnCollisionStay(Collision collision) { contacts.Add(collision.collider); }
        void OnCollisionExit(Collision collision) { contacts.Remove(collision.collider); }
        void OnTriggerEnter(Collider other)
        {
            if ((other.CompareTag("Dirt") || other.CompareTag("Water")) && !terrains.Contains(other)) terrains.Add(other);
            TriggerEntered?.Invoke(other);
        }
        void OnTriggerExit(Collider other) { terrains.Remove(other); TriggerExited?.Invoke(other); }
        public void ResetContacts() { contacts.Clear(); terrains.Clear(); }
        void OnDisable() { ResetContacts(); }
    }
}
