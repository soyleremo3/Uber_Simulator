using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Economy/shop metadata for a vehicle. NOTE: physics tuning intentionally
    /// lives on VehicleController inspector fields (project decision — the raycast
    /// suspension controller does not read a ScriptableObject). This asset is used
    /// by ShopManager (price, catalog), fuel system (capacity) and UI (display name).
    /// </summary>
    [CreateAssetMenu(fileName = "NewVehicle", menuName = "DeliverySim/Vehicle Data")]
    public class VehicleData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string vehicleId = "vehicle_000";
        [SerializeField] private string displayName = "Yeni Araç";

        [Header("Economy")]
        [SerializeField] private float price = 5000f;

        [Header("Stats (info + gameplay hooks)")]
        [SerializeField] private float topSpeedKph = 140f;
        [SerializeField] private float acceleration = 5f;
        [SerializeField] private float fuelCapacity = 45f;
        [Range(0f, 1f)][SerializeField] private float handling = 0.5f;
        [Range(0f, 1f)][SerializeField] private float durability = 0.5f;

        [Header("Optional")]
        [Tooltip("Prefab spawned in garage/scene when this vehicle is selected.")]
        [SerializeField] private GameObject vehiclePrefab;

        public string VehicleId => vehicleId;
        public string DisplayName => displayName;
        public float Price => price;
        public float TopSpeedKph => topSpeedKph;
        public float Acceleration => acceleration;
        public float FuelCapacity => fuelCapacity;
        public float Handling => handling;
        public float Durability => durability;
        public GameObject VehiclePrefab => vehiclePrefab;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                Debug.LogWarning($"[VehicleData] '{name}' vehicleId boş olamaz.");
            }

            if (price < 0f) price = 0f;
            if (fuelCapacity < 1f) fuelCapacity = 1f;
        }
    }
}
