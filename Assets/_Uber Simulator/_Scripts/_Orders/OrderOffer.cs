using UnityEngine;

namespace DeliverySim
{
    [System.Flags]
    public enum OfferFlags
    {
        None = 0,
        Priority = 1 << 0,
        Rush = 1 << 1,
        LongHaul = 1 << 2,
        AwkwardDrop = 1 << 3,
        RegularCustomer = 1 << 4
    }

    /// <summary>
    /// One concrete offer on the board: an <see cref="OrderData"/> template plus
    /// runtime-chosen points / customer / rolls, with pay + time DERIVED from the
    /// real pickup-&gt;delivery distance (see docs/design/order-board-redesign.md).
    /// Not a ScriptableObject — a plain per-board instance, so <see cref="Ttl"/> can
    /// count down while it sits on the board.
    /// </summary>
    public class OrderOffer
    {
        public OrderData Template;
        public string PickupPointId;
        public string DeliveryPointId;
        public CustomerInstance Customer;

        public float DistanceMeters;
        public float Payment;
        public float TimeLimit;
        public float SurgeMultiplier = 1f;
        public OfferFlags Flags = OfferFlags.None;

        /// <summary>Seconds remaining before this offer leaves the board on its own.</summary>
        public float Ttl;
        public string DisplayName;

        public CargoType CargoType => Template != null ? Template.CargoType : CargoType.Package;

        public ReputationTier MinReputationTier =>
            Template != null ? Template.MinReputationTier : ReputationTier.Bronze;

        public bool HasFlag(OfferFlags flag)
        {
            return (Flags & flag) != 0;
        }
    }
}
