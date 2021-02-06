using System;
using System.Collections.Generic;
using AutoModerator.Core;
using NLog;
using Profiler.Basics;
using Sandbox.Game.World;

namespace AutoModerator.Players
{
    public sealed class PlayerLagSnapshotCreator
    {
        public interface IConfig
        {
            double PlayerMspfThreshold { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;

        public PlayerLagSnapshotCreator(IConfig config)
        {
            _config = config;
        }

        public IEnumerable<IEntityLagSnapshot> CreateLagSnapshots(BaseProfilerResult<MyIdentity> playerProfileResult)
        {
            foreach (var (player, profilerEntry) in playerProfileResult.GetTopEntities(50))
            {
                var mspf = profilerEntry.MainThreadTime / playerProfileResult.TotalTime;
                var lag = mspf / _config.PlayerMspfThreshold;
                var snapshot = PlayerLagSnapshot.FromPlayer(player, lag);
                yield return snapshot;
            }
        }
    }
}