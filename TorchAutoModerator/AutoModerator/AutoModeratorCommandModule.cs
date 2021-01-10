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
    [Category("lg")]
    public sealed class AutoModeratorCommandModule : CommandModule
    {
        AutoModeratorPlugin Plugin => (AutoModeratorPlugin) Context.Plugin;

        [Command("on", "Enable broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void EnableBroadcasting() => this.CatchAndReport(() => { Plugin.Config.EnableBroadcasting = true; });

        [Command("off", "Disable broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void DisableBroadcasting() => this.CatchAndReport(() => { Plugin.Config.EnableBroadcasting = false; });

        [Command("mspf", "Get or set the current ms/f threshold per online member.")]
        [Permission(MyPromoteLevel.Admin)]
        public void MspfThreshold() => this.CatchAndReport(() =>
        {
            if (!Context.Args.Any())
            {
                var currentThreshold = Plugin.Config.ThresholdMspf;
                Context.Respond($"{currentThreshold:0.000}mspf per online member");
                return;
            }

            var arg = Context.Args[0];
            if (!float.TryParse(arg, out var newThreshold))
            {
                Context.Respond($"Failed to parse threshold value: {arg}", Color.Red);
                return;
            }

            Plugin.Config.ThresholdMspf = newThreshold;
            Context.Respond($"Set new threshold: {newThreshold:0.000}mspf per online member");
        });

        [Command("ss", "Get or set the current sim speed threshold.")]
        [Permission(MyPromoteLevel.Admin)]
        public void SimSpeedThreshold() => this.CatchAndReport(() =>
        {
            if (!Context.Args.Any())
            {
                var value = Plugin.Config.SimSpeedThreshold;
                Context.Respond($"{value:0.00}ss");
                return;
            }

            var arg = Context.Args[0];
            if (!double.TryParse(arg, out var newThreshold))
            {
                Context.Respond($"Failed to parse threshold value: {arg}", Color.Red);
                return;
            }

            Plugin.Config.SimSpeedThreshold = newThreshold;
            Context.Respond($"Set new threshold: {newThreshold:0.000}ss");
        });

        [Command("admins-only", "Get or set the current \"admins only\" value.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AdminsOnly() => this.CatchAndReport(() =>
        {
            if (!Context.Args.Any())
            {
                var value = Plugin.Config.AdminsOnly;
                Context.Respond($"{value}");
                return;
            }

            var arg = Context.Args[0];
            if (!bool.TryParse(arg, out var newValue))
            {
                Context.Respond($"Failed to parse bool value: {arg}", Color.Red);
                return;
            }

            Plugin.Config.AdminsOnly = newValue;
            Context.Respond($"Set admins-only: {newValue}");
        });

        [Command("clear", "Clear all custom GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ClearCustomGps() => this.CatchAndReport(() => { Plugin.DeleteAllTrackedGpss(); });

        [Command("show", "Show the list of custom GPS entities.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ShowCustomGpsEntities() => this.CatchAndReport(() =>
        {
            var gpss = Plugin.GetAllTrackedGpsEntities();

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
        });

        [Command("unmute_all", "Force every player to unmute broadcasting.")]
        [Permission(MyPromoteLevel.Admin)]
        public void UnmuteBroadcastsToAll() => this.CatchAndReport(() => { Plugin.Config.RemoveAllMutedPlayers(); });

        [Command("profile", "Profile laggy grids.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Profile() => this.CatchAndReport(async () =>
        {
            var profileTime = 5;
            var broadcastResults = false;
            var remainingTimeSecs = 600;

            foreach (var arg in Context.Args)
            {
                if (CommandOption.TryGetOption(arg, out var option))
                {
                    if (option.TryParseInt("time", out profileTime) ||
                        option.TryGetParameterlessBool("broadcast", out broadcastResults) ||
                        option.TryParseInt("keep", out remainingTimeSecs))
                    {
                        continue;
                    }

                    Context.Respond($"Unknown option: {arg}", Color.Red);
                    return;
                }
            }

            Context.Respond($"Profiling (profile time: {profileTime}s, broadcast: {broadcastResults} for {remainingTimeSecs}s)...");

            var mask = new GameEntityMask(null, null, null);
            using (var profiler = Plugin.GetProfiler(mask))
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
                    await Plugin.Broadcast(profileResults, remainingTimeSecs.Seconds());
                    Context.Respond("Done broadcasting");
                }
            }
        });

        [Command("me", "Profile player grids.")]
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
            using (var gridLagProfiler = Plugin.GetProfiler(mask))
            using (var blockDefProfiler = new BlockDefinitionProfiler(mask))
            using (ProfilerResultQueue.Profile(gridLagProfiler))
            {
                gridLagProfiler.MarkStart();
                blockDefProfiler.MarkStart();

                await Task.Delay(profileTime, Plugin.GetCancellationToken());

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