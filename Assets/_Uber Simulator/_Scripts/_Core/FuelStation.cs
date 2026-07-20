using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Refuel interaction: fills as many liters as the player can afford
    /// (partial refuel allowed). Needs a trigger collider so VehicleInteractor
    /// can detect it.
    /// </summary>
    public class FuelStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private float pricePerLiter = 3f;

        public void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }

            VehicleFuel fuel = interactor.GetComponentInParent<VehicleFuel>();
            if (fuel == null)
            {
                Debug.LogWarning("[FuelStation] Etkileşen objede VehicleFuel yok.");
                return;
            }

            if (EconomyManager.Instance == null)
            {
                Debug.LogWarning("[FuelStation] EconomyManager sahnede yok.");
                return;
            }

            float neededLiters = fuel.Capacity - fuel.CurrentFuel;
            if (neededLiters <= 0.01f)
            {
                NotificationService.Raise("Depo zaten dolu.");
                return;
            }

            float balance = EconomyManager.Instance.CurrentBalance;
            float affordableLiters = pricePerLiter > 0f ? balance / pricePerLiter : neededLiters;
            float liters = Mathf.Min(neededLiters, affordableLiters);

            if (liters <= 0.01f)
            {
                NotificationService.Raise("Yakıt için paran yok!");
                return;
            }

            float cost = liters * pricePerLiter;
            if (EconomyManager.Instance.SpendMoney(cost))
            {
                fuel.Refuel(liters);
                NotificationService.Raise($"{liters:F1} litre yakıt alındı (-{cost:F0} para).");
            }
        }

        public string GetInteractionPrompt()
        {
            return $"Yakıt Al [E] ({pricePerLiter:F1}/litre)";
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.8f, 0.1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.5f, new Vector3(2f, 3f, 2f));

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3.5f, "FuelStation");
#endif
        }
    }
}
