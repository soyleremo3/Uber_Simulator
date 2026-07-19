using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Bridges ShopManager upgrade state onto the vehicle's components:
    ///   Engine     -> VehicleController.upgradePowerMultiplier
    ///   FuelTank   -> VehicleFuel.SetCapacityMultiplier
    ///   Durability -> VehicleCondition.SetDurabilityMultiplier
    /// Applies current levels on Start (covers save-load) and listens for purchases.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleUpgradeApplier : MonoBehaviour
    {
        private VehicleController controller;
        private VehicleFuel fuel;
        private VehicleCondition condition;

        private void Awake()
        {
            controller = GetComponent<VehicleController>();
            fuel = GetComponent<VehicleFuel>();
            condition = GetComponent<VehicleCondition>();
        }

        private void Start()
        {
            ApplyAll();

            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnUpgradePurchased += HandleUpgradePurchased;
            }
        }

        private void OnDestroy()
        {
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
            }
        }

        private void HandleUpgradePurchased(VehicleUpgradeData upgrade, int newLevel)
        {
            ApplyAll();
        }

        public void ApplyAll()
        {
            if (ShopManager.Instance == null)
            {
                return;
            }

            if (controller != null)
            {
                controller.upgradePowerMultiplier =
                    ShopManager.Instance.GetCurrentMultiplier(UpgradeCategory.Engine);
            }

            if (fuel != null)
            {
                fuel.SetCapacityMultiplier(
                    ShopManager.Instance.GetCurrentMultiplier(UpgradeCategory.FuelTank));
            }

            if (condition != null)
            {
                condition.SetDurabilityMultiplier(
                    ShopManager.Instance.GetCurrentMultiplier(UpgradeCategory.Durability));
            }
        }
    }
}
