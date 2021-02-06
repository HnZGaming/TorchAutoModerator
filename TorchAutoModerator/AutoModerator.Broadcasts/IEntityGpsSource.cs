using Sandbox.Game.Screens.Helpers;

namespace AutoModerator.Broadcasts
{
    public interface IEntityGpsSource
    {
        bool TryCreateGps(out MyGps gps);
    }
}