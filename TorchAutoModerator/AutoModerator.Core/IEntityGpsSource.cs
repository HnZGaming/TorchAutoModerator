using Sandbox.Game.Screens.Helpers;

namespace AutoModerator.Core
{
    public interface IEntityGpsSource
    {
        long EntityId { get; }
        bool TryCreateGps(int rank, out MyGps gps);
    }
}