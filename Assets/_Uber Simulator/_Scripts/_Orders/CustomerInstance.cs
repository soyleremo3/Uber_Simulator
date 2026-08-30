namespace DeliverySim
{
    public enum CustomerType
    {
        Individual,
        Restaurant,
        Shop,
        Corporate,
        Clinic
    }

    /// <summary>
    /// Runtime instance of the customer attached to one offer
    /// (order-board redesign, spec D). Plain data — the pool that produces these is
    /// a ScriptableObject, but an instance is per-offer.
    /// </summary>
    public class CustomerInstance
    {
        public string DisplayName;
        public CustomerType Type;

        /// <summary>Stable id (hash of DisplayName) used to track "regular customer" progress across saves.</summary>
        public string CustomerId;

        public int CompletedForThisCustomer;
        public bool IsRegular;
    }
}
