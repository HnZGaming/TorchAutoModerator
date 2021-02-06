using Sandbox.Game.Screens.Helpers;

namespace AutoModerator.Broadcast
{
    public interface IEntityGpsSource
    {
        long AttachedEntityId { get; }
        double LagNormal { get; }
        bool TryCreateGps(out MyGps gps);
    }
}