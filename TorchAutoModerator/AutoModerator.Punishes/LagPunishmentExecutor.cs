using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Entities;
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
            // move to the game loop so we can synchronously operate on blocks
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

                // move to the next frame so we won't lag the server
                await GameLoopObserver.MoveToGameLoop();

                Log.Trace($"finished \"{grid.DisplayName}\" {_config.PunishmentType}");
            }

            // back to some worker thread
            await TaskUtils.MoveToThreadPool();
        }

        async Task PunishGrid(MyCubeGrid grid)
        {
            var blocks = grid.GetFatBlocks();
            for (var i = 0; i < blocks.Count; i++)
            {
                // move to the next frame so we won't lag the server
                if (i % ProcessedBlockCountPerFrame == 0)
                {
                    await GameLoopObserver.MoveToGameLoop();
                }

                var block = blocks[i];
                if (block == null) continue;

                switch (_config.PunishmentType)
                {
                    case LagPunishmentType.Shutdown:
                    {
                        DisableFunctionalBlock(block);
                        break;
                    }
                    case LagPunishmentType.Damage:
                    {
                        DamageBlock(block);
                        break;
                    }
                }
            }
        }

        void DamageBlock(MyCubeBlock block)
        {
            Thread.CurrentThread.ThrowIfNotSessionThread();

            var slimBlock = block.SlimBlock;
            var damage = slimBlock.BlockDefinition.MaxIntegrity * (float) _config.DamageNormalPerInterval;
            slimBlock.DoDamage(damage, MyDamageType.Fire);
        }

        void DisableFunctionalBlock(MyCubeBlock block)
        {
            Thread.CurrentThread.ThrowIfNotSessionThread();

            if (block is IMyFunctionalBlock functionalBlock)
            {
                if (block is MyParachute) return;
                if (block is MyButtonPanel) return;
                if (block is IMyPowerProducer) return;

                functionalBlock.Enabled = false;
            }
        }
    }
}