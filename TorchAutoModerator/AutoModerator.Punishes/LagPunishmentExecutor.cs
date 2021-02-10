using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using Utils.General;
using Utils.Torch;
using VRage.Game;

namespace AutoModerator.Punishes
{
    public sealed class LagPunishmentExecutor
    {
        public interface IConfig
        {
            double PunishmentInitialIdleTime { get; }
            LagPunishmentType PunishmentType { get; }
            double DamageNormalPerInterval { get; }
        }

        const int ProcessedBlockCountPerFrame = 100;

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;

        public LagPunishmentExecutor(IConfig config)
        {
            _config = config;
        }

        public void Clear()
        {
        }

        public async Task Update(IReadOnlyDictionary<long, LagPunishmentSource> lags)
        {
            var elapsedSessionTime = MySession.Static.ElapsedPlayTime;
            Log.Trace($"elapsed play time: {elapsedSessionTime.TotalSeconds:0}secs");
            if (elapsedSessionTime < _config.PunishmentInitialIdleTime.Seconds())
            {
                Log.Debug("skipped because server just started");
                return;
            }

            await GameLoopObserver.MoveToGameLoop();

            foreach (var (gridId, lag) in lags)
            {
                if (!lag.IsPinned) continue;

                if (!VRageUtils.TryGetCubeGridById(gridId, out var grid))
                {
                    Log.Warn($"grid not found for grid id: {gridId}");
                    continue;
                }

                await PunishGrid(grid);
                await GameLoopObserver.MoveToGameLoop();

                Log.Trace($"finished \"{grid.DisplayName}\" {_config.PunishmentType}");
            }

            await TaskUtils.MoveToThreadPool();
        }

        async Task PunishGrid(MyCubeGrid grid)
        {
            Thread.CurrentThread.ThrowIfNotSessionThread();

            var blocks = grid.GetFatBlocks();
            for (var i = 0; i < blocks.Count; i++)
            {
                if (i % ProcessedBlockCountPerFrame == 0)
                {
                    await GameLoopObserver.MoveToGameLoop();
                }

                var block = blocks[i];
                if (block != null)
                {
                    PunishBlock(block);
                }
            }
        }

        void PunishBlock(MyCubeBlock block)
        {
            Thread.CurrentThread.ThrowIfNotSessionThread();

            switch (_config.PunishmentType)
            {
                case LagPunishmentType.None:
                {
                    return;
                }
                case LagPunishmentType.Disable:
                {
                    if (block is IMyFunctionalBlock functionalBlock)
                    {
                        if (block is MyParachute) return;
                        if (block is MyButtonPanel) return;
                        if (block is IMyPowerProducer) return;

                        functionalBlock.Enabled = false;
                    }

                    return;
                }
                case LagPunishmentType.Damage:
                {
                    var slimBlock = block.SlimBlock;
                    var damage = slimBlock.BlockDefinition.MaxIntegrity * (float) _config.DamageNormalPerInterval;
                    slimBlock.DoDamage(damage, MyDamageType.Fire);

                    return;
                }
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}