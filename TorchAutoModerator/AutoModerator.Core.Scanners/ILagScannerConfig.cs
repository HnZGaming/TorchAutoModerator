namespace AutoModerator.Core.Scanners
{
    public interface ILagScannerConfig
    {
        /// <summary>
        /// Minimum ms/f per member count to be broadcasted to players.
        /// </summary>
        double MspfPerOnlineGroupMember { get; }
    }
}