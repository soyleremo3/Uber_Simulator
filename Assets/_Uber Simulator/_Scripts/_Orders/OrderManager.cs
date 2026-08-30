using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliverySim
{
    public enum OrderPhase
    {
        None,           // No active order
        AwaitingPickup, // Order accepted, driving to pickup
        Delivering      // Cargo on board, timer running
    }

    /// <summary>Outcome of a finished delivery, consumed by UI and ReputationManager.</summary>
    public class DeliveryResult
    {
        public OrderData Order;
        public float Stars;   // 1..5
        public float Payout;  // Final amount added to balance
        public bool OnTime;
        public bool Failed;   // True when the order timed out entirely
    }

    /// <summary>
    /// Core loop owner: offer pool -> accept -> pickup -> deliver -> score/pay -> new offers.
    /// Scene points are resolved through InteractionPoint's ID registry, never by
    /// direct object references (project architecture rule).
    /// Timer note: the countdown starts at PICKUP (delivery leg is the timed part).
    /// </summary>
    public class OrderManager : MonoBehaviour
    {
        public static OrderManager Instance { get; private set; }

        [Header("Order Pool")]
        [Tooltip("All order definitions this scene can offer.")]
        [SerializeField] private List<OrderData> orderPool = new List<OrderData>();

        [Header("Offers")]
        [SerializeField] private int maxOffers = 3;
        [Tooltip("Seconds between automatic offer refills.")]
        [SerializeField] private float offerRefreshInterval = 15f;

        [Header("Time Limit (distance-based)")]
        [Tooltip("When on, the delivery time limit is computed from the real pickup->delivery distance instead of OrderData's fixed value.")]
        [SerializeField] private bool useDistanceBasedTimeLimit = true;
        [Tooltip("Average speed the player is expected to sustain (km/h).")]
        [SerializeField] private float averageSpeedKmh = 40f;
        [Tooltip("Straight-line distance is multiplied by this to account for winding roads.")]
        [SerializeField] private float routeFactor = 1.4f;
        [Tooltip("Flat extra seconds for stopping, turning around, parking.")]
        [SerializeField] private float timeBufferSeconds = 20f;
        [Tooltip("No order gets less time than this.")]
        [SerializeField] private float minTimeLimitSeconds = 45f;

        [Header("Payment (distance-based)")]
        [Tooltip("Açıkken ödeme = taban ücret + km başına ücret × iş mesafesi. Kapalıyken OrderData.PaymentAmount kullanılır.")]
        [SerializeField] private bool useDistanceBasedPayment = true;
        [Tooltip("Her işin sabit taban ücreti (mesafeden bağımsız).")]
        [SerializeField] private float baseFare = 15f;
        [Tooltip("İş mesafesinin (pickup->delivery) her kilometresi için eklenen ücret.")]
        [SerializeField] private float paymentPerKm = 12f;

        [Header("Scoring")]
        [Tooltip("Extra late time allowed before the order fails, as a fraction of the time limit. 0.5 = half the limit again.")]
        [SerializeField] private float lateGraceFactor = 0.5f;
        [SerializeField] private float minStars = 1f;
        [SerializeField] private float maxStars = 5f;
        [Tooltip("Fraction of the payment still paid at the very last late moment.")]
        [Range(0f, 1f)][SerializeField] private float latePayFraction = 0.4f;

        [Header("Variety")]
        [Tooltip("How many recently accepted/rotated-out orders are avoided when refilling offers (as long as other candidates exist).")]
        [SerializeField] private int recentHistoryLimit = 3;

        private readonly List<OrderData> currentOffers = new List<OrderData>();
        private readonly List<OrderData> recentHistory = new List<OrderData>();
        private OrderData activeOrder;
        private OrderPhase phase = OrderPhase.None;
        private float remainingTime;
        private float activeTimeLimit; // Resolved at accept time (distance-based or OrderData fallback)
        private float offerTimer;

        public event Action<IReadOnlyList<OrderData>> OnOffersChanged;
        public event Action<OrderData> OnOrderAccepted;
        public event Action<OrderData> OnCargoPickedUp;
        public event Action<DeliveryResult> OnOrderCompleted;
        public event Action<OrderData> OnOrderFailed;
        /// <summary>Remaining delivery seconds; fires only while the timer runs. Negative = late.</summary>
        public event Action<float> OnTimerTick;

        public OrderData ActiveOrder => activeOrder;
        public OrderPhase Phase => phase;
        public IReadOnlyList<OrderData> CurrentOffers => currentOffers;
        public float RemainingTime => remainingTime;
        /// <summary>Time limit resolved at accept time for the active order; 0 when idle. Used by the HUD timer bar.</summary>
        public float ActiveTimeLimit => activeTimeLimit;

        /// <summary>World position of the current target point (pickup or delivery), null when idle.</summary>
        public Vector3? CurrentTargetPosition
        {
            get
            {
                InteractionPoint point = GetCurrentTargetPoint();
                return point != null ? point.transform.position : (Vector3?)null;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            offerTimer = offerRefreshInterval;
            RefreshOffers();
        }

        private void Update()
        {
            // Periodic rotation so the board keeps moving even if the player
            // never accepts/rejects anything — RefreshOffers alone only fills
            // empty slots, so untouched offers would otherwise sit forever.
            offerTimer -= Time.deltaTime;
            if (offerTimer <= 0f)
            {
                offerTimer = offerRefreshInterval;
                RotateOffers();
            }

            if (phase == OrderPhase.Delivering && activeOrder != null)
            {
                remainingTime -= Time.deltaTime;
                OnTimerTick?.Invoke(remainingTime);

                float failThreshold = -activeTimeLimit * lateGraceFactor;
                if (remainingTime < failThreshold)
                {
                    FailActiveOrder("Süre doldu, sipariş iptal edildi!");
                }
            }
        }

        /// <summary>
        /// Approx. real road distance (metres) of the job pickup->delivery: straight
        /// line inflated by routeFactor. Returns -1 when the scene points can't be
        /// resolved. Shared by the time-limit estimate, the payout calc and the
        /// offer card so all three agree on "how far is this job".
        /// </summary>
        public float GetOrderDistance(OrderData order)
        {
            if (order == null ||
                !InteractionPoint.TryGetPoint(order.PickupPointId, out InteractionPoint pickup) ||
                !InteractionPoint.TryGetPoint(order.DeliveryPointId, out InteractionPoint delivery))
            {
                return -1f;
            }

            return Vector3.Distance(pickup.transform.position, delivery.transform.position) * routeFactor;
        }

        /// <summary>
        /// Payout for an order BEFORE lateness and reputation multipliers.
        /// Distance-based when enabled (base fare + per-km × job distance), otherwise
        /// the authored OrderData.PaymentAmount. Also used by the offer card.
        /// </summary>
        public float GetOrderPayment(OrderData order)
        {
            if (order == null)
            {
                return 0f;
            }

            float distance = GetOrderDistance(order);
            if (!useDistanceBasedPayment || distance < 0f)
            {
                return order.PaymentAmount;
            }

            return baseFare + paymentPerKm * (distance / 1000f);
        }

        /// <summary>
        /// Time limit for an order: real pickup->delivery distance / expected speed
        /// (+ buffer), clamped to a minimum. Falls back to OrderData's fixed value
        /// when disabled or when the scene points can't be resolved. Also used by
        /// the order panel to show the estimate on offer cards.
        /// </summary>
        public float GetEstimatedTimeLimit(OrderData order)
        {
            if (order == null)
            {
                return 0f;
            }

            float distance = GetOrderDistance(order);
            if (!useDistanceBasedTimeLimit || distance < 0f)
            {
                return order.TimeLimitSeconds;
            }

            float speedMs = Mathf.Max(1f, averageSpeedKmh / 3.6f);
            float travelTime = distance / speedMs;
            return Mathf.Max(minTimeLimitSeconds, travelTime + timeBufferSeconds);
        }

        // ---------- Offers ----------

        public void RefreshOffers()
        {
            bool changed = false;

            for (int i = currentOffers.Count - 1; i >= 0; i--)
            {
                if (currentOffers[i] == null)
                {
                    currentOffers.RemoveAt(i);
                    changed = true;
                }
            }

            var candidates = new List<OrderData>();
            foreach (OrderData order in orderPool)
            {
                if (order == null || order == activeOrder || currentOffers.Contains(order))
                {
                    continue;
                }

                if (ReputationManager.Instance != null &&
                    !ReputationManager.Instance.IsOrderUnlocked(order))
                {
                    continue;
                }

                candidates.Add(order);
            }

            // Prefer orders outside recent history so the same offer doesn't
            // instantly reappear the moment its slot frees up; fall back to the
            // full candidate list when there isn't enough variety left.
            var preferred = new List<OrderData>();
            foreach (OrderData candidate in candidates)
            {
                if (!recentHistory.Contains(candidate))
                {
                    preferred.Add(candidate);
                }
            }

            while (currentOffers.Count < maxOffers && candidates.Count > 0)
            {
                List<OrderData> pool = preferred.Count > 0 ? preferred : candidates;
                int index = UnityEngine.Random.Range(0, pool.Count);
                OrderData picked = pool[index];

                currentOffers.Add(picked);
                candidates.Remove(picked);
                preferred.Remove(picked);
                changed = true;
            }

            if (changed)
            {
                OnOffersChanged?.Invoke(currentOffers);
            }
        }

        /// <summary>Retires the longest-standing offer and tries to replace it with a fresh candidate. Called on a timer, independent of player accept/reject.</summary>
        public void RotateOffers()
        {
            if (currentOffers.Count == 0)
            {
                RefreshOffers();
                return;
            }

            OrderData rotatedOut = currentOffers[0];
            currentOffers.RemoveAt(0);
            RememberRecent(rotatedOut);

            RefreshOffers();

            if (currentOffers.Count < maxOffers)
            {
                // No fresh candidate available (small pool) — keep the offer count stable.
                currentOffers.Add(rotatedOut);
                OnOffersChanged?.Invoke(currentOffers);
            }
        }

        private void RememberRecent(OrderData order)
        {
            if (order == null)
            {
                return;
            }

            recentHistory.Remove(order);
            recentHistory.Add(order);

            while (recentHistory.Count > Mathf.Max(0, recentHistoryLimit))
            {
                recentHistory.RemoveAt(0);
            }
        }

        public void AcceptOffer(OrderData order)
        {
            if (order == null || !currentOffers.Contains(order))
            {
                return;
            }

            if (activeOrder != null)
            {
                NotificationService.Raise("Zaten aktif bir siparişin var.");
                return;
            }

            if (!InteractionPoint.TryGetPoint(order.PickupPointId, out InteractionPoint pickup) ||
                !InteractionPoint.TryGetPoint(order.DeliveryPointId, out InteractionPoint delivery))
            {
                Debug.LogError($"[OrderManager] '{order.OrderId}' için sahnede nokta bulunamadı " +
                               $"(pickup: '{order.PickupPointId}', delivery: '{order.DeliveryPointId}').");
                NotificationService.Raise("Sipariş noktaları sahnede eksik!");
                return;
            }

            activeOrder = order;
            phase = OrderPhase.AwaitingPickup;
            activeTimeLimit = GetEstimatedTimeLimit(order);
            currentOffers.Remove(order);
            RememberRecent(order);

            pickup.SetMarkerActive(true);
            delivery.SetMarkerActive(false);
            if (RouteManager.Instance != null)
            {
                RouteManager.Instance.SetDestination(pickup.transform.position);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.OrderActive);
            }

            OnOffersChanged?.Invoke(currentOffers);
            OnOrderAccepted?.Invoke(order);
            NotificationService.Raise($"Sipariş kabul edildi: {order.OrderName}. Alım noktasına git!");
        }

        public void RejectOffer(OrderData order)
        {
            if (order != null && currentOffers.Remove(order))
            {
                OnOffersChanged?.Invoke(currentOffers);
            }
        }

        // ---------- Pickup / Deliver (called by scene points) ----------

        public bool IsCurrentPickupTarget(PickupPoint point)
        {
            return activeOrder != null && phase == OrderPhase.AwaitingPickup &&
                   point != null && point.PointId == activeOrder.PickupPointId;
        }

        public bool IsCurrentDeliveryTarget(DeliveryPoint point)
        {
            return activeOrder != null && phase == OrderPhase.Delivering &&
                   point != null && point.PointId == activeOrder.DeliveryPointId;
        }

        public void TryPickup(PickupPoint point)
        {
            if (!IsCurrentPickupTarget(point))
            {
                return;
            }

            phase = OrderPhase.Delivering;
            remainingTime = activeTimeLimit;

            point.SetMarkerActive(false);
            if (InteractionPoint.TryGetPoint(activeOrder.DeliveryPointId, out InteractionPoint delivery))
            {
                delivery.SetMarkerActive(true);
                if (RouteManager.Instance != null)
                {
                    RouteManager.Instance.SetDestination(delivery.transform.position);
                }
            }

            OnCargoPickedUp?.Invoke(activeOrder);
            NotificationService.Raise($"Yük alındı! Teslimat için {Mathf.RoundToInt(remainingTime)} saniyen var.");
        }

        public void TryDeliver(DeliveryPoint point)
        {
            if (!IsCurrentDeliveryTarget(point))
            {
                return;
            }

            bool onTime = remainingTime >= 0f;
            float stars;
            float payFactor;

            if (onTime)
            {
                stars = maxStars;
                payFactor = 1f;
            }
            else
            {
                // Linear falloff across the late-grace window.
                float lateWindow = Mathf.Max(0.01f, activeTimeLimit * lateGraceFactor);
                float lateT = Mathf.Clamp01(-remainingTime / lateWindow);
                stars = Mathf.Lerp(maxStars, minStars, lateT);
                payFactor = Mathf.Lerp(1f, latePayFraction, lateT);
            }

            float reputationMultiplier = ReputationManager.Instance != null
                ? ReputationManager.Instance.CurrentPaymentMultiplier
                : 1f;

            float payout = GetOrderPayment(activeOrder) * payFactor * reputationMultiplier;

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddMoney(payout);
            }

            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.RegisterDelivery(stars);
            }

            var result = new DeliveryResult
            {
                Order = activeOrder,
                Stars = stars,
                Payout = payout,
                OnTime = onTime,
                Failed = false
            };

            NotificationService.Raise(onTime
                ? $"Teslimat tamamlandı! +{payout:F0} para, {stars:F1} yıldız."
                : $"Geç teslimat. +{payout:F0} para, {stars:F1} yıldız.");

            ClearActiveOrder();
            OnOrderCompleted?.Invoke(result);
            RefreshOffers();
        }

        // ---------- Failure / cleanup ----------

        private void FailActiveOrder(string reason)
        {
            OrderData failed = activeOrder;

            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.RegisterDelivery(minStars);
            }

            NotificationService.Raise(reason);
            ClearActiveOrder();
            OnOrderFailed?.Invoke(failed);
            RefreshOffers();
        }

        private void ClearActiveOrder()
        {
            if (activeOrder != null)
            {
                if (InteractionPoint.TryGetPoint(activeOrder.PickupPointId, out InteractionPoint pickup))
                {
                    pickup.SetMarkerActive(false);
                }

                if (InteractionPoint.TryGetPoint(activeOrder.DeliveryPointId, out InteractionPoint delivery))
                {
                    delivery.SetMarkerActive(false);
                }
            }

            if (RouteManager.Instance != null)
            {
                RouteManager.Instance.ClearDestination();
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OrderActive)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
            }

            activeOrder = null;
            phase = OrderPhase.None;
            remainingTime = 0f;
            activeTimeLimit = 0f;
        }

        private InteractionPoint GetCurrentTargetPoint()
        {
            if (activeOrder == null)
            {
                return null;
            }

            string targetId = phase == OrderPhase.AwaitingPickup
                ? activeOrder.PickupPointId
                : activeOrder.DeliveryPointId;

            InteractionPoint point;
            return InteractionPoint.TryGetPoint(targetId, out point) ? point : null;
        }
    }
}
