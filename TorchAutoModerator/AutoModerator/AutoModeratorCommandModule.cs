using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        [Command("enable", "Enable/disable broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void GetOrSetEnabled() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config, nameof(AutoModeratorConfig.EnableBroadcasting));
        });

        [Command("grid_mspf", "Get or set the current ms/f threshold per online member.")]
        [Permission(MyPromoteLevel.Admin)]
        public void GridMspfThreshold() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config, nameof(AutoModeratorConfig.GridMspfThreshold));
        });

        [Command("admins-only", "Get or set the current \"admins only\" value.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AdminsOnly() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config, nameof(AutoModeratorConfig.AdminsOnly));
        });

        [Command("clear", "Clear all custom GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ClearCustomGps() => this.CatchAndReport(() =>
        {
            Plugin.DeleteAllGpss();
        });

        [Command("show", "Show the list of custom GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowCustomGpsEntities() => this.CatchAndReport(() =>
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

            var doesReceive = Plugin.CheckPlayerReceivesGpss(player as MyPlayer);
            var msg = doesReceive
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
            long? playerMask = Context.Player.IdentityId;
            long? factionMask = null;
            long? gridMask = null;
            var profileTime = 10.Seconds();
            var top = 3;
            foreach (var arg in Context.Args)
            {
                if (!CommandOption.TryGetOption(arg, out var option)) continue;

                if (option.IsParameterless("faction"))
                {
                    var faction = MySession.Static.Factions.GetPlayerFaction(Context.Player.IdentityId);
                    if (faction == null)
                    {
                        Context.Respond("Faction not found", Color.Red);
                        return;
                    }

                    factionMask = faction.FactionId;
                    playerMask = null;
                    continue;
                }

                if (option.IsParameterless("this"))
                {
                    var grid = Context.Player?.Controller?.ControlledEntity?.Entity;
                    if (grid == null)
                    {
                        Context.Respond("Grid not found", Color.Red);
                        return;
                    }

                    gridMask = grid.EntityId;
                    continue;
                }

                if (option.TryParseInt("time", out var time))
                {
                    profileTime = time.Seconds();
                    continue;
                }

                if (option.TryParseInt("top", out top))
                {
                    continue;
                }

                Context.Respond($"Unknown argument: {arg}", Color.Red);
                return;
            }

            Context.Respond($"Profiling for {profileTime.TotalSeconds} seconds...");

            var msgBuilder = new StringBuilder();
            msgBuilder.AppendLine();

            var mask = new GameEntityMask(playerMask, gridMask, factionMask);
            using (var gridProfiler = new GridProfiler(mask))
            using (var blockDefProfiler = new BlockDefinitionProfiler(mask))
            using (ProfilerResultQueue.Profile(gridProfiler))
            {
                gridProfiler.MarkStart();
                blockDefProfiler.MarkStart();

                await Task.Delay(profileTime);

                msgBuilder.AppendLine("Performance by grids (% of broadcasting threshold):");

                var profileResult = gridProfiler.GetResult();
                foreach (var (grid, profilerEntry) in profileResult.GetTopEntities(top))
                {
                    var gridName = grid.DisplayName;
                    var mspf = profilerEntry.MainThreadTime / profileResult.TotalTime;
                    var lagNormal = mspf / Plugin.Config.GridMspfThreshold;
                    var lagStr = $"{lagNormal * 100:0}%";
                    msgBuilder.AppendLine($"\"{gridName}\" {lagStr}");
                }

                msgBuilder.AppendLine();
                msgBuilder.AppendLine("Performance by blocks (% of total):");

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
        });
    }
}