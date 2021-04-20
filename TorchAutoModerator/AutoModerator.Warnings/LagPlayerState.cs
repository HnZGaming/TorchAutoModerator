namespace AutoModerator.Warnings
{
    public sealed class LagPlayerState
    {
        public LagQuest Quest { get; set; }
        public LagWarningSource Latest { get; set; }
        public double LastWarningLagNormal { get; set; }

        // takes copy
        public LagPlayerState Snapshot() => new LagPlayerState
        {
            Quest = Quest,
            Latest = Latest,
            LastWarningLagNormal = LastWarningLagNormal,
        };
    }
}