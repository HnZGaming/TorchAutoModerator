namespace AutoModerator.Core
{
    public interface IEntityLagSnapshot
    {
        long EntityId { get; }
        double LagNormal { get; }
        string FactionTagOrNull { get; }
    }
}