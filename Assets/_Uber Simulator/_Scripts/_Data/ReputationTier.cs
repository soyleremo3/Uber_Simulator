namespace DeliverySim
{
    /// <summary>
    /// Reputation tiers. Order matters: higher value = better tier,
    /// comparisons like (currentTier >= order.MinReputationTier) rely on it.
    /// </summary>
    public enum ReputationTier
    {
        Bronze = 0,
        Silver = 1,
        Gold = 2,
        Diamond = 3
    }
}
