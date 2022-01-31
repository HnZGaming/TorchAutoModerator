using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Torch.Commands;
using Torch.Commands.Permissions;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;
using VRageMath;

namespace AutoModerator
{
    public sealed partial class AutoModeratorCommandModule
    {
        [Command("profile", "Self-profiler for players. `-this` or `-name=` to profile a specific grid.")]
        [Permission(MyPromoteLevel.None)]
        public void ProfilePlayer() => this.CatchAndReport(async () =>
        {
            WarnIfIdle();

            Context.Player.ThrowIfNull("must be called by a player");

            // parse options
            var playerId = Context.Player.IdentityId;
            var gridId = (long?)null;
            var profileTime = 5.Seconds();
            var count = 4;
            foreach (var arg in Context.Args)
            {
                if (!CommandOption.TryGetOption(arg, out var option)) continue;

                if (option.IsParameterless("this"))
                {
                    var (found, grid) = await Context.Player.TryGetSelectedGrid();
                    if (!found)
                    {
                        Context.Respond("Grid not found", Color.Red);
                        return;
                    }

                    if (!grid.BigOwners.TryGetFirst(out var ownerId) || ownerId != playerId)
                    {
                        Context.Respond($"Not your grid: {grid.DisplayName}", Color.Red);
                        return;
                    }

                    gridId = grid.EntityId;
                    continue;
                }

                if (option.TryParse("name", out var name))
                {
                    if (!MyEntities.TryGetEntityByName(name, out var entity) ||
                        !(entity is MyCubeGrid grid))
                    {
                        Context.Respond($"Grid not found by name: {name}", Color.Red);
                        return;
                    }

                    gridId = grid.EntityId;
                    continue;
                }

                if (option.TryParseInt("time", out var time))
                {
                    profileTime = time.Seconds();
                    continue;
                }

                if (option.TryParseInt("count", out count))
                {
                    continue;
                }

                Context.Respond($"Unknown argument: {arg}", Color.Red);
                return;
            }

            Log.Debug($"player \"{Context.Player.DisplayName}\" self-profile; player: {playerId}, grid: {gridId}");

            Context.Respond($"Profiling for {profileTime.TotalSeconds} seconds...");

            var msgBuilder = new StringBuilder();
            msgBuilder.AppendLine();

            var mask = new GameEntityMask(playerMask: playerId, gridMask: gridId, exemptBlockTypeIds: Plugin.Config.ProfileExemptBlockTypeIds);
            using (var gridProfiler = new GridProfiler(mask))
            using (ProfilerResultQueue.Profile(gridProfiler))
            using (var blockProfiler = new BlockDefinitionProfiler(mask))
            using (ProfilerResultQueue.Profile(blockProfiler))
            {
                gridProfiler.MarkStart();
                blockProfiler.MarkStart();

                await Task.Delay(profileTime);

                msgBuilder.AppendLine("Grids (% among all your grids):");

                var gridProfileResult = gridProfiler.GetResult();
                foreach (var (grid, percentage) in GetRelativeTimes(gridProfileResult, count))
                {
                    msgBuilder.AppendLine($"> {grid.DisplayName} {percentage * 100:0}%");
                }

                msgBuilder.AppendLine();
                msgBuilder.AppendLine("Blocks (% among all your blocks):");

                var blockProfilerResult = blockProfiler.GetResult();
                foreach (var (block, percentage) in GetRelativeTimes(blockProfilerResult, count))
                {
                    msgBuilder.AppendLine($"> {block.BlockPairName} {percentage * 100:0}%");
                }

                msgBuilder.AppendLine();
                msgBuilder.AppendLine("For other commands, type !lag commands");

                Context.Respond(msgBuilder.ToString(), "AutoModerator");
            }

            AutoModerator.OnSelfProfiled(playerId);
        });

        static IEnumerable<(T Entity, double NormalTime)> GetRelativeTimes<T>(BaseProfilerResult<T> result, int count)
        {
            var times = new List<(T Entity, double Time)>();
            foreach (var (entity, profilerEntry) in result.GetTopEntities(count))
            {
                times.Add((entity, profilerEntry.MainThreadTime));
            }

            var totalTime = times.Sum(t => t.Time);
            foreach (var (entity, time) in times)
            {
                var normalTime = time / totalTime;
                yield return (entity, normalTime);
            }
        }
    }
}