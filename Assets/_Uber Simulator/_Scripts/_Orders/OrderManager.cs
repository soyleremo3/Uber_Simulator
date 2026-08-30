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
        public float Stars;   // 1..5 (final, jitter applied)
        public float RawStars; // pre-round score (for a future results card)
        public float Payout;  // Final amount added to balance
        public bool OnTime;
        public bool Failed;   // True when the order timed out entirely

        // Star-rating inputs (see docs/design/reputation-redesign.md A).
        public int Collisions;         // impacts during this delivery
        public float ConditionLost;    // vehicle condition drop pickup -> delivery
        public float PeakSpeedKph;
        public float JobDistanceMeters;
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
        [Tooltip("Panoda aynı anda görünebilecek en fazla teklif (her zaman dolu olmak zorunda değil).")]
        [SerializeField] private int maxOffers = 10;
        [Tooltip("Seconds between automatic offer refills (backfill; replaced by Poisson arrivals in a later step).")]
        [SerializeField] private float offerRefreshInterval = 15f;
        [Tooltip("Her teklif kendi TTL'i (sn) kadar panoda kalır, sonra kendiliğinden düşer.")]
        [SerializeField] private float offerTtlMin = 75f;
        [SerializeField] private float offerTtlMax = 150f;

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

        [Header("Pickup Time Limit")]
        [Tooltip("Sipariş kabul edildikten sonra ALIM noktasına ulaşmak için de ayrı bir süre. Dolarsa sipariş iptal edilir (itibar cezası yok).")]
        [SerializeField] private bool usePickupTimeLimit = true;
        [Tooltip("Açıkken alım süresi araç->alım mesafesinden (yukarıdaki averageSpeedKmh / routeFactor ile) hesaplanır; kapalıyken alttaki sabit değer.")]
        [SerializeField] private bool useDistanceBasedPickupTime = true;
        [SerializeField] private float pickupTimeLimitSeconds = 120f;
        [Tooltip("Alım süresine eklenen sabit tampon saniye.")]
        [SerializeField] private float pickupTimeBufferSeconds = 30f;
        [Tooltip("Hiçbir alım süresi bunun altına inmez.")]
        [SerializeField] private float minPickupTimeSeconds = 40f;
        [Tooltip("Alım süresi bittikten sonra iptalden önce tanınan ek süre (limitin kesri).")]
        [SerializeField] private float pickupLateGraceFactor = 0.5f;

        [Header("Scoring")]
        [Tooltip("Extra late time allowed before the order fails, as a fraction of the time limit. 0.5 = half the limit again.")]
        [SerializeField] private float lateGraceFactor = 0.5f;
        [SerializeField] private float minStars = 1f;
        [SerializeField] private float maxStars = 5f;
        [Tooltip("Fraction of the payment still paid at the very last late moment.")]
        [Range(0f, 1f)][SerializeField] private float latePayFraction = 0.4f;

        [Header("Star Rating (multi-factor — see reputation-redesign.md A)")]
        [Tooltip("Bu km/s üstünde geçirilen süre 'dikkatsiz sürüş' sayılır.")]
        [SerializeField] private float carefulSpeedThresholdKph = 90f;
        [Tooltip("Teslimat sırasındaki her çarpışma için yıldız cezası (kargo hassasiyetiyle çarpılır).")]
        [SerializeField] private float crashStarPenalty = 0.8f;
        [Tooltip("Kaybedilen her 1 condition puanı için yıldız cezası.")]
        [SerializeField] private float damageStarPenaltyPerPoint = 0.05f;
        [Tooltip("Aşırı hızdan gelebilecek en fazla yıldız cezası.")]
        [SerializeField] private float speedStarPenaltyMax = 0.6f;
        [Tooltip("Zamanında ama 'kıl payı' sayılan kalan-süre oranı (bunun altındaysa küçük ceza).")]
        [SerializeField] private float closeCallMarginFraction = 0.08f;
        [SerializeField] private float closeCallPenalty = 0.3f;
        [Tooltip("Kargo tipi hassasiyeti — indeks: Food, Package, Fragile.")]
        [SerializeField] private float[] cargoSensitivity = { 1.0f, 0.6f, 1.6f };
        [Tooltip("Son N tamamlanan rota (alım>teslim) hatırlanır; aynı rotayı tekrarlarsan RP çarpanı 1/(1+k*routeRepeatPenalty) ile düşer (farm önleme).")]
        [SerializeField] private int routeRepeatWindow = 5;
        [SerializeField] private float routeRepeatPenalty = 0.6f;

        [Header("Variety")]
        [Tooltip("How many recently accepted/rotated-out orders are avoided when refilling offers (as long as other candidates exist).")]
        [SerializeField] private int recentHistoryLimit = 3;

        private readonly List<OrderOffer> currentOffers = new List<OrderOffer>();
        private readonly List<OrderData> recentHistory = new List<OrderData>();
        private OrderData activeOrder;      // the accepted offer's template
        private OrderOffer activeOffer;     // the accepted offer instance (resolved points, pay, time)
        private OrderPhase phase = OrderPhase.None;
        private float remainingTime;
        private float activeTimeLimit; // Resolved at accept time (distance-based or OrderData fallback)
        private float remainingPickupTime;
        private float activePickupTimeLimit; // Resolved at accept time; 0 = no pickup limit
        private float offerTimer;
        private VehicleController cachedVehicle;
        private VehicleCondition cachedVehicleCondition;

        // Per-delivery star-rating snapshot (taken at pickup, diffed at delivery).
        private float conditionAtPickup = -1f;
        private int collisionsAtPickup;
        private float deliveryStartTime;
        private float speedingSeconds;
        private float peakSpeedKph;

        // Anti-farm: last few completed "pickupId>deliveryId" routes; repeating one damps its RP.
        private readonly List<string> recentRoutes = new List<string>();

        public event Action<IReadOnlyList<OrderOffer>> OnOffersChanged;
        public event Action<OrderData> OnOrderAccepted;
        public event Action<OrderData> OnCargoPickedUp;
        public event Action<DeliveryResult> OnOrderCompleted;
        public event Action<OrderData> OnOrderFailed;
        /// <summary>Remaining delivery seconds; fires only while the timer runs. Negative = late.</summary>
        public event Action<float> OnTimerTick;
        /// <summary>Remaining seconds to reach the pickup; fires only during AwaitingPickup when a pickup limit is set. Negative = late.</summary>
        public event Action<float> OnPickupTimerTick;

        public OrderData ActiveOrder => activeOrder;
        public OrderOffer ActiveOffer => activeOffer;
        public OrderPhase Phase => phase;
        public IReadOnlyList<OrderOffer> CurrentOffers => currentOffers;
        public float RemainingTime => remainingTime;
        /// <summary>Time limit resolved at accept time for the active order; 0 when idle. Used by the HUD timer bar.</summary>
        public float ActiveTimeLimit => activeTimeLimit;
        /// <summary>Seconds allowed to reach the pickup, resolved at accept time; 0 when idle or the pickup limit is disabled.</summary>
        public float ActivePickupTimeLimit => activePickupTimeLimit;
        public float RemainingPickupTime => remainingPickupTime;

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

            cachedVehicle = FindFirstObjectByType<VehicleController>();
            if (cachedVehicle != null)
            {
                cachedVehicleCondition = cachedVehicle.GetComponent<VehicleCondition>();
            }

            RefreshOffers();
        }

        private void Update()
        {
            // Each offer leaves the board on its own TTL (the board "churns"), then a
            // periodic backfill tops the count back up. The old fixed 15s "retire the
            // oldest" rotation is gone; Poisson arrivals replace the backfill later.
            TickOfferTtl();

            offerTimer -= Time.deltaTime;
            if (offerTimer <= 0f)
            {
                offerTimer = offerRefreshInterval;
                RefreshOffers();
            }

            if (phase == OrderPhase.AwaitingPickup && activeOrder != null && activePickupTimeLimit > 0f)
            {
                remainingPickupTime -= Time.deltaTime;
                OnPickupTimerTick?.Invoke(remainingPickupTime);

                float pickupFailThreshold = -activePickupTimeLimit * pickupLateGraceFactor;
                if (remainingPickupTime < pickupFailThreshold)
                {
                    FailActiveOrder("Alım süresi doldu, sipariş iptal edildi!", registerReputationHit: false);
                }
            }

            if (phase == OrderPhase.Delivering && activeOrder != null)
            {
                remainingTime -= Time.deltaTime;
                OnTimerTick?.Invoke(remainingTime);

                // Careful-driving sampling for the star rating.
                if (cachedVehicle != null)
                {
                    float kph = cachedVehicle.CurrentSpeedKph;
                    if (kph > peakSpeedKph)
                    {
                        peakSpeedKph = kph;
                    }

                    if (kph > carefulSpeedThresholdKph)
                    {
                        speedingSeconds += Time.deltaTime;
                    }
                }

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

        /// <summary>
        /// Seconds allowed to reach the pickup after accepting. Distance-based
        /// (vehicle position now -> pickup, same speed/routeFactor as the delivery
        /// leg) when enabled, else a flat value. Returns 0 when the pickup limit is
        /// disabled — callers treat 0 as "no limit".
        /// </summary>
        private float ResolvePickupTimeLimit(InteractionPoint pickup)
        {
            if (!usePickupTimeLimit)
            {
                return 0f;
            }

            if (!useDistanceBasedPickupTime || pickup == null)
            {
                return pickupTimeLimitSeconds;
            }

            Vector3 vehiclePos = GetVehiclePosition(pickup.transform.position);
            float distance = Vector3.Distance(vehiclePos, pickup.transform.position) * routeFactor;
            float speedMs = Mathf.Max(1f, averageSpeedKmh / 3.6f);
            return Mathf.Max(minPickupTimeSeconds, distance / speedMs + pickupTimeBufferSeconds);
        }

        private Vector3 GetVehiclePosition(Vector3 fallback)
        {
            if (cachedVehicle == null)
            {
                cachedVehicle = FindFirstObjectByType<VehicleController>();
            }

            return cachedVehicle != null ? cachedVehicle.transform.position : fallback;
        }

        // ---------- Offers ----------

        /// <summary>Counts down each offer's TTL; drops any that expire and refills.</summary>
        private void TickOfferTtl()
        {
            bool removed = false;

            for (int i = currentOffers.Count - 1; i >= 0; i--)
            {
                OrderOffer offer = currentOffers[i];
                if (offer == null)
                {
                    currentOffers.RemoveAt(i);
                    removed = true;
                    continue;
                }

                offer.Ttl -= Time.deltaTime;
                if (offer.Ttl <= 0f)
                {
                    RememberRecent(offer.Template);
                    currentOffers.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                OnOffersChanged?.Invoke(currentOffers);
                RefreshOffers();
            }
        }

        /// <summary>Backfills the board toward maxOffers from the template pool. (Poisson arrivals replace this in a later step.)</summary>
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
                if (order == null || order == activeOrder || OffersContainTemplate(order))
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

            // Prefer templates outside recent history so the same offer doesn't
            // instantly reappear; fall back to the full list when variety runs out.
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

                currentOffers.Add(BuildOffer(picked));
                candidates.Remove(picked);
                preferred.Remove(picked);
                changed = true;
            }

            if (changed)
            {
                OnOffersChanged?.Invoke(currentOffers);
            }
        }

        private bool OffersContainTemplate(OrderData template)
        {
            for (int i = 0; i < currentOffers.Count; i++)
            {
                if (currentOffers[i] != null && currentOffers[i].Template == template)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Builds a concrete offer from a template. Step 2: points come straight from
        /// the template; pay/time/distance are derived; TTL is randomised. Later steps
        /// add runtime point selection, a customer and difficulty rolls here.
        /// </summary>
        private OrderOffer BuildOffer(OrderData template)
        {
            var offer = new OrderOffer
            {
                Template = template,
                PickupPointId = template.PickupPointId,
                DeliveryPointId = template.DeliveryPointId,
                DisplayName = template.OrderName,
                Ttl = UnityEngine.Random.Range(offerTtlMin, offerTtlMax)
            };

            offer.DistanceMeters = Mathf.Max(0f, GetOrderDistance(template));
            offer.TimeLimit = GetEstimatedTimeLimit(template);
            offer.Payment = GetOrderPayment(template);
            return offer;
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

        public void AcceptOffer(OrderOffer offer)
        {
            if (offer == null || !currentOffers.Contains(offer))
            {
                return;
            }

            if (activeOrder != null)
            {
                NotificationService.Raise("Zaten aktif bir siparişin var.");
                return;
            }

            if (!InteractionPoint.TryGetPoint(offer.PickupPointId, out InteractionPoint pickup) ||
                !InteractionPoint.TryGetPoint(offer.DeliveryPointId, out InteractionPoint delivery))
            {
                Debug.LogError($"[OrderManager] '{offer.DisplayName}' için sahnede nokta bulunamadı " +
                               $"(pickup: '{offer.PickupPointId}', delivery: '{offer.DeliveryPointId}').");
                NotificationService.Raise("Sipariş noktaları sahnede eksik!");
                return;
            }

            activeOffer = offer;
            activeOrder = offer.Template;
            phase = OrderPhase.AwaitingPickup;
            activeTimeLimit = offer.TimeLimit;
            activePickupTimeLimit = ResolvePickupTimeLimit(pickup);
            remainingPickupTime = activePickupTimeLimit;
            currentOffers.Remove(offer);
            RememberRecent(offer.Template);

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
            OnOrderAccepted?.Invoke(activeOrder);
            NotificationService.Raise($"Sipariş kabul edildi: {offer.DisplayName}. Alım noktasına git!");
        }

        public void RejectOffer(OrderOffer offer)
        {
            if (offer != null && currentOffers.Remove(offer))
            {
                OnOffersChanged?.Invoke(currentOffers);
            }
        }

        // ---------- Pickup / Deliver (called by scene points) ----------

        public bool IsCurrentPickupTarget(PickupPoint point)
        {
            return activeOffer != null && phase == OrderPhase.AwaitingPickup &&
                   point != null && point.PointId == activeOffer.PickupPointId;
        }

        public bool IsCurrentDeliveryTarget(DeliveryPoint point)
        {
            return activeOffer != null && phase == OrderPhase.Delivering &&
                   point != null && point.PointId == activeOffer.DeliveryPointId;
        }

        public void TryPickup(PickupPoint point)
        {
            if (!IsCurrentPickupTarget(point))
            {
                return;
            }

            phase = OrderPhase.Delivering;
            remainingTime = activeTimeLimit;

            // Star-rating snapshot: everything measured over the delivery leg.
            conditionAtPickup = cachedVehicleCondition != null ? cachedVehicleCondition.CurrentCondition : -1f;
            collisionsAtPickup = cachedVehicleCondition != null ? cachedVehicleCondition.CollisionCount : 0;
            deliveryStartTime = Time.time;
            speedingSeconds = 0f;
            peakSpeedKph = 0f;

            point.SetMarkerActive(false);
            if (InteractionPoint.TryGetPoint(activeOffer.DeliveryPointId, out InteractionPoint delivery))
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

            // payFactor stays purely time-based (money balance is a separate open
            // TODO); the star rating now carries the quality signal.
            float payFactor;
            float lateT;
            if (onTime)
            {
                payFactor = 1f;
                lateT = 0f;
            }
            else
            {
                float lateWindow = Mathf.Max(0.01f, activeTimeLimit * lateGraceFactor);
                lateT = Mathf.Clamp01(-remainingTime / lateWindow);
                payFactor = Mathf.Lerp(1f, latePayFraction, lateT);
            }

            int collisions = cachedVehicleCondition != null
                ? Mathf.Max(0, cachedVehicleCondition.CollisionCount - collisionsAtPickup)
                : 0;
            float conditionLost = (conditionAtPickup >= 0f && cachedVehicleCondition != null)
                ? Mathf.Max(0f, conditionAtPickup - cachedVehicleCondition.CurrentCondition)
                : 0f;
            float jobDistance = activeOffer != null
                ? activeOffer.DistanceMeters
                : Mathf.Max(0f, GetOrderDistance(activeOrder));

            float stars = ScoreDelivery(lateT, onTime, collisions, conditionLost);
            float distanceFactor = Mathf.Clamp(jobDistance / 250f, 0.5f, 2f);

            // Anti-farm: repeating the exact same route damps its RP (spec E2).
            string routeKey = activeOffer.PickupPointId + ">" + activeOffer.DeliveryPointId;
            int routeRepeats = 0;
            for (int i = 0; i < recentRoutes.Count; i++)
            {
                if (recentRoutes[i] == routeKey)
                {
                    routeRepeats++;
                }
            }

            float routeRepeatFactor = 1f / (1f + Mathf.Max(0f, routeRepeatPenalty) * routeRepeats);

            recentRoutes.Add(routeKey);
            while (recentRoutes.Count > Mathf.Max(1, routeRepeatWindow))
            {
                recentRoutes.RemoveAt(0);
            }

            float reputationMultiplier = ReputationManager.Instance != null
                ? ReputationManager.Instance.CurrentPaymentMultiplier
                : 1f;

            float basePayment = activeOffer != null ? activeOffer.Payment : GetOrderPayment(activeOrder);
            float payout = basePayment * payFactor * reputationMultiplier;

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddMoney(payout);
            }

            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.RegisterDelivery(stars, distanceFactor, routeRepeatFactor);
            }

            var result = new DeliveryResult
            {
                Order = activeOrder,
                Stars = stars,
                RawStars = stars,
                Payout = payout,
                OnTime = onTime,
                Failed = false,
                Collisions = collisions,
                ConditionLost = conditionLost,
                PeakSpeedKph = peakSpeedKph,
                JobDistanceMeters = jobDistance
            };

            NotificationService.Raise(onTime
                ? $"Teslimat tamamlandı! +{payout:F0} para, {stars:F1} yıldız."
                : $"Geç teslimat. +{payout:F0} para, {stars:F1} yıldız.");

            ClearActiveOrder();
            OnOrderCompleted?.Invoke(result);
            RefreshOffers();
        }

        /// <summary>
        /// Multi-factor delivery star score (docs/design/reputation-redesign.md A):
        /// start at max, subtract lateness + close-call + cargo-weighted
        /// crash/damage/speeding penalties, clamp, then a small picky-customer jitter
        /// so a clean run averages ~4.9 rather than a guaranteed 5.
        /// </summary>
        private float ScoreDelivery(float lateT, bool onTime, int collisions, float conditionLost)
        {
            float sens = CargoSensitivity(activeOrder != null ? activeOrder.CargoType : CargoType.Package);

            float latePenalty = lateT * 4f;

            float margin = activeTimeLimit > 0f ? remainingTime / activeTimeLimit : 1f;
            float closeCall = (onTime && margin < closeCallMarginFraction) ? closeCallPenalty : 0f;

            float crashDed = Mathf.Min(2f, collisions * crashStarPenalty);
            float damageDed = Mathf.Min(2f, conditionLost * damageStarPenaltyPerPoint);

            float deliveryDuration = Mathf.Max(0.01f, Time.time - deliveryStartTime);
            float spdFrac = Mathf.Clamp01(speedingSeconds / deliveryDuration);
            float speedDed = Mathf.Min(speedStarPenaltyMax, spdFrac * 1.2f);

            float stars = maxStars - latePenalty - closeCall - sens * (crashDed + damageDed + speedDed);
            stars = Mathf.Clamp(stars, minStars, maxStars);
            stars += UnityEngine.Random.Range(-0.15f, 0.10f);
            return Mathf.Clamp(stars, minStars, maxStars);
        }

        private float CargoSensitivity(CargoType type)
        {
            int i = (int)type;
            if (cargoSensitivity != null && i >= 0 && i < cargoSensitivity.Length)
            {
                return cargoSensitivity[i];
            }

            return 1f;
        }

        // ---------- Failure / cleanup ----------

        private void FailActiveOrder(string reason, bool registerReputationHit = true)
        {
            OrderData failed = activeOrder;

            if (registerReputationHit && ReputationManager.Instance != null)
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
            if (activeOffer != null)
            {
                if (InteractionPoint.TryGetPoint(activeOffer.PickupPointId, out InteractionPoint pickup))
                {
                    pickup.SetMarkerActive(false);
                }

                if (InteractionPoint.TryGetPoint(activeOffer.DeliveryPointId, out InteractionPoint delivery))
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
            activeOffer = null;
            phase = OrderPhase.None;
            remainingTime = 0f;
            activeTimeLimit = 0f;
            remainingPickupTime = 0f;
            activePickupTimeLimit = 0f;
        }

        private InteractionPoint GetCurrentTargetPoint()
        {
            if (activeOffer == null)
            {
                return null;
            }

            string targetId = phase == OrderPhase.AwaitingPickup
                ? activeOffer.PickupPointId
                : activeOffer.DeliveryPointId;

            InteractionPoint point;
            return InteractionPoint.TryGetPoint(targetId, out point) ? point : null;
        }
    }
}
