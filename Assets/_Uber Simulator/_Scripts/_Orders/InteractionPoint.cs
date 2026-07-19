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

        [Header("Visuals")]
        [Tooltip("Optional marker object (beacon/arrow) toggled while this point is the active target.")]
        [SerializeField] private GameObject markerVisual;

        private static readonly Dictionary<string, InteractionPoint> registry = new Dictionary<string, InteractionPoint>();

        public string PointId => pointId;

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

        protected virtual void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(transform.position, 2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4.5f,
                $"{GetType().Name}\n[{pointId}]");
#endif
        }
    }
}
