namespace TaskbarHero
{
    /// <summary>
    /// Summary of what the hero earned while the app was closed, produced by
    /// <see cref="IdleRpg.FastForward"/> and shown to the player as a welcome-back toast.
    /// </summary>
    public struct OfflineResult
    {
        public long GoldGained;
        public int StagesGained;
        public int LevelsGained;
        public double SecondsSimulated;

        public bool HasAnything => SecondsSimulated > 0 && (GoldGained > 0 || StagesGained > 0 || LevelsGained > 0);
    }
}
