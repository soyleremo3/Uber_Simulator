using System;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Fuel tank + consumption for one vehicle. Consumption scales with throttle
    /// and speed; an empty tank cuts engine power via
    /// VehicleController.fuelPowerMultiplier. Refueling happens at FuelStation.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleFuel : MonoBehaviour
    {
        [Header("Tank")]
        [Tooltip("Base capacity in liters. FuelTank upgrades multiply this.")]
        [SerializeField] private float baseCapacity = 45f;
        [SerializeField] private float startingFuel = 45f;

        [Header("Consumption (liters per second)")]
        [SerializeField] private float idleConsumption = 0.005f;
        [Tooltip("Extra consumption at full throttle.")]
        [SerializeField] private float throttleConsumption = 0.05f;
        [Tooltip("Extra consumption per 100 km/h of speed.")]
        [SerializeField] private float speedConsumptionPer100Kph = 0.02f;

        private VehicleController controller;
        private float capacityMultiplier = 1f;
        private float currentFuel;

        public float Capacity => baseCapacity * capacityMultiplier;
        public float CurrentFuel => currentFuel;
        public bool HasFuel => currentFuel > 0f;

        /// <summary>(current, capacity) — UI listens to this.</summary>
        public event Action<float, float> OnFuelChanged;
        public event Action OnFuelEmpty;

        private void Awake()
        {
            controller = GetComponent<VehicleController>();
            currentFuel = Mathf.Clamp(startingFuel, 0f, Capacity);
        }

        private void Start()
        {
            OnFuelChanged?.Invoke(currentFuel, Capacity);
        }

        private void Update()
        {
            if (currentFuel <= 0f)
            {
                return;
            }

            float throttle = Mathf.Abs(controller.userInput.y);
            float speedKph = controller.CurrentSpeedKph;

            float consumption = idleConsumption
                + throttleConsumption * throttle
                + speedConsumptionPer100Kph * (speedKph / 100f);

            currentFuel = Mathf.Max(0f, currentFuel - consumption * Time.deltaTime);
            OnFuelChanged?.Invoke(currentFuel, Capacity);

            if (currentFuel <= 0f)
            {
                controller.fuelPowerMultiplier = 0f; // Engine cut — car coasts to a stop.
                OnFuelEmpty?.Invoke();
                NotificationService.Raise("Yakıt bitti! En yakın istasyona ulaşman gerek.");
            }
            else
            {
                controller.fuelPowerMultiplier = 1f;
            }
        }

        /// <summary>Adds fuel (station pays-per-liter logic lives in FuelStation).</summary>
        public void Refuel(float liters)
        {
            if (liters <= 0f)
            {
                return;
            }

            currentFuel = Mathf.Min(Capacity, currentFuel + liters);
            if (currentFuel > 0f)
            {
                controller.fuelPowerMultiplier = 1f;
            }

            OnFuelChanged?.Invoke(currentFuel, Capacity);
        }

        /// <summary>Called by VehicleUpgradeApplier when the FuelTank upgrade changes.</summary>
        public void SetCapacityMultiplier(float multiplier)
        {
            capacityMultiplier = Mathf.Max(0.1f, multiplier);
            currentFuel = Mathf.Min(currentFuel, Capacity);
            OnFuelChanged?.Invoke(currentFuel, Capacity);
        }
    }
}
