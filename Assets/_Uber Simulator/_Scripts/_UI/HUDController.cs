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
        [SerializeField] private Text turnText;
        [SerializeField] private Text surgeText;
        [SerializeField] private Text streakText;

        [Header("Bars (all optional)")]
        [SerializeField] private Image fuelBar;
        [SerializeField] private Image conditionBar;
        [SerializeField] private Image timerBar;
        [SerializeField] private Image reputationBar;

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

        public void SetBars(Image fuel, Image condition, Image timer)
        {
            fuelBar = fuel;
            conditionBar = condition;
            timerBar = timer;
        }

        public void SetReputationBar(Image reputation)
        {
            reputationBar = reputation;
        }

        public void SetTurnIndicator(Text turn)
        {
            turnText = turn;
        }

        public void SetSurgeText(Text surge)
        {
            surgeText = surge;
        }

        public void SetStreakText(Text streak)
        {
            streakText = streak;
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

            if (turnText != null)
            {
                RouteManager.TurnDirection turn = RouteManager.Instance != null
                    ? RouteManager.Instance.NextTurn
                    : RouteManager.TurnDirection.None;
                turnText.text = TurnLabel(turn);
            }
        }

        /// <summary>Legible turn-by-turn cue — the route ribbon alone doesn't communicate direction on its own.</summary>
        private static string TurnLabel(RouteManager.TurnDirection direction)
        {
            switch (direction)
            {
                case RouteManager.TurnDirection.Left: return "◄ SOLA DÖN";
                case RouteManager.TurnDirection.Right: return "SAĞA DÖN ►";
                case RouteManager.TurnDirection.Straight: return "▲ DÜZ GİT";
                default: return string.Empty;
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
                OrderManager.Instance.OnPickupTimerTick += HandlePickupTimerTick;
                OrderManager.Instance.OnOrderAccepted += HandleOrderStateChanged;
                OrderManager.Instance.OnCargoPickedUp += HandleOrderStateChanged;
                OrderManager.Instance.OnOrderCompleted += HandleOrderCompleted;
                OrderManager.Instance.OnOrderFailed += HandleOrderStateChanged;
                OrderManager.Instance.OnSurgeChanged += HandleSurgeChanged;
                OrderManager.Instance.OnStreakChanged += HandleStreakChanged;
            }

            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.OnReputationChanged += HandleReputationChanged;
                ReputationManager.Instance.OnReputationProgress += HandleReputationProgress;
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
                OrderManager.Instance.OnPickupTimerTick -= HandlePickupTimerTick;
                OrderManager.Instance.OnOrderAccepted -= HandleOrderStateChanged;
                OrderManager.Instance.OnCargoPickedUp -= HandleOrderStateChanged;
                OrderManager.Instance.OnOrderCompleted -= HandleOrderCompleted;
                OrderManager.Instance.OnOrderFailed -= HandleOrderStateChanged;
                OrderManager.Instance.OnSurgeChanged -= HandleSurgeChanged;
                OrderManager.Instance.OnStreakChanged -= HandleStreakChanged;
            }

            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.OnReputationChanged -= HandleReputationChanged;
                ReputationManager.Instance.OnReputationProgress -= HandleReputationProgress;
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
                HandleReputationProgress(
                    ReputationManager.Instance.CurrentLevel,
                    ReputationManager.Instance.RPIntoCurrentLevel,
                    ReputationManager.Instance.RPForNextLevel);
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
            ResetTimerDisplay();
        }

        private void ResetTimerDisplay()
        {
            if (timerText != null)
            {
                timerText.text = string.Empty;
            }

            if (timerBar != null)
            {
                timerBar.fillAmount = 0f;
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
            float ratio = capacity > 0f ? Mathf.Clamp01(current / capacity) : 0f;

            if (fuelText != null)
            {
                fuelText.text = $"Yakıt: {current:F1} / {capacity:F0} L";
            }

            if (fuelBar != null)
            {
                fuelBar.fillAmount = ratio;
                fuelBar.color = UIFactory.BarColorForRatio(ratio);
            }
        }

        private void HandleConditionChanged(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (conditionText != null)
            {
                conditionText.text = $"Durum: %{ratio * 100f:F0}";
            }

            if (conditionBar != null)
            {
                conditionBar.fillAmount = ratio;
                conditionBar.color = UIFactory.BarColorForRatio(ratio);
            }
        }

        private void HandleTimerTick(float remaining)
        {
            float limit = OrderManager.Instance != null ? OrderManager.Instance.ActiveTimeLimit : 0f;
            float ratio = limit > 0f ? Mathf.Clamp01(remaining / limit) : 0f;

            if (timerBar != null)
            {
                timerBar.fillAmount = ratio;
                timerBar.color = remaining < 0f ? UIFactory.BarDangerColor : UIFactory.BarColorForRatio(ratio);
            }

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

        private void HandlePickupTimerTick(float remaining)
        {
            float limit = OrderManager.Instance != null ? OrderManager.Instance.ActivePickupTimeLimit : 0f;
            float ratio = limit > 0f ? Mathf.Clamp01(remaining / limit) : 0f;

            if (timerBar != null)
            {
                timerBar.fillAmount = ratio;
                timerBar.color = remaining < 0f ? UIFactory.BarDangerColor : UIFactory.BarColorForRatio(ratio);
            }

            if (timerText == null)
            {
                return;
            }

            if (remaining >= 0f)
            {
                int minutes = Mathf.FloorToInt(remaining / 60f);
                int seconds = Mathf.FloorToInt(remaining % 60f);
                timerText.text = $"Alım: {minutes:00}:{seconds:00}";
                timerText.color = remaining < 20f ? new Color(1f, 0.6f, 0.2f) : Color.white;
            }
            else
            {
                timerText.text = $"ALIM GECİKME: {Mathf.Abs(remaining):F0} sn";
                timerText.color = new Color(1f, 0.25f, 0.25f);
            }
        }

        private void HandleSurgeChanged(float multiplier)
        {
            if (surgeText != null)
            {
                surgeText.text = multiplier > 1.01f ? $"⚡ SURGE ×{multiplier:0.0}" : string.Empty;
            }
        }

        private void HandleStreakChanged(int streak)
        {
            if (streakText != null)
            {
                streakText.text = streak >= 2 ? $"🔥 ×{streak}" : string.Empty;
            }
        }

        private void HandleOrderStateChanged(OrderData order)
        {
            RefreshCargoState();
        }

        private void HandleOrderCompleted(DeliveryResult result)
        {
            RefreshCargoState();
            ResetTimerDisplay();
        }

        private void HandleReputationChanged(float average, ReputationTier tier)
        {
            if (reputationText != null)
            {
                int level = ReputationManager.Instance != null ? ReputationManager.Instance.CurrentLevel : 1;
                reputationText.text = $"Sv{level} {ReputationManager.TierDisplayName(tier)} ★{average:F1}";
            }
        }

        private void HandleReputationProgress(int level, int rpIntoLevel, int rpForNextLevel)
        {
            if (reputationBar != null)
            {
                reputationBar.fillAmount = rpForNextLevel > 0
                    ? Mathf.Clamp01((float)rpIntoLevel / rpForNextLevel)
                    : 0f;
            }
        }

        private void RefreshCargoState()
        {
            if (OrderManager.Instance == null || OrderManager.Instance.Phase == OrderPhase.None)
            {
                if (cargoText != null)
                {
                    cargoText.text = "Sipariş yok";
                }

                ResetTimerDisplay(); // No order -> clear any leftover (pickup/delivery) timer text.
                return;
            }

            if (cargoText != null)
            {
                OrderData order = OrderManager.Instance.ActiveOrder;
                cargoText.text = OrderManager.Instance.Phase == OrderPhase.AwaitingPickup
                    ? $"Alım bekleniyor: {order.OrderName}"
                    : $"Yük: {order.OrderName} ({order.CargoType.Label()})";
            }

            // During AwaitingPickup the pickup-timer tick drives the timer widget;
            // only clear it when there is no pickup limit running.
            if (OrderManager.Instance.Phase == OrderPhase.AwaitingPickup &&
                OrderManager.Instance.ActivePickupTimeLimit <= 0f)
            {
                ResetTimerDisplay();
            }
        }
    }
}
