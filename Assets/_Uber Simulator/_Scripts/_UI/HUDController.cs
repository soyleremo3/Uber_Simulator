using UnityEngine;
using UnityEngine.UI;

namespace DeliverySim
{
    /// <summary>
    /// Driving HUD: speed, money, fuel, condition, order timer, distance, cargo,
    /// reputation. Event-driven for everything except speed/distance, which are
    /// inherently continuous and refresh on a short interval instead of per-frame.
    /// All references optional (null-safe) so partial HUDs work.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Texts (all optional)")]
        [SerializeField] private Text speedText;
        [SerializeField] private Text moneyText;
        [SerializeField] private Text fuelText;
        [SerializeField] private Text conditionText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text distanceText;
        [SerializeField] private Text cargoText;
        [SerializeField] private Text reputationText;

        [Header("Refresh")]
        [Tooltip("Seconds between speed/distance refreshes.")]
        [SerializeField] private float continuousRefreshInterval = 0.1f;

        private VehicleController vehicle;
        private VehicleFuel vehicleFuel;
        private VehicleCondition vehicleCondition;
        private float refreshTimer;

        public void SetTexts(Text speed, Text money, Text fuel, Text condition,
            Text timer, Text distance, Text cargo, Text reputation)
        {
            speedText = speed;
            moneyText = money;
            fuelText = fuel;
            conditionText = condition;
            timerText = timer;
            distanceText = distance;
            cargoText = cargo;
            reputationText = reputation;
        }

        private void Start()
        {
            vehicle = FindFirstObjectByType<VehicleController>();
            if (vehicle != null)
            {
                vehicleFuel = vehicle.GetComponent<VehicleFuel>();
                vehicleCondition = vehicle.GetComponent<VehicleCondition>();
            }

            Subscribe();
            InitialRefresh();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer > 0f)
            {
                return;
            }

            refreshTimer = continuousRefreshInterval;

            if (speedText != null && vehicle != null)
            {
                speedText.text = $"{vehicle.CurrentSpeedKph:F0} km/s";
            }

            if (distanceText != null)
            {
                float distance = RouteManager.Instance != null
                    ? RouteManager.Instance.GetDistanceToDestination()
                    : -1f;
                distanceText.text = distance >= 0f ? $"Hedef: {distance:F0} m" : string.Empty;
            }
        }

        // ---------- Event wiring ----------

        private void Subscribe()
        {
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnMoneyChanged += HandleMoneyChanged;
            }

            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.OnTimerTick += HandleTimerTick;
                OrderManager.Instance.OnOrderAccepted += HandleOrderStateChanged;
                OrderManager.Instance.OnCargoPickedUp += HandleOrderStateChanged;
                OrderManager.Instance.OnOrderCompleted += HandleOrderCompleted;
                OrderManager.Instance.OnOrderFailed += HandleOrderStateChanged;
            }

            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.OnReputationChanged += HandleReputationChanged;
            }

            if (vehicleFuel != null)
            {
                vehicleFuel.OnFuelChanged += HandleFuelChanged;
            }

            if (vehicleCondition != null)
            {
                vehicleCondition.OnConditionChanged += HandleConditionChanged;
            }
        }

        private void Unsubscribe()
        {
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnMoneyChanged -= HandleMoneyChanged;
            }

            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.OnTimerTick -= HandleTimerTick;
                OrderManager.Instance.OnOrderAccepted -= HandleOrderStateChanged;
                OrderManager.Instance.OnCargoPickedUp -= HandleOrderStateChanged;
                OrderManager.Instance.OnOrderCompleted -= HandleOrderCompleted;
                OrderManager.Instance.OnOrderFailed -= HandleOrderStateChanged;
            }

            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.OnReputationChanged -= HandleReputationChanged;
            }

            if (vehicleFuel != null)
            {
                vehicleFuel.OnFuelChanged -= HandleFuelChanged;
            }

            if (vehicleCondition != null)
            {
                vehicleCondition.OnConditionChanged -= HandleConditionChanged;
            }
        }

        private void InitialRefresh()
        {
            if (EconomyManager.Instance != null)
            {
                HandleMoneyChanged(EconomyManager.Instance.CurrentBalance);
            }

            if (ReputationManager.Instance != null)
            {
                HandleReputationChanged(
                    ReputationManager.Instance.AverageScore, ReputationManager.Instance.CurrentTier);
            }

            if (vehicleFuel != null)
            {
                HandleFuelChanged(vehicleFuel.CurrentFuel, vehicleFuel.Capacity);
            }

            if (vehicleCondition != null)
            {
                HandleConditionChanged(vehicleCondition.CurrentCondition, vehicleCondition.MaxCondition);
            }

            RefreshCargoState();
            if (timerText != null)
            {
                timerText.text = string.Empty;
            }
        }

        // ---------- Handlers ----------

        private void HandleMoneyChanged(float balance)
        {
            if (moneyText != null)
            {
                moneyText.text = $"₺ {balance:F0}";
            }
        }

        private void HandleFuelChanged(float current, float capacity)
        {
            if (fuelText != null)
            {
                fuelText.text = $"Yakıt: {current:F1} / {capacity:F0} L";
            }
        }

        private void HandleConditionChanged(float current, float max)
        {
            if (conditionText != null)
            {
                conditionText.text = $"Durum: %{(max > 0f ? current / max * 100f : 0f):F0}";
            }
        }

        private void HandleTimerTick(float remaining)
        {
            if (timerText == null)
            {
                return;
            }

            if (remaining >= 0f)
            {
                int minutes = Mathf.FloorToInt(remaining / 60f);
                int seconds = Mathf.FloorToInt(remaining % 60f);
                timerText.text = $"Süre: {minutes:00}:{seconds:00}";
                timerText.color = remaining < 30f ? new Color(1f, 0.6f, 0.2f) : Color.white;
            }
            else
            {
                timerText.text = $"GECİKME: {Mathf.Abs(remaining):F0} sn";
                timerText.color = new Color(1f, 0.25f, 0.25f);
            }
        }

        private void HandleOrderStateChanged(OrderData order)
        {
            RefreshCargoState();
        }

        private void HandleOrderCompleted(DeliveryResult result)
        {
            RefreshCargoState();
            if (timerText != null)
            {
                timerText.text = string.Empty;
            }
        }

        private void HandleReputationChanged(float average, ReputationTier tier)
        {
            if (reputationText != null)
            {
                reputationText.text = $"★ {average:F1} ({ReputationManager.TierDisplayName(tier)})";
            }
        }

        private void RefreshCargoState()
        {
            if (cargoText == null)
            {
                return;
            }

            if (OrderManager.Instance == null || OrderManager.Instance.Phase == OrderPhase.None)
            {
                cargoText.text = "Sipariş yok";
                return;
            }

            OrderData order = OrderManager.Instance.ActiveOrder;
            cargoText.text = OrderManager.Instance.Phase == OrderPhase.AwaitingPickup
                ? $"Alım bekleniyor: {order.OrderName}"
                : $"Yük: {order.OrderName} ({order.CargoType})";

            if (timerText != null && OrderManager.Instance.Phase == OrderPhase.AwaitingPickup)
            {
                timerText.text = string.Empty;
            }
        }
    }
}
