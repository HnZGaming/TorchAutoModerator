using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoModerator.Core;
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

        [Command("mspf", "Get or set the current ms/f threshold per online member.")]
        [Permission(MyPromoteLevel.Admin)]
        public void MspfThreshold() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config, nameof(AutoModeratorConfig.GridMspfThreshold));
        });

        [Command("ss", "Get or set the current sim speed threshold.")]
        [Permission(MyPromoteLevel.Admin)]
        public void SimSpeedThreshold() => this.CatchAndReport(() =>
        {
            this.GetOrSetProperty(Plugin.Config, nameof(AutoModeratorConfig.SimSpeedThreshold));
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

        [Command("profile", "Profile laggy grids.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Profile() => this.CatchAndReport(async () =>
        {
            var profileTime = 5;
            var broadcastResults = false;

            foreach (var arg in Context.Args)
            {
                if (CommandOption.TryGetOption(arg, out var option))
                {
                    if (option.TryParseInt("time", out profileTime) ||
                        option.TryGetParameterlessBool("broadcast", out broadcastResults))
                    {
                        continue;
                    }

                    Context.Respond($"Unknown option: {arg}", Color.Red);
                    return;
                }
            }

            Context.Respond($"Profiling (profile time: {profileTime}s, broadcast: {broadcastResults}...");

            var mask = new GameEntityMask(null, null, null);
            using (var profiler = new GridLagProfiler(Plugin.Config, mask))
            using (ProfilerResultQueue.Profile(profiler))
            {
                Context.Respond("Profiling...");

                profiler.MarkStart();

                await Task.Delay(profileTime.Seconds());

                var profileResults = profiler.GetProfileResults(4);
                if (!profileResults.Any())
                {
                    Context.Respond("No laggy grids found");
                    return;
                }

                var msgBuilder = new StringBuilder();
                foreach (var report in profileResults)
                {
                    msgBuilder.AppendLine($"> {report}");
                }

                Context.Respond($"Done profiling:\n{msgBuilder}");

                if (broadcastResults)
                {
                    Plugin.BroadcastGpss(profileResults);
                    Context.Respond("Done broadcasting. It may take some time to take effect.");
                }
            }
        });

        [Command("mine", "Profile player grids.")]
        [Permission(MyPromoteLevel.None)]
        public void ProfilePlayer() => this.CatchAndReport(async () =>
        {
            Context.Player.ThrowIfNull("must be called by a player");

            // parse all options
            long? playerMask = Context.Player.IdentityId;
            long? factionMask = null;
            long? gridMask = null;
            var profileTime = 10.Seconds();
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

                Context.Respond($"Unknown argument: {arg}", Color.Red);
                return;
            }

            Context.Respond($"Profiling for {profileTime.TotalSeconds} seconds...");

            var msgBuilder = new StringBuilder();
            msgBuilder.AppendLine();

            var mask = new GameEntityMask(playerMask, gridMask, factionMask);
            using (var gridLagProfiler = new GridLagProfiler(Plugin.Config, mask))
            using (var blockDefProfiler = new BlockDefinitionProfiler(mask))
            using (ProfilerResultQueue.Profile(gridLagProfiler))
            {
                gridLagProfiler.MarkStart();
                blockDefProfiler.MarkStart();

                await Task.Delay(profileTime);

                msgBuilder.AppendLine("Performance by grids (% of broadcasting threshold):");

                var profileResults = gridLagProfiler.GetProfileResults(4);
                foreach (var profileResult in profileResults)
                {
                    var gridName = profileResult.GridName;
                    var playerName = profileResult.PlayerNameOrNull ?? "<none>";
                    var lag = profileResult.ThresholdNormal;
                    var lagStr = $"{lag * 100:0}%";
                    msgBuilder.AppendLine($"\"{gridName}\" {lagStr} by {playerName}");
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