using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Purchases: vehicle upgrades (Engine / FuelTank / Durability levels) and new
    /// vehicles. All money flow goes through EconomyManager (project rule).
    /// Owned state is persisted by SaveSystem via the snapshot/restore API below.
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Catalogs")]
        [Tooltip("One asset per (category, level). Levels must start at 1 and increase by 1.")]
        [SerializeField] private List<VehicleUpgradeData> upgradeCatalog = new List<VehicleUpgradeData>();
        [SerializeField] private List<VehicleData> vehicleCatalog = new List<VehicleData>();

        [Header("Start State")]
        [Tooltip("Vehicle the player owns from the start.")]
        [SerializeField] private string startingVehicleId = "vehicle_van";

        private readonly Dictionary<UpgradeCategory, int> upgradeLevels = new Dictionary<UpgradeCategory, int>();
        private readonly List<string> ownedVehicleIds = new List<string>();

        /// <summary>(upgrade, new level)</summary>
        public event Action<VehicleUpgradeData, int> OnUpgradePurchased;
        public event Action<VehicleData> OnVehiclePurchased;
        public event Action<string> OnPurchaseFailed;

        public IReadOnlyList<VehicleUpgradeData> UpgradeCatalog => upgradeCatalog;
        public IReadOnlyList<VehicleData> VehicleCatalog => vehicleCatalog;
        public IReadOnlyList<string> OwnedVehicleIds => ownedVehicleIds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            foreach (UpgradeCategory category in Enum.GetValues(typeof(UpgradeCategory)))
            {
                upgradeLevels[category] = 0;
            }

            if (!string.IsNullOrEmpty(startingVehicleId) && !ownedVehicleIds.Contains(startingVehicleId))
            {
                ownedVehicleIds.Add(startingVehicleId);
            }
        }

        // ---------- Upgrades ----------

        public int GetUpgradeLevel(UpgradeCategory category)
        {
            return upgradeLevels.TryGetValue(category, out int level) ? level : 0;
        }

        /// <summary>Catalog entry for the NEXT level of a category; null when maxed or missing.</summary>
        public VehicleUpgradeData GetNextUpgrade(UpgradeCategory category)
        {
            int nextLevel = GetUpgradeLevel(category) + 1;
            foreach (VehicleUpgradeData upgrade in upgradeCatalog)
            {
                if (upgrade != null && upgrade.Category == category && upgrade.Level == nextLevel)
                {
                    return upgrade;
                }
            }

            return null;
        }

        /// <summary>Absolute effect multiplier for the currently owned level (1 when level 0).</summary>
        public float GetCurrentMultiplier(UpgradeCategory category)
        {
            int level = GetUpgradeLevel(category);
            if (level <= 0)
            {
                return 1f;
            }

            foreach (VehicleUpgradeData upgrade in upgradeCatalog)
            {
                if (upgrade != null && upgrade.Category == category && upgrade.Level == level)
                {
                    return upgrade.EffectMultiplier;
                }
            }

            return 1f;
        }

        public bool PurchaseNextUpgrade(UpgradeCategory category)
        {
            VehicleUpgradeData next = GetNextUpgrade(category);
            if (next == null)
            {
                OnPurchaseFailed?.Invoke("Bu kategori maksimum seviyede.");
                NotificationService.Raise("Bu kategori zaten maksimum seviyede.");
                return false;
            }

            if (EconomyManager.Instance == null || !EconomyManager.Instance.SpendMoney(next.Cost))
            {
                OnPurchaseFailed?.Invoke("Yetersiz bakiye.");
                NotificationService.Raise($"Yetersiz bakiye: {next.DisplayName} için {next.Cost:F0} gerekli.");
                return false;
            }

            upgradeLevels[category] = next.Level;
            OnUpgradePurchased?.Invoke(next, next.Level);
            NotificationService.Raise($"Satın alındı: {next.DisplayName} (Seviye {next.Level}).");
            return true;
        }

        // ---------- Vehicles ----------

        public bool IsVehicleOwned(string vehicleId)
        {
            return !string.IsNullOrEmpty(vehicleId) && ownedVehicleIds.Contains(vehicleId);
        }

        public bool PurchaseVehicle(VehicleData vehicle)
        {
            if (vehicle == null)
            {
                return false;
            }

            if (IsVehicleOwned(vehicle.VehicleId))
            {
                OnPurchaseFailed?.Invoke("Bu araca zaten sahipsin.");
                NotificationService.Raise("Bu araca zaten sahipsin.");
                return false;
            }

            if (EconomyManager.Instance == null || !EconomyManager.Instance.SpendMoney(vehicle.Price))
            {
                OnPurchaseFailed?.Invoke("Yetersiz bakiye.");
                NotificationService.Raise($"Yetersiz bakiye: {vehicle.DisplayName} için {vehicle.Price:F0} gerekli.");
                return false;
            }

            ownedVehicleIds.Add(vehicle.VehicleId);
            OnVehiclePurchased?.Invoke(vehicle);
            NotificationService.Raise($"Yeni araç satın alındı: {vehicle.DisplayName}!");
            return true;
        }

        // ---------- Save/Load API (used by SaveSystem) ----------

        public List<UpgradeEntry> GetUpgradeSnapshot()
        {
            var entries = new List<UpgradeEntry>();
            foreach (KeyValuePair<UpgradeCategory, int> pair in upgradeLevels)
            {
                entries.Add(new UpgradeEntry { category = pair.Key.ToString(), level = pair.Value });
            }

            return entries;
        }

        public List<string> GetOwnedVehiclesSnapshot()
        {
            return new List<string>(ownedVehicleIds);
        }

        public void RestoreState(List<UpgradeEntry> upgrades, List<string> vehicles)
        {
            if (upgrades != null)
            {
                foreach (UpgradeEntry entry in upgrades)
                {
                    if (entry != null && Enum.TryParse(entry.category, out UpgradeCategory category))
                    {
                        upgradeLevels[category] = Mathf.Max(0, entry.level);
                    }
                }
            }

            if (vehicles != null && vehicles.Count > 0)
            {
                ownedVehicleIds.Clear();
                ownedVehicleIds.AddRange(vehicles);
            }
        }
    }
}
