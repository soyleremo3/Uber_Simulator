using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeliverySim
{
    /// <summary>
    /// Shop screen: one row per upgrade category (Engine/FuelTank/Durability) plus
    /// purchasable vehicles. Rows are rebuilt on purchase/money events only.
    /// Toggle key: B. Opening the shop sets GameState.Shop, closing restores Playing.
    /// </summary>
    public class ShopPanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform listContainer;

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.B;

        [Header("Layout")]
        [SerializeField] private float rowHeight = 72f;
        [SerializeField] private float rowSpacing = 8f;

        private readonly List<GameObject> rows = new List<GameObject>();
        private bool subscribed;

        public void SetReferences(GameObject root, RectTransform container)
        {
            panelRoot = root;
            listContainer = container;
        }

        private void Start()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            TrySubscribe();
            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (subscribed && ShopManager.Instance != null)
            {
                ShopManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
                ShopManager.Instance.OnVehiclePurchased -= HandleVehiclePurchased;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                TrySubscribe();
                bool show = !panelRoot.activeSelf;
                panelRoot.SetActive(show);

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetGameState(show ? GameState.Shop : GameState.Playing);
                }

                if (show)
                {
                    Rebuild();
                }
            }
        }

        private void TrySubscribe()
        {
            if (subscribed || ShopManager.Instance == null)
            {
                return;
            }

            ShopManager.Instance.OnUpgradePurchased += HandleUpgradePurchased;
            ShopManager.Instance.OnVehiclePurchased += HandleVehiclePurchased;
            subscribed = true;
        }

        private void HandleUpgradePurchased(VehicleUpgradeData upgrade, int level) => Rebuild();
        private void HandleVehiclePurchased(VehicleData vehicle) => Rebuild();

        private void Rebuild()
        {
            foreach (GameObject row in rows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }

            rows.Clear();

            if (listContainer == null || ShopManager.Instance == null)
            {
                return;
            }

            int index = 0;

            foreach (UpgradeCategory category in Enum.GetValues(typeof(UpgradeCategory)))
            {
                rows.Add(BuildUpgradeRow(category, index));
                index++;
            }

            foreach (VehicleData vehicle in ShopManager.Instance.VehicleCatalog)
            {
                if (vehicle == null)
                {
                    continue;
                }

                rows.Add(BuildVehicleRow(vehicle, index));
                index++;
            }
        }

        private GameObject BuildUpgradeRow(UpgradeCategory category, int index)
        {
            RectTransform row = CreateRow(index, out Text info);

            int level = ShopManager.Instance.GetUpgradeLevel(category);
            VehicleUpgradeData next = ShopManager.Instance.GetNextUpgrade(category);

            string categoryName = CategoryDisplayName(category);
            if (next != null)
            {
                info.text = $"{categoryName} — Seviye {level}\nSonraki: {next.DisplayName} (₺{next.Cost:F0})";
                Button buy = UIFactory.CreateButton(row, "BuyButton", "Satın Al", UIFactory.AcceptColor,
                    () => ShopManager.Instance?.PurchaseNextUpgrade(category));
                UIFactory.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f),
                    new Vector2(-10f, 0f), new Vector2(110f, 40f));
            }
            else
            {
                info.text = $"{categoryName} — Seviye {level} (MAKS)";
            }

            return row.gameObject;
        }

        private GameObject BuildVehicleRow(VehicleData vehicle, int index)
        {
            RectTransform row = CreateRow(index, out Text info);

            if (ShopManager.Instance.IsVehicleOwned(vehicle.VehicleId))
            {
                info.text = $"{vehicle.DisplayName} — SAHİPSİN";
            }
            else
            {
                info.text = $"{vehicle.DisplayName}\nFiyat: ₺{vehicle.Price:F0}";
                Button buy = UIFactory.CreateButton(row, "BuyButton", "Satın Al", UIFactory.AcceptColor,
                    () => ShopManager.Instance?.PurchaseVehicle(vehicle));
                UIFactory.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f),
                    new Vector2(-10f, 0f), new Vector2(110f, 40f));
            }

            return row.gameObject;
        }

        private RectTransform CreateRow(int index, out Text info)
        {
            RectTransform row = UIFactory.CreatePanel(listContainer, $"ShopRow_{index}", UIFactory.RowColor);
            UIFactory.Place(row, new Vector2(0.5f, 1f),
                new Vector2(0f, -(index * (rowHeight + rowSpacing)) - rowSpacing),
                new Vector2(460f, rowHeight));

            info = UIFactory.CreateText(row, "Info", string.Empty, 18, TextAnchor.MiddleLeft, Color.white);
            UIFactory.Place((RectTransform)info.transform, new Vector2(0f, 0.5f),
                new Vector2(12f, 0f), new Vector2(300f, rowHeight - 12f));
            return row;
        }

        private static string CategoryDisplayName(UpgradeCategory category)
        {
            switch (category)
            {
                case UpgradeCategory.Engine: return "Motor";
                case UpgradeCategory.FuelTank: return "Yakıt Deposu";
                case UpgradeCategory.Durability: return "Dayanıklılık";
                default: return category.ToString();
            }
        }
    }
}
