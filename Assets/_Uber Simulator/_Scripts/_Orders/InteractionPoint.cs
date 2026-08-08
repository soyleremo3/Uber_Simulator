using System.Collections.Generic;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Base class for scene points referenced by OrderData via string IDs
    /// (same ID-resolution pattern as the Inventory/ItemData architecture).
    /// Registers itself into a static registry so OrderManager can resolve
    /// "pickup_a" -> scene object without direct references.
    /// </summary>
    public abstract class InteractionPoint : MonoBehaviour, IInteractable
    {
        [Header("Identity")]
        [Tooltip("Must match OrderData.pickupPointId / deliveryPointId.")]
        [SerializeField] private string pointId;

        [Tooltip("Tabelada gösterilecek isim. Boşsa pointId kullanılır.")]
        [SerializeField] private string displayName;

        [Header("Visuals")]
        [Tooltip("ACTIVE-TARGET highlight (e.g. bright ground ring). Toggled on while this point is the current order target.")]
        [SerializeField] private GameObject markerVisual;
        [Tooltip("ALWAYS-VISIBLE identity marker (yer adını gösteren tabelalı durak). Never hidden — players can always see where pickup/delivery locations are.")]
        [SerializeField] private GameObject permanentBeacon;

        private static readonly Dictionary<string, InteractionPoint> registry = new Dictionary<string, InteractionPoint>();

        public string PointId => pointId;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? pointId : displayName;

        protected abstract Color GizmoColor { get; }

        public static bool TryGetPoint(string id, out InteractionPoint point)
        {
            point = null;
            return !string.IsNullOrEmpty(id) && registry.TryGetValue(id, out point);
        }

        protected virtual void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(pointId))
            {
                Debug.LogWarning($"[InteractionPoint] '{name}' pointId boş — sipariş sistemi bu noktayı bulamaz.");
                return;
            }

            if (registry.TryGetValue(pointId, out InteractionPoint existing) && existing != this && existing != null)
            {
                Debug.LogWarning($"[InteractionPoint] Çakışan pointId '{pointId}': '{existing.name}' ve '{name}'. İkincisi kayıt edilmedi.");
                return;
            }

            registry[pointId] = this;
            SetMarkerActive(false);

            // The permanent beacon must ALWAYS stay visible (design rule: locations
            // never disappear, even after pickup/delivery).
            if (permanentBeacon != null)
            {
                permanentBeacon.SetActive(true);
            }
        }

        protected virtual void OnDisable()
        {
            if (!string.IsNullOrWhiteSpace(pointId) &&
                registry.TryGetValue(pointId, out InteractionPoint existing) && existing == this)
            {
                registry.Remove(pointId);
            }
        }

        public void SetMarkerActive(bool active)
        {
            if (markerVisual != null)
            {
                markerVisual.SetActive(active);
            }
        }

        public abstract void Interact(GameObject interactor);
        public abstract string GetInteractionPrompt();

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(transform.position, 2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4.5f,
                $"{GetType().Name}\n[{pointId}]\n{DisplayName}");
#endif
        }
    }
}
