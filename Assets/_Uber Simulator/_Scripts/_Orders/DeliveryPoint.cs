using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Cargo delivery location. Interaction succeeds only when the active order's
    /// deliveryPointId matches this point and the cargo has been picked up.
    /// </summary>
    public class DeliveryPoint : InteractionPoint
    {
        protected override Color GizmoColor => new Color(0.9f, 0.4f, 0.1f);

        public override void Interact(GameObject interactor)
        {
            if (OrderManager.Instance == null)
            {
                Debug.LogWarning("[DeliveryPoint] OrderManager sahnede yok.");
                return;
            }

            if (!InteractorIsStopped(interactor, out float speedKmh))
            {
                NotificationService.Raise($"Teslim etmek için dur (hız {speedKmh:F0} km/s).");
                return;
            }

            OrderManager.Instance.TryDeliver(this);
        }

        public override string GetInteractionPrompt()
        {
            if (OrderManager.Instance != null && OrderManager.Instance.IsCurrentDeliveryTarget(this))
            {
                return "Teslim Et [E]";
            }

            return string.Empty;
        }
    }
}
