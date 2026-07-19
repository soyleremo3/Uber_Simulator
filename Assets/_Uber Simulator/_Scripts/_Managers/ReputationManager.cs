using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Tracks the rolling average of the last N delivery scores and maps it to a
    /// reputation tier (Bronze/Silver/Gold/Diamond). Higher tiers unlock better
    /// paying orders (OrderData.MinReputationTier) and add a payment multiplier.
    /// </summary>
    public class ReputationManager : MonoBehaviour
    {
        public static ReputationManager Instance { get; private set; }

        [Header("Averaging")]
        [Tooltip("How many recent deliveries count toward the average.")]
        [SerializeField] private int recentDeliveryWindow = 10;
        [Tooltip("Average assumed before any delivery is registered.")]
        [SerializeField] private float startingAverage = 3f;

        [Header("Tier thresholds (average stars required)")]
        [SerializeField] private float silverThreshold = 3.0f;
        [SerializeField] private float goldThreshold = 4.0f;
        [SerializeField] private float diamondThreshold = 4.6f;

        [Header("Payment multipliers per tier (Bronze, Silver, Gold, Diamond)")]
        [SerializeField] private float[] paymentMultipliers = { 1.0f, 1.05f, 1.12f, 1.2f };

        private readonly Queue<float> recentScores = new Queue<float>();

        public float AverageScore { get; private set; }
        public ReputationTier CurrentTier { get; private set; } = ReputationTier.Bronze;

        /// <summary>Fires on every registered delivery: (average, tier).</summary>
        public event Action<float, ReputationTier> OnReputationChanged;
        /// <summary>Fires only when the tier actually changes.</summary>
        public event Action<ReputationTier> OnReputationLevelChanged;

        public float CurrentPaymentMultiplier
        {
            get
            {
                int index = Mathf.Clamp((int)CurrentTier, 0, paymentMultipliers.Length - 1);
                return paymentMultipliers.Length > 0 ? paymentMultipliers[index] : 1f;
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

            AverageScore = startingAverage;
            CurrentTier = TierForAverage(AverageScore);
        }

        public void RegisterDelivery(float stars)
        {
            recentScores.Enqueue(Mathf.Clamp(stars, 0f, 5f));
            while (recentScores.Count > Mathf.Max(1, recentDeliveryWindow))
            {
                recentScores.Dequeue();
            }

            RecalculateAverage();
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

            RecalculateAverage();
        }

        // ---------- Internals ----------

        private void RecalculateAverage()
        {
            if (recentScores.Count == 0)
            {
                AverageScore = startingAverage;
            }
            else
            {
                float sum = 0f;
                foreach (float s in recentScores)
                {
                    sum += s;
                }

                AverageScore = sum / recentScores.Count;
            }

            ReputationTier newTier = TierForAverage(AverageScore);
            bool tierChanged = newTier != CurrentTier;
            CurrentTier = newTier;

            OnReputationChanged?.Invoke(AverageScore, CurrentTier);
            if (tierChanged)
            {
                OnReputationLevelChanged?.Invoke(CurrentTier);
                NotificationService.Raise($"İtibar kademesi değişti: {TierDisplayName(CurrentTier)}!");
            }
        }

        private ReputationTier TierForAverage(float average)
        {
            if (average >= diamondThreshold) return ReputationTier.Diamond;
            if (average >= goldThreshold) return ReputationTier.Gold;
            if (average >= silverThreshold) return ReputationTier.Silver;
            return ReputationTier.Bronze;
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
