using UnityEngine;

namespace DeliverySim
{
    public enum UpgradeCategory
    {
        Engine,
        FuelTank,
        Durability
    }

    /// <summary>
    /// One purchasable upgrade step. The shop catalog holds one asset per
    /// (category, level) pair; levels must start at 1 and increase by 1.
    /// effectMultiplier is ABSOLUTE for that level (not cumulative):
    ///   Engine     -> engine power multiplier (1.15 = +15% torque)
    ///   FuelTank   -> fuel capacity multiplier
    ///   Durability -> incoming damage is divided by this multiplier
    /// </summary>
    [CreateAssetMenu(fileName = "NewUpgrade", menuName = "DeliverySim/Vehicle Upgrade Data")]
    public class VehicleUpgradeData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private UpgradeCategory category = UpgradeCategory.Engine;
        [SerializeField] private string displayName = "Motor Yükseltmesi";
        [Tooltip("1-based level within its category.")]
        [SerializeField] private int level = 1;

        [Header("Economy")]
        [SerializeField] private float cost = 500f;

        [Header("Effect")]
        [Tooltip("Absolute multiplier applied while this level is owned (see class summary).")]
        [SerializeField] private float effectMultiplier = 1.15f;
        [SerializeField][TextArea] private string description;

        public UpgradeCategory Category => category;
        public string DisplayName => displayName;
        public int Level => level;
        public float Cost => cost;
        public float EffectMultiplier => effectMultiplier;
        public string Description => description;

        private void OnValidate()
        {
            if (level < 1) level = 1;
            if (cost < 0f) cost = 0f;
            if (effectMultiplier < 0.1f) effectMultiplier = 0.1f;
        }
    }
}
