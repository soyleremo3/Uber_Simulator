using System;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Vehicle wear/damage: collisions reduce condition (0-100). Repairs happen at
    /// RepairStation for money. Durability upgrades divide incoming damage.
    /// MVP note: low condition has no physical handling penalty yet — it is purely
    /// an economy sink (GDD 3.4: every delivery must carry a small cost).
    /// </summary>
    public class VehicleCondition : MonoBehaviour
    {
        [Header("Condition")]
        [SerializeField] private float maxCondition = 100f;

        [Header("Collision Damage")]
        [Tooltip("Collision impulse below this is ignored (small scrapes).")]
        [SerializeField] private float minImpulseThreshold = 300f;
        [Tooltip("Condition points lost per 1000 units of collision impulse.")]
        [SerializeField] private float damagePerThousandImpulse = 2.5f;

        private float currentCondition;
        private float durabilityMultiplier = 1f;

        public float MaxCondition => maxCondition;
        public float CurrentCondition => currentCondition;

        /// <summary>(current, max) — UI listens to this.</summary>
        public event Action<float, float> OnConditionChanged;

        private void Awake()
        {
            currentCondition = maxCondition;
        }

        private void Start()
        {
            OnConditionChanged?.Invoke(currentCondition, maxCondition);
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impulse = collision.impulse.magnitude;
            if (impulse < minImpulseThreshold)
            {
                return;
            }

            // Durability upgrade divides incoming damage (multiplier >= 1).
            float damage = (impulse / 1000f) * damagePerThousandImpulse / Mathf.Max(1f, durabilityMultiplier);
            ApplyDamage(damage);
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentCondition = Mathf.Max(0f, currentCondition - amount);
            OnConditionChanged?.Invoke(currentCondition, maxCondition);
        }

        /// <summary>Full repair; cost logic lives in RepairStation.</summary>
        public void RepairFully()
        {
            currentCondition = maxCondition;
            OnConditionChanged?.Invoke(currentCondition, maxCondition);
        }

        /// <summary>Called by VehicleUpgradeApplier when the Durability upgrade changes.</summary>
        public void SetDurabilityMultiplier(float multiplier)
        {
            durabilityMultiplier = Mathf.Max(1f, multiplier);
        }
    }
}
