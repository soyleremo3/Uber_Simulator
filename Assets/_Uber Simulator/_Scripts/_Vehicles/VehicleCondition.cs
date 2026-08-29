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
        [Tooltip("Bu hızın (m/s) altındaki temaslar hasar vermez — hafif sürtme/park teması. 3 m/s ≈ 11 km/s.")]
        [SerializeField] private float minImpactSpeed = 3f;
        [Tooltip("Hasar eğrisinin dikliği. 1 = doğrusal; >1 => sert çarpışmalar orantısız fazla hasar (çarpma sertliğine göre).")]
        [SerializeField] private float impactSpeedExponent = 1.6f;
        [Tooltip("Eşiği aşan her 1 m/s için taban hasar çarpanı.")]
        [SerializeField] private float damagePerUnitImpact = 1.1f;
        [Tooltip("Tek çarpışmada verilebilecek en fazla condition hasarı.")]
        [SerializeField] private float maxDamagePerHit = 60f;
        [Tooltip("Aynı temastan art arda hasar almayı engelleyen çarpışmalar arası en kısa süre (sn).")]
        [SerializeField] private float hitCooldown = 0.2f;

        private float currentCondition;
        private float durabilityMultiplier = 1f;
        private float lastHitTime = -999f;

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
            if (Time.time - lastHitTime < hitCooldown || collision.contactCount == 0)
            {
                return;
            }

            // Impact severity = closing speed ALONG the contact normal, NOT the full
            // relativeVelocity (which would punish merely sliding along a wall).
            // This is mass-independent on purpose: the old Collision.impulse path
            // never cleared its threshold on this ~1 kg Rigidbody, so no crash ever
            // registered any damage.
            Vector3 normal = collision.GetContact(0).normal;
            float impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
            if (impactSpeed < minImpactSpeed)
            {
                return;
            }

            lastHitTime = Time.time;

            float over = impactSpeed - minImpactSpeed;
            float damage = Mathf.Pow(over, impactSpeedExponent) * damagePerUnitImpact;
            // Durability upgrade divides incoming damage (multiplier >= 1).
            damage = Mathf.Min(maxDamagePerHit, damage) / Mathf.Max(1f, durabilityMultiplier);
            if (damage <= 0f)
            {
                return;
            }

            ApplyDamage(damage);
            NotificationService.Raise($"Çarpışma! Araç hasarı -{damage:F0} (kalan %{currentCondition:F0})");
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
