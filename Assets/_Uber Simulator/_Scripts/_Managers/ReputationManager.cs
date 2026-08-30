using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Reputation progression, redesigned as an XP-style model (see
    /// docs/design/reputation-redesign.md):
    ///
    ///  - Every delivery earns cumulative "Reputation Points" (RP) that never decay.
    ///    RP -> a rising level curve. Bronze/Silver/Gold/Diamond are bands over the
    ///    level, so a single delivery can no longer skip a tier and you start at
    ///    Bronze / Level 1.
    ///  - "Recent Form" = the rolling mean of the last N delivery star scores. It is
    ///    display-only + gates the Diamond payout multiplier; it does NOT drive the
    ///    tier any more.
    /// </summary>
    public class ReputationManager : MonoBehaviour
    {
        public static ReputationManager Instance { get; private set; }

        [Header("Recent Form (rolling star average — display + Diamond gate only)")]
        [Tooltip("How many recent deliveries count toward Recent Form.")]
        [SerializeField] private int recentDeliveryWindow = 10;
        [Tooltip("Recent Form value before any delivery is registered.")]
        [SerializeField] private float startingRecentForm = 5f;
        [Tooltip("Diamond kademesindeyken Son Form bunun altına düşerse ödeme çarpanı geçici olarak Altın seviyesine iner.")]
        [SerializeField] private float diamondFormGate = 4.2f;

        [Header("Reputation Points (XP) & levels")]
        [Tooltip("Bir sonraki seviyeye ulaşmak için gereken toplam RP = rpCurveBase * (level-1)^rpCurveExp. ~90 RP/teslimat varsayımıyla ayarlandı.")]
        [SerializeField] private float rpCurveBase = 500f;
        [SerializeField] private float rpCurveExp = 1.45f;
        [Tooltip("Yıldıza göre taban RP (indeks 0=1★ .. 4=5★); aradaki yıldızlar interpolasyonla.")]
        [SerializeField] private float[] rpByStar = { 2f, 10f, 30f, 60f, 100f };
        [Tooltip("Bu seviyede (dahil) yeni tier başlar: {Bronze, Silver, Gold, Diamond} için minimum seviye.")]
        [SerializeField] private int[] tierMinLevel = { 1, 4, 9, 16 };

        [Header("Payment multipliers per tier (Bronze, Silver, Gold, Diamond)")]
        [SerializeField] private float[] paymentMultipliers = { 1.0f, 1.05f, 1.12f, 1.2f };

        private readonly Queue<float> recentScores = new Queue<float>();

        /// <summary>Rolling mean of the last N delivery star scores. Kept named AverageScore for API compatibility; also exposed as RecentForm.</summary>
        public float AverageScore { get; private set; }
        public float RecentForm => AverageScore;
        public ReputationTier CurrentTier { get; private set; } = ReputationTier.Bronze;

        public int TotalReputationPoints { get; private set; }
        public int CurrentLevel { get; private set; } = 1;
        public int RPIntoCurrentLevel { get; private set; }
        public int RPForNextLevel { get; private set; } = 1;

        /// <summary>Fires on every registered delivery: (recentForm, tier). Kept for existing HUD wiring.</summary>
        public event Action<float, ReputationTier> OnReputationChanged;
        /// <summary>Fires only when the tier actually changes.</summary>
        public event Action<ReputationTier> OnReputationLevelChanged;
        /// <summary>Fires when the level increases; arg = the new level.</summary>
        public event Action<int> OnLevelUp;
        /// <summary>Fires on every registered delivery: (level, rpIntoLevel, rpForNextLevel) — drives the HUD progress bar.</summary>
        public event Action<int, int, int> OnReputationProgress;

        public float CurrentPaymentMultiplier
        {
            get
            {
                if (paymentMultipliers.Length == 0)
                {
                    return 1f;
                }

                int index = Mathf.Clamp((int)CurrentTier, 0, paymentMultipliers.Length - 1);

                // Diamond gate: sloppy Recent Form temporarily suspends the top bonus
                // (keeps the tier + order access, drops the multiplier to Gold's).
                if (CurrentTier == ReputationTier.Diamond && RecentForm < diamondFormGate)
                {
                    index = Mathf.Clamp((int)ReputationTier.Gold, 0, paymentMultipliers.Length - 1);
                }

                return paymentMultipliers[index];
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
            DontDestroyOnLoad(gameObject);

            AverageScore = startingRecentForm;
            TotalReputationPoints = 0;
            RecalculateLevel();
            CurrentTier = TierForLevel(CurrentLevel);
        }

        // ---------- Delivery entry points ----------

        public void RegisterDelivery(float stars)
        {
            RegisterDelivery(stars, 1f, 1f);
        }

        /// <summary>
        /// Full entry point. <paramref name="distanceFactor"/> scales RP by job
        /// length (spec E1), <paramref name="routeRepeatFactor"/> damps farming the
        /// same route (spec E2). Both default to 1 via the simple overload.
        /// </summary>
        public void RegisterDelivery(float stars, float distanceFactor, float routeRepeatFactor)
        {
            float clamped = Mathf.Clamp(stars, 0f, 5f);

            // --- Recent Form (rolling star average) ---
            recentScores.Enqueue(clamped);
            while (recentScores.Count > Mathf.Max(1, recentDeliveryWindow))
            {
                recentScores.Dequeue();
            }

            RecalculateRecentForm();

            // --- Reputation Points (cumulative XP, never decays) ---
            int rp = Mathf.RoundToInt(BaseRPForStars(clamped)
                * Mathf.Clamp(distanceFactor, 0.1f, 4f)
                * Mathf.Clamp(routeRepeatFactor, 0.1f, 1f));
            rp = Mathf.Max(1, rp); // a finished job always earns something

            int previousLevel = CurrentLevel;
            ReputationTier previousTier = CurrentTier;

            TotalReputationPoints += rp;
            RecalculateLevel();
            CurrentTier = TierForLevel(CurrentLevel);

            OnReputationChanged?.Invoke(AverageScore, CurrentTier);
            OnReputationProgress?.Invoke(CurrentLevel, RPIntoCurrentLevel, RPForNextLevel);

            if (CurrentLevel > previousLevel)
            {
                OnLevelUp?.Invoke(CurrentLevel);
                NotificationService.Raise($"Seviye {CurrentLevel}! (+{rp} itibar puanı)");
            }

            if (CurrentTier != previousTier)
            {
                OnReputationLevelChanged?.Invoke(CurrentTier);
                NotificationService.Raise($"İtibar kademesi: {TierDisplayName(CurrentTier)}!");
            }
        }

        public bool IsOrderUnlocked(OrderData order)
        {
            return order != null && CurrentTier >= order.MinReputationTier;
        }

        // ---------- Save/Load API (used by SaveSystem) ----------

        public List<float> GetScoresSnapshot()
        {
            return new List<float>(recentScores);
        }

        public void RestoreScores(List<float> scores)
        {
            recentScores.Clear();

            if (scores != null)
            {
                foreach (float s in scores)
                {
                    recentScores.Enqueue(Mathf.Clamp(s, 0f, 5f));
                }
            }

            RecalculateRecentForm();
        }

        public int GetReputationPoints()
        {
            return TotalReputationPoints;
        }

        /// <summary>Restores RP + Recent Form from a save. Recomputes level and tier and fires the refresh events.</summary>
        public void RestoreReputation(int reputationPoints, List<float> scores)
        {
            TotalReputationPoints = Mathf.Max(0, reputationPoints);
            RecalculateLevel();
            RestoreScores(scores);
            CurrentTier = TierForLevel(CurrentLevel);

            OnReputationChanged?.Invoke(AverageScore, CurrentTier);
            OnReputationProgress?.Invoke(CurrentLevel, RPIntoCurrentLevel, RPForNextLevel);
            OnReputationLevelChanged?.Invoke(CurrentTier);
        }

        // ---------- Internals ----------

        private void RecalculateRecentForm()
        {
            if (recentScores.Count == 0)
            {
                AverageScore = startingRecentForm;
                return;
            }

            float sum = 0f;
            foreach (float s in recentScores)
            {
                sum += s;
            }

            AverageScore = sum / recentScores.Count;
        }

        /// <summary>Cumulative RP needed to REACH a level (level 1 = 0).</summary>
        private int RPToReachLevel(int level)
        {
            if (level <= 1)
            {
                return 0;
            }

            return Mathf.RoundToInt(rpCurveBase * Mathf.Pow(level - 1, rpCurveExp));
        }

        private void RecalculateLevel()
        {
            int level = 1;
            while (level < 999 && TotalReputationPoints >= RPToReachLevel(level + 1))
            {
                level++;
            }

            CurrentLevel = level;
            int floor = RPToReachLevel(level);
            int ceiling = RPToReachLevel(level + 1);
            RPIntoCurrentLevel = TotalReputationPoints - floor;
            RPForNextLevel = Mathf.Max(1, ceiling - floor);
        }

        private float BaseRPForStars(float stars)
        {
            if (rpByStar == null || rpByStar.Length == 0)
            {
                return Mathf.Clamp(stars, 1f, 5f) * 20f;
            }

            float t = Mathf.Clamp(stars, 1f, 5f) - 1f; // 0..4
            int lo = Mathf.Clamp(Mathf.FloorToInt(t), 0, rpByStar.Length - 1);
            int hi = Mathf.Clamp(lo + 1, 0, rpByStar.Length - 1);
            return Mathf.Lerp(rpByStar[lo], rpByStar[hi], t - lo);
        }

        private ReputationTier TierForLevel(int level)
        {
            ReputationTier tier = ReputationTier.Bronze;

            if (tierMinLevel != null)
            {
                for (int i = 0; i < tierMinLevel.Length && i <= (int)ReputationTier.Diamond; i++)
                {
                    if (level >= tierMinLevel[i])
                    {
                        tier = (ReputationTier)i;
                    }
                }
            }

            return tier;
        }

        public static string TierDisplayName(ReputationTier tier)
        {
            switch (tier)
            {
                case ReputationTier.Silver: return "Gümüş";
                case ReputationTier.Gold: return "Altın";
                case ReputationTier.Diamond: return "Elmas";
                default: return "Bronz";
            }
        }
    }
}
