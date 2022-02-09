using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Utils.General;
using Utils.Torch;
using VRage.Game;
using VRage.Game.ModAPI;

namespace AutoModerator.Punishes
{
    public sealed class PunishExecutor
    {
        public interface IConfig
        {
            PunishType PunishType { get; }
            double DamageNormalPerInterval { get; }
            double MinIntegrityNormal { get; }
        }

        const int ProcessedBlockCountPerFrame = 100;

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly BlockTypePairCollection _exemptBlockPairs;
        readonly HashSet<long> _punishedIds;

        public PunishExecutor(IConfig config, BlockTypePairCollection exemptBlockPairs)
        {
            _config = config;
            _exemptBlockPairs = exemptBlockPairs;
            _punishedIds = new HashSet<long>();
        }

        public void Clear()
        {
            _punishedIds.Clear();
        }

        public async Task Update(IReadOnlyDictionary<long, PunishSource> lags)
        {
            // move to the game loop so we can synchronously operate on blocks
            await VRageUtils.MoveToGameLoop();

            foreach (var (gridId, lag) in lags)
            {
                if (!lag.IsPinned) continue;

                if (!VRageUtils.TryGetCubeGridById(gridId, out var grid))
                {
                    Log.Warn($"grid not found for grid id: {gridId}");
                    continue;
                }

                if (!_punishedIds.Contains(gridId))
                {
                    _punishedIds.Add(gridId);
                    Log.Info($"Punished: \"{grid.DisplayName}\" type: {_config.PunishType}");
                }

                await PunishGrid(grid);
                
                Log.Debug($"block punish: \"{grid.Name}\" <{lag.GridId}> {_config.PunishType}");

                // move to the next frame so we won't lag the server
                await VRageUtils.MoveToGameLoop();
            }

            foreach (var existingId in _punishedIds)
            {
                if (!lags.ContainsKey(existingId))
                {
                    var name = VRageUtils.TryGetCubeGridById(existingId, out var g) ? $"\"{g.DisplayName}\"" : $"<{existingId}>";
                    Log.Info($"Done punishment: {name}");
                }
            }

            _punishedIds.ExceptWith(lags.Keys);

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
                    await VRageUtils.MoveToGameLoop();
                }

                var block = blocks[i];
                if (block == null) continue;

                switch (_config.PunishType)
                {
                    case PunishType.Shutdown:
                    {
                        DisableFunctionalBlock(block);
                        break;
                    }
                    case PunishType.Damage:
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

            if (IsExemptBlock(block)) return;

            var slimBlock = block.SlimBlock;
            var maxIntegrity = slimBlock.BlockDefinition.MaxIntegrity;
            if (slimBlock.Integrity / maxIntegrity > _config.MinIntegrityNormal)
            {
                var damage = maxIntegrity * (float) _config.DamageNormalPerInterval;
                slimBlock.DoDamage(damage, MyDamageType.Fire, true, null, 0);
            }
        }

        void DisableFunctionalBlock(MyCubeBlock block)
        {
            Thread.CurrentThread.ThrowIfNotSessionThread();

            if (IsExemptBlock(block)) return;

            if (block is IMyFunctionalBlock functionalBlock)
            {
                functionalBlock.Enabled = false;
            }
        }

        bool IsExemptBlock(IMyCubeBlock block)
        {
            var blockDef = block.BlockDefinition;
            return _exemptBlockPairs.Contains(blockDef.TypeIdString, blockDef.SubtypeId);
        }
    }
}