using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
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
    [Category("lag")]
    public sealed class AutoModeratorCommandModule : CommandModule
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        AutoModeratorPlugin Plugin => (AutoModeratorPlugin) Context.Plugin;

        [Command("configs", "Get or set config")]
        [Permission(MyPromoteLevel.Admin)]
        public void GetOrSetConfig() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config);
        });

        [Command("commands", "Get a list of commands")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowCommandList() => this.CatchAndReport(() =>
        {
            this.ShowCommands();
        });

        [Command("clear", "Clear all internal state.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Clear() => this.CatchAndReport(() =>
        {
            Plugin.ClearCache();
        });

        [Command("gpslist", "Show the list of custom GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowGpss() => this.CatchAndReport(() =>
        {
            var gpss = Plugin.GetAllGpss();

            if (!gpss.Any())
            {
                Context.Respond("No GPS entities found");
                return;
            }

            var msgBuilder = new StringBuilder();
            foreach (var gps in gpss)
            {
                msgBuilder.AppendLine($"> {gps.Name}");
            }

            Context.Respond($"Custom GPS entities: \n{msgBuilder}");
        });

        [Command("mute", "Mute broadcasting.")]
        [Permission(MyPromoteLevel.None)]
        public void MuteBroadcastsToPlayer() => this.CatchAndReport(() =>
        {
            if (Context.Player == null)
            {
                Context.Respond("Can be called by a player only", Color.Red);
                return;
            }

            Plugin.Config.AddMutedPlayer(Context.Player.SteamUserId);
            Context.Respond("Muted broadcasting. It may take some time to take effect.");
        });

        [Command("unmute", "Unmute broadcasting.")]
        [Permission(MyPromoteLevel.None)]
        public void UnmuteBroadcastsToPlayer() => this.CatchAndReport(() =>
        {
            if (Context.Player == null)
            {
                Context.Respond("Can be called by a player only", Color.Red);
                return;
            }

            Plugin.Config.RemoveMutedPlayer(Context.Player.SteamUserId);
            Context.Respond("Unmuted broadcasting. It may take some time to take effect.");
        });

        [Command("unmuteall", "Force every player to unmute broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void UnmuteBroadcastsToAll() => this.CatchAndReport(() =>
        {
            Plugin.Config.RemoveAllMutedPlayers();
        });

        [Command("clearwarning", "Clear quest HUD.")]
        [Permission(MyPromoteLevel.None)]
        public void ClearQuests() => this.CatchAndReport(() =>
        {
            Context.Player.ThrowIfNull("must be called by a player");
            Plugin.ClearQuestForUser(Context.Player.IdentityId);
        });

        [Command("scan", "Self-profiler for players. `-this` or `-name=` to profile a specific grid.")]
        [Permission(MyPromoteLevel.None)]
        public void ProfilePlayer() => this.CatchAndReport(async () =>
        {
            Context.Player.ThrowIfNull("must be called by a player");

            // parse all options
            var playerId = Context.Player.IdentityId;
            var gridId = (long?) null;
            var profileTime = 5.Seconds();
            var count = 4;
            foreach (var arg in Context.Args)
            {
                if (!CommandOption.TryGetOption(arg, out var option)) continue;

                if (option.IsParameterless("this"))
                {
                    if (Context.Player.TryGetSelectedGrid(out var grid))
                    {
                        if (!grid.BigOwners.TryGetFirst(out var ownerId) ||
                            ownerId != playerId)
                        {
                            Context.Respond($"Not your grid: {grid.DisplayName}", Color.Red);
                            return;
                        }

                        gridId = grid.EntityId;
                        continue;
                    }

                    Context.Respond("Grid not found", Color.Red);
                    return;
                }

                if (option.TryParse("name", out var name))
                {
                    if (MyEntities.TryGetEntityByName(name, out var entity) &&
                        entity is MyCubeGrid grid)
                    {
                        gridId = grid.EntityId;
                        continue;
                    }

                    Context.Respond($"Grid not found by name: {name}", Color.Red);
                    return;
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

            var mask = new GameEntityMask(playerId, gridId, null);
            using (var gridProfiler = new GridProfiler(mask))
            using (ProfilerResultQueue.Profile(gridProfiler))
            using (var blockProfiler = new BlockDefinitionProfiler(mask))
            using (ProfilerResultQueue.Profile(blockProfiler))
            {
                gridProfiler.MarkStart();
                blockProfiler.MarkStart();

                await Task.Delay(profileTime);

                msgBuilder.AppendLine("Grid lags (% of max lag per grid):");

                var gridProfileResult = gridProfiler.GetResult();
                foreach (var (grid, profilerEntry) in gridProfileResult.GetTopEntities(count))
                {
                    var gridName = grid.DisplayName;
                    var mspf = profilerEntry.MainThreadTime / gridProfileResult.TotalFrameCount;
                    var lagNormal = mspf / Plugin.Config.MaxGridMspf;
                    msgBuilder.AppendLine($"\"{gridName}\" {lagNormal * 100:0}%");
                }

                msgBuilder.AppendLine();
                msgBuilder.AppendLine("Block lags (% of total):");

                var blockLags = new Dictionary<string, double>();
                var blockProfilerResult = blockProfiler.GetResult();
                foreach (var (block, profileEntity) in blockProfilerResult.GetTopEntities(count))
                {
                    var blockName = block.BlockPairName;

                    blockLags.TryGetValue(blockName, out var lag);
                    lag += profileEntity.MainThreadTime;

                    blockLags[blockName] = lag;
                    Log.Trace($"player \"{Context.Player.DisplayName}\" self-profile block {blockName} {lag:0.00}ms");
                }

                var totalBlockLag = blockLags.Values.Sum();
                foreach (var (blockName, lag) in blockLags.OrderByDescending(p => p.Value))
                {
                    var relLag = lag / totalBlockLag;
                    msgBuilder.AppendLine($"{blockName} {relLag * 100:0}%");
                }

                Context.Respond(msgBuilder.ToString());
            }

            Plugin.OnSelfProfiled(playerId);
        });
    }
}