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
        [Tooltip("All order definitions this scene can offer (used as archetype templates).")]
        [SerializeField] private List<OrderData> orderPool = new List<OrderData>();
        [Tooltip("Her siparişe rastgele bir müşteri (alıcı) adı vermek için havuz. Boşsa müşteri gösterilmez.")]
        [SerializeField] private CustomerPoolData customerPool;
        [Tooltip("Alıcının birey (ev teslimatı) olma olasılığı; kalanı işletme.")]
        [Range(0f, 1f)][SerializeField] private float individualRecipientChance = 0.72f;

        [Header("Offers")]
        [Tooltip("Panoda aynı anda görünebilecek en fazla teklif (her zaman dolu olmak zorunda değil).")]
        [SerializeField] private int maxOffers = 10;
        [Tooltip("Seconds between automatic offer refills — only used when Arrivals is OFF (legacy backfill).")]
        [SerializeField] private float offerRefreshInterval = 15f;
        [Tooltip("Her teklif kendi TTL'i (sn) kadar panoda kalır, sonra kendiliğinden düşer.")]
        [SerializeField] private float offerTtlMin = 75f;
        [SerializeField] private float offerTtlMax = 150f;

        [Header("Arrivals (time-varying Poisson — see order-board-redesign.md A)")]
        [Tooltip("Açıkken teklifler GameClock'a bağlı λ(t) hızıyla rastgele 'gelir' (yığın + durgunluk). Kapalıyken eski sabit backfill kullanılır.")]
        [SerializeField] private bool useArrivals = true;
        [Tooltip("Oyun-içi saate göre gelme hızı (teklif/gerçek dk), indeks 0..23. 24 giriş yoksa fallbackLambda kullanılır.")]
        [SerializeField] private float[] lambdaByHour =
        {
            0.15f, 0.15f, 0.15f, 0.15f, 0.15f, 0.15f, // 00-06 late night
            0.5f, 0.5f,                               // 06-08 early morning
            1.6f, 1.6f, 1.6f,                         // 08-11 morning rush
            0.4f,                                     // 11-12 midday lull
            2.2f, 2.2f,                               // 12-14 lunch rush
            0.9f, 0.9f, 0.9f,                         // 14-17 afternoon
            2.4f, 2.4f, 2.4f,                         // 17-20 evening rush
            0.7f, 0.7f, 0.7f, 0.7f                    // 20-24 night
        };
        [SerializeField] private float fallbackLambda = 1.2f;
        [Tooltip("Gelme hızı tier çarpanı: {Bronze, Silver, Gold, Diamond}.")]
        [SerializeField] private float[] tierArrivalMultiplier = { 0.8f, 1.0f, 1.25f, 1.5f };
        [Tooltip("Gündüz (06:00-22:00) pano bu kadar saniye boş kalırsa bir teklif zorla eklenir.")]
        [SerializeField] private float daytimeFloorEmptySeconds = 45f;
        [Tooltip("Gündüz taban teklif sayısı: {Bronze, Silver, Gold, Diamond}. Gece taban 0.")]
        [SerializeField] private int[] daytimeFloorByTier = { 1, 1, 2, 2 };
        [Tooltip("Oyun başında panoya doğrudan eklenecek teklif sayısı (ilk açılışta pano boş kalmasın).")]
        [SerializeField] private int seedOffers = 2;

        [Header("Cluster arrivals (batches)")]
        [Tooltip("Bir gelişin arkasından kısa süre içinde 1-2 ek gelişi tetikleme ihtimali (restoranın toplu sipariş vermesi hissi).")]
        [Range(0f, 1f)][SerializeField] private float clusterChance = 0.35f;
        [SerializeField] private int clusterMinExtra = 1;
        [SerializeField] private int clusterMaxExtra = 2;
        [SerializeField] private float clusterDelayMin = 3f;
        [SerializeField] private float clusterDelayMax = 8f;

        [Header("Priority / VIP offer")]
        [Tooltip("Nadir, yüksek ödemeli ama daha sıkı süreli + kısa TTL'li 'ÖNCELİKLİ' sipariş.")]
        [SerializeField] private bool usePriorityOffers = true;
        [Range(0f, 1f)][SerializeField] private float priorityChance = 0.04f;
        [SerializeField] private ReputationTier priorityMinTier = ReputationTier.Silver;
        [SerializeField] private float priorityPayMin = 2.2f;
        [SerializeField] private float priorityPayMax = 3.5f;
        [Tooltip("Öncelikli siparişin süresi normal sürenin bu katı (daha kısa = daha zor).")]
        [SerializeField] private float priorityTimeFactor = 0.8f;
        [Tooltip("Öncelikli siparişin panoda kalma süresi (kısa — anlık karar).")]
        [SerializeField] private float priorityTtl = 40f;
        [Tooltip("İki öncelikli sipariş arası en kısa süre.")]
        [SerializeField] private float priorityCooldown = 90f;

        [Header("Surge pricing")]
        [Tooltip("Talep patlamasında / kıtlıkta yeni tekliflerin ödemesini geçici olarak yükseltir (durgunlukta beklemeyi ödüllendirir).")]
        [SerializeField] private bool useSurge = true;
        [Tooltip("Son 120 sn'deki geliş / beklenen oranı bunu aşarsa surge devreye girer.")]
        [SerializeField] private float surgeDemandThreshold = 1.6f;
        [Tooltip("Pano bu kadar saniye ≤1 teklifte kalırsa (kıtlık) surge devreye girer.")]
        [SerializeField] private float scarcitySeconds = 40f;
        [SerializeField] private float surgeMin = 1.15f;
        [SerializeField] private float surgeMax = 2.0f;
        [SerializeField] private float surgeScarcityMultiplier = 1.35f;
        [SerializeField] private float surgeRecalcInterval = 5f;

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
        [Tooltip("Açıkken ödeme = (taban ücret + km başına ücret × iş mesafesi) × kargo çarpanı + Fragile handling. Kapalıyken OrderData.PaymentAmount.")]
        [SerializeField] private bool useDistanceBasedPayment = true;
        [Tooltip("Her işin sabit taban (kapıya gelme) ücreti.")]
        [SerializeField] private float baseFare = 20f;
        [Tooltip("İş mesafesinin her kilometresi için eklenen ücret.")]
        [SerializeField] private float paymentPerKm = 14f;
        [Tooltip("Kargo tipi ödeme çarpanı — indeks: Food, Package, Fragile.")]
        [SerializeField] private float[] cargoPayMultiplier = { 1.0f, 1.05f, 1.25f };
        [Tooltip("Fragile kargoya eklenen sabit elleçleme ücreti.")]
        [SerializeField] private float fragileHandlingFee = 15f;
        [Tooltip("Rush (ACİL) bayraklı siparişin ödeme çarpanı.")]
        [SerializeField] private float rushPayMultiplier = 1.3f;

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

        [Header("Delivery Streak (session-scoped, not saved)")]
        [Tooltip("Üst üste zamanında ve ≥ bu yıldız olan teslimatlar seriyi büyütür; geç/başarısız teslimat sıfırlar.")]
        [SerializeField] private float streakOnTimeStarThreshold = 4f;
        [Tooltip("Seri seviyesi başına ödeme bonusu (0.04 = her seri +%4).")]
        [SerializeField] private float streakBonusPerLevel = 0.04f;
        [Tooltip("Seri bonusunun üst sınırı (0.40 = maks +%40).")]
        [SerializeField] private float streakBonusMax = 0.40f;

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

        private float emptyBoardSeconds;

        // Cluster arrivals: absolute Time.time stamps at which to spawn a batched extra.
        private readonly List<float> pendingClusterArrivals = new List<float>();

        // Surge: rolling window of arrival timestamps + the current multiplier.
        private readonly Queue<float> arrivalTimestamps = new Queue<float>();
        private float surgeMultiplier = 1f;
        private float surgeTimer;
        private float lowBoardSeconds;

        public float SurgeMultiplier => surgeMultiplier;
        public event Action<float> OnSurgeChanged;

        private int streak;
        public int Streak => streak;
        public event Action<int> OnStreakChanged;

        private float lastPriorityTime = -999f;

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

            if (useArrivals)
            {
                // Don't start on a dead board — drop a few offers in immediately (no cluster on seed).
                for (int i = 0; i < seedOffers; i++)
                {
                    SpawnArrival(allowCluster: false);
                }
            }
            else
            {
                RefreshOffers();
            }
        }

        private void Update()
        {
            // Each offer leaves the board on its own TTL — the board "churns".
            TickOfferTtl();

            if (useArrivals)
            {
                if (useSurge)
                {
                    TickSurge();
                }

                TickArrivals();
            }
            else
            {
                offerTimer -= Time.deltaTime;
                if (offerTimer <= 0f)
                {
                    offerTimer = offerRefreshInterval;
                    RefreshOffers();
                }
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

                // With arrivals ON the freed slot is NOT instantly refilled — an
                // arrival (or the daytime floor) does that. Only the legacy path backfills.
                if (!useArrivals)
                {
                    RefreshOffers();
                }
            }
        }

        // ---------- Arrivals (time-varying Poisson) ----------

        /// <summary>
        /// Stochastic offer arrivals whose rate λ(t) varies over the in-game day
        /// (rush hours vs lulls) — randomness alone produces the burst/quiet feel.
        /// Plus a daytime "soft floor" so the board is never dead for long in daylight.
        /// </summary>
        private void TickArrivals()
        {
            // Fire any due cluster ("batch") arrivals first.
            for (int i = pendingClusterArrivals.Count - 1; i >= 0; i--)
            {
                if (Time.time >= pendingClusterArrivals[i])
                {
                    pendingClusterArrivals.RemoveAt(i);
                    SpawnArrival(allowCluster: false);
                }
            }

            float lambda = CurrentLambda();                 // offers per real minute
            float p = lambda * Time.deltaTime / 60f;        // arrival prob this frame
            if (currentOffers.Count < maxOffers && UnityEngine.Random.value < p)
            {
                SpawnArrival(allowCluster: true);
            }

            bool night = GameClock.Instance != null && GameClock.Instance.IsNight;

            if (currentOffers.Count == 0)
            {
                emptyBoardSeconds += Time.deltaTime;
            }
            else
            {
                emptyBoardSeconds = 0f;
            }

            if (!night &&
                currentOffers.Count < DaytimeFloor() &&
                emptyBoardSeconds > daytimeFloorEmptySeconds)
            {
                SpawnArrival(allowCluster: true);
                emptyBoardSeconds = 0f;
            }
        }

        /// <summary>
        /// Recomputes the surge multiplier every surgeRecalcInterval seconds from the
        /// last-120s arrival count vs. expected, plus a "board starved" scarcity
        /// trigger. New offers spawned while surge is up get their payment multiplied.
        /// </summary>
        private void TickSurge()
        {
            while (arrivalTimestamps.Count > 0 && Time.time - arrivalTimestamps.Peek() > 120f)
            {
                arrivalTimestamps.Dequeue();
            }

            if (currentOffers.Count <= 1)
            {
                lowBoardSeconds += Time.deltaTime;
            }
            else
            {
                lowBoardSeconds = 0f;
            }

            surgeTimer -= Time.deltaTime;
            if (surgeTimer > 0f)
            {
                return;
            }

            surgeTimer = surgeRecalcInterval;

            float expected = Mathf.Max(0.5f, CurrentLambda() * 2f); // arrivals expected per 120 s
            float demandRatio = arrivalTimestamps.Count / expected;

            float previous = surgeMultiplier;
            if (demandRatio > surgeDemandThreshold)
            {
                surgeMultiplier = Mathf.Clamp(1f + 0.5f * (demandRatio - 1f), surgeMin, surgeMax);
            }
            else if (lowBoardSeconds > scarcitySeconds)
            {
                surgeMultiplier = surgeScarcityMultiplier;
            }
            else if (surgeMultiplier > 1f && demandRatio < 1.2f && currentOffers.Count > 2)
            {
                surgeMultiplier = 1f;
            }

            if (!Mathf.Approximately(previous, surgeMultiplier))
            {
                OnSurgeChanged?.Invoke(surgeMultiplier);
            }
        }

        private float CurrentLambda()
        {
            float baseLambda = fallbackLambda;
            if (lambdaByHour != null && lambdaByHour.Length == 24 && GameClock.Instance != null)
            {
                int h = Mathf.Clamp(Mathf.FloorToInt(GameClock.Instance.Hour), 0, 23);
                baseLambda = lambdaByHour[h];
            }

            float tierMultiplier = 1f;
            if (ReputationManager.Instance != null && tierArrivalMultiplier != null && tierArrivalMultiplier.Length > 0)
            {
                int t = Mathf.Clamp((int)ReputationManager.Instance.CurrentTier, 0, tierArrivalMultiplier.Length - 1);
                tierMultiplier = tierArrivalMultiplier[t];
            }

            return Mathf.Max(0f, baseLambda) * tierMultiplier;
        }

        private int DaytimeFloor()
        {
            if (daytimeFloorByTier == null || daytimeFloorByTier.Length == 0)
            {
                return 1;
            }

            int t = ReputationManager.Instance != null
                ? Mathf.Clamp((int)ReputationManager.Instance.CurrentTier, 0, daytimeFloorByTier.Length - 1)
                : 0;
            return Mathf.Max(0, daytimeFloorByTier[t]);
        }

        /// <summary>
        /// Adds ONE fresh offer to the board (if a template is available and there's
        /// room). When <paramref name="allowCluster"/> is set, may schedule 1-2
        /// batched extra arrivals a few seconds later (the "restaurant batch" feel).
        /// </summary>
        private void SpawnArrival(bool allowCluster)
        {
            if (currentOffers.Count >= maxOffers)
            {
                return;
            }

            OrderData template = PickTemplate();
            if (template == null)
            {
                return;
            }

            OrderOffer offer = BuildOffer(template);
            MaybeMakePriority(offer);
            currentOffers.Add(offer);
            arrivalTimestamps.Enqueue(Time.time);
            OnOffersChanged?.Invoke(currentOffers);

            if (allowCluster && UnityEngine.Random.value < clusterChance)
            {
                int extra = UnityEngine.Random.Range(clusterMinExtra, clusterMaxExtra + 1);
                for (int i = 0; i < extra; i++)
                {
                    pendingClusterArrivals.Add(
                        Time.time + UnityEngine.Random.Range(clusterDelayMin, clusterDelayMax));
                }
            }
        }

        /// <summary>
        /// Picks an unlocked template not already on the board, preferring ones
        /// outside recent history. Returns null when nothing qualifies.
        /// </summary>
        private OrderData PickTemplate()
        {
            var candidates = new List<OrderData>();
            var preferred = new List<OrderData>();

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
                if (!recentHistory.Contains(order))
                {
                    preferred.Add(order);
                }
            }

            List<OrderData> pool = preferred.Count > 0 ? preferred : candidates;
            return pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : null;
        }

        // ---------- Legacy backfill (Arrivals OFF) ----------

        /// <summary>Fills the board toward maxOffers from the template pool. Only used when Arrivals is off.</summary>
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

            while (currentOffers.Count < maxOffers)
            {
                OrderData picked = PickTemplate();
                if (picked == null)
                {
                    break;
                }

                currentOffers.Add(BuildOffer(picked));
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
            offer.TimeLimit = ResolveOfferTimeLimit(offer);
            offer.Payment = ResolveOfferPayment(offer);

            if (customerPool != null && customerPool.HasContent)
            {
                offer.Customer = UnityEngine.Random.value < individualRecipientChance
                    ? customerPool.RollIndividual()
                    : customerPool.RollBusiness();
            }

            // Surge: an offer that arrives while prices are surging pays more.
            offer.SurgeMultiplier = surgeMultiplier;
            if (surgeMultiplier > 1f)
            {
                offer.Payment *= surgeMultiplier;
            }

            return offer;
        }

        /// <summary>
        /// Distance-derived delivery time for an offer (order-board-redesign.md C):
        /// travel time (distance already includes routeFactor) + buffer, scaled by
        /// the difficulty time factor, floored at minTimeLimitSeconds. Falls back to
        /// the template's fixed value when distance can't be resolved.
        /// </summary>
        private float ResolveOfferTimeLimit(OrderOffer offer)
        {
            if (!useDistanceBasedTimeLimit || offer.DistanceMeters <= 0f)
            {
                return offer.Template != null ? offer.Template.TimeLimitSeconds : minTimeLimitSeconds;
            }

            float speedMs = Mathf.Max(1f, averageSpeedKmh / 3.6f);
            float travelTime = offer.DistanceMeters / speedMs;
            float difficultyTimeFactor = 1f; // difficulty flags feed this in a later step
            return Mathf.Max(minTimeLimitSeconds, (travelTime + timeBufferSeconds) * difficultyTimeFactor);
        }

        /// <summary>
        /// Distance-derived payout for an offer BEFORE lateness / reputation / surge
        /// multipliers (order-board-redesign.md C): (base fare + per-km × effKm) ×
        /// cargo multiplier × rush multiplier + Fragile handling fee.
        /// </summary>
        private float ResolveOfferPayment(OrderOffer offer)
        {
            if (!useDistanceBasedPayment)
            {
                return offer.Template != null ? offer.Template.PaymentAmount : 0f;
            }

            float effKm = Mathf.Max(0.3f, offer.DistanceMeters / 1000f);
            float amount = baseFare + paymentPerKm * effKm;
            amount *= CargoPayMultiplier(offer.CargoType);
            if (offer.HasFlag(OfferFlags.Rush))
            {
                amount *= rushPayMultiplier;
            }

            if (offer.CargoType == CargoType.Fragile)
            {
                amount += fragileHandlingFee;
            }

            return amount;
        }

        private float CargoPayMultiplier(CargoType type)
        {
            int i = (int)type;
            if (cargoPayMultiplier != null && i >= 0 && i < cargoPayMultiplier.Length)
            {
                return cargoPayMultiplier[i];
            }

            return 1f;
        }

        /// <summary>
        /// Rolls the rare "priority / VIP" upgrade on a just-built offer: much higher
        /// pay, tighter time, very short TTL. Silver+ only, at most one on the board,
        /// with a cooldown between spawns.
        /// </summary>
        private void MaybeMakePriority(OrderOffer offer)
        {
            if (!usePriorityOffers || offer == null)
            {
                return;
            }

            if (ReputationManager.Instance != null && ReputationManager.Instance.CurrentTier < priorityMinTier)
            {
                return;
            }

            if (Time.time - lastPriorityTime < priorityCooldown || BoardHasPriority())
            {
                return;
            }

            if (UnityEngine.Random.value >= priorityChance)
            {
                return;
            }

            offer.Flags |= OfferFlags.Priority;
            offer.Payment *= UnityEngine.Random.Range(priorityPayMin, priorityPayMax);
            offer.TimeLimit = Mathf.Max(minTimeLimitSeconds, offer.TimeLimit * priorityTimeFactor);
            offer.Ttl = priorityTtl;
            lastPriorityTime = Time.time;

            NotificationService.Raise($"⭐ ÖNCELİKLİ sipariş! {offer.DisplayName} — ₺{offer.Payment:F0}, sadece {priorityTtl:F0} sn panoda.");
        }

        private bool BoardHasPriority()
        {
            for (int i = 0; i < currentOffers.Count; i++)
            {
                if (currentOffers[i] != null && currentOffers[i].HasFlag(OfferFlags.Priority))
                {
                    return true;
                }
            }

            return false;
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

            // Delivery streak: consecutive good on-time deliveries pay a growing bonus.
            UpdateStreak(onTime && stars >= streakOnTimeStarThreshold);
            float streakBonus = Mathf.Min(streak * streakBonusPerLevel, streakBonusMax);

            float basePayment = activeOffer != null ? activeOffer.Payment : GetOrderPayment(activeOrder);
            float payout = basePayment * payFactor * reputationMultiplier * (1f + streakBonus);

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
            if (!useArrivals)
            {
                RefreshOffers();
            }
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

        /// <summary>Advances or resets the delivery streak and fires the change event. Session-scoped — never saved.</summary>
        private void UpdateStreak(bool qualifies)
        {
            if (qualifies)
            {
                streak++;
                if (streak == 3 || streak == 5 || streak == 10)
                {
                    NotificationService.Raise($"🔥 {streak}'li teslimat serisi! Bonus büyüyor.");
                }
            }
            else if (streak > 0)
            {
                streak = 0;
                NotificationService.Raise("Teslimat serisi bozuldu.");
            }

            OnStreakChanged?.Invoke(streak);
        }

        // ---------- Failure / cleanup ----------

        private void FailActiveOrder(string reason, bool registerReputationHit = true)
        {
            OrderData failed = activeOrder;

            UpdateStreak(false);

            if (registerReputationHit && ReputationManager.Instance != null)
            {
                ReputationManager.Instance.RegisterDelivery(minStars);
            }

            NotificationService.Raise(reason);
            ClearActiveOrder();
            OnOrderFailed?.Invoke(failed);
            if (!useArrivals)
            {
                RefreshOffers();
            }
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
