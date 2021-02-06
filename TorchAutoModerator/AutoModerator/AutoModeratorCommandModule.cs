using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoModerator.Quests;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.World;
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
        AutoModeratorPlugin Plugin => (AutoModeratorPlugin) Context.Plugin;

        [Command("broadcast", "Enable/disable broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void GetOrSetEnabled() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config, nameof(AutoModeratorConfig.EnableBroadcasting));
        });

        [Command("grid_mspf", "Get or set the current ms/f threshold per grid.")]
        [Permission(MyPromoteLevel.Admin)]
        public void GridMspfThreshold() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config, nameof(AutoModeratorConfig.GridMspfThreshold));
        });

        [Command("player_mspf", "Get or set the current ms/f threshold per player.")]
        [Permission(MyPromoteLevel.Admin)]
        public void PlayerMspfThreshold() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config, nameof(AutoModeratorConfig.PlayerMspfThreshold));
        });

        [Command("admins-only", "Get or set the current \"admins only\" value.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AdminsOnly() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config, nameof(AutoModeratorConfig.AdminsOnly));
        });

        [Command("clear_gps", "Clear all custom GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ClearGpss() => this.CatchAndReport(() =>
        {
            Plugin.DeleteAllGpss();
        });

        [Command("show_gps", "Show the list of custom GPS entities.")]
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

        [Command("check", "Show if the player is receiving a broadcast")]
        [Permission(MyPromoteLevel.None)]
        public void CheckReceive(string playerName = null) => this.CatchAndReport(() =>
        {
            var player = Context.Player ?? MySession.Static.Players.GetPlayerByName(playerName);
            if (player == null)
            {
                Context.Respond($"Player not found: \"{playerName}\"", Color.Red);
                return;
            }

            var msg = Plugin.CheckPlayerReceivesGpss(player as MyPlayer)
                ? $"Player \"{playerName}\" does receive broadcasts"
                : $"Player \"{playerName}\" does not receive broadcasts";

            Context.Respond(msg);
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

        [Command("unmute_all", "Force every player to unmute broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void UnmuteBroadcastsToAll() => this.CatchAndReport(() =>
        {
            Plugin.Config.RemoveAllMutedPlayers();
        });

        [Command("profile", "Self profiler interface.")]
        [Permission(MyPromoteLevel.None)]
        public void ProfilePlayer() => this.CatchAndReport(async () =>
        {
            Context.Player.ThrowIfNull("must be called by a player");

            // parse all options
            var playerId = Context.Player.IdentityId;
            var gridId = (long?) null;
            var profileTime = 10.Seconds();
            var count = 3;
            foreach (var arg in Context.Args)
            {
                if (!CommandOption.TryGetOption(arg, out var option)) continue;

                if (option.IsParameterless("this"))
                {
                    var grid = Context.Player?.Controller?.ControlledEntity?.Entity;
                    if (grid == null)
                    {
                        Context.Respond("You're not sitting in a control seat", Color.Red);
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

            Context.Respond($"Profiling for {profileTime.TotalSeconds} seconds...");

            var msgBuilder = new StringBuilder();
            msgBuilder.AppendLine();

            var mask = new GameEntityMask(playerId, gridId, null);
            using (var gridProfiler = new GridProfiler(mask))
            using (var blockDefProfiler = new BlockDefinitionProfiler(mask))
            using (ProfilerResultQueue.Profile(gridProfiler))
            {
                gridProfiler.MarkStart();
                blockDefProfiler.MarkStart();

                await Task.Delay(profileTime);

                msgBuilder.AppendLine("Grid lags (% of threshold):");

                var profileResult = gridProfiler.GetResult();
                foreach (var (grid, profilerEntry) in profileResult.GetTopEntities(count))
                {
                    var gridName = grid.DisplayName;
                    var mspf = profilerEntry.MainThreadTime / profileResult.TotalTime;
                    var lagNormal = mspf / Plugin.Config.GridMspfThreshold;
                    var lagStr = $"{lagNormal * 100:0}%";
                    msgBuilder.AppendLine($"\"{gridName}\" {lagStr}");
                }

                msgBuilder.AppendLine();
                msgBuilder.AppendLine("Block type lags (% of total):");

                var blockDefProfilerResult = blockDefProfiler.GetResult();
                foreach (var (blockDef, profileEntity) in blockDefProfilerResult.GetTopEntities(4))
                {
                    var blockName = blockDef.BlockPairName;
                    var lag = profileEntity.TotalTime / blockDefProfilerResult.TotalTime;
                    var lagStr = $"{lag * 100:0}%";
                    msgBuilder.AppendLine($"{blockName} {lagStr}");
                }

                Context.Respond(msgBuilder.ToString());
            }

            Plugin.OnSelfProfiled(playerId);
        });
    }
}