using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Cargo pickup location. Interaction succeeds only when the active order's
    /// pickupPointId matches this point (checked by OrderManager).
    /// </summary>
    public class PickupPoint : InteractionPoint
    {
        protected override Color GizmoColor => new Color(0.2f, 0.8f, 0.2f);

        public override void Interact(GameObject interactor)
        {
            if (OrderManager.Instance == null)
            {
                Debug.LogWarning("[PickupPoint] OrderManager sahnede yok.");
                return;
            }

            OrderManager.Instance.TryPickup(this);
        }

        public override string GetInteractionPrompt()
        {
            if (OrderManager.Instance != null && OrderManager.Instance.IsCurrentPickupTarget(this))
            {
                return "Yükü Al [F]";
            }

            return string.Empty;
        }
    }
}
