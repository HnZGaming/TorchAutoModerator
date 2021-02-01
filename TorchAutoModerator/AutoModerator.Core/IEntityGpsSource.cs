using Sandbox.Game.Screens.Helpers;

namespace AutoModerator.Core
{
    public interface IEntityGpsSource
    {
        long AttachedEntityId { get; }
        double LagNormal { get; }
        bool TryCreateGps(out MyGps gps);
    }
}