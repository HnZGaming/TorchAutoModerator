using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoModerator.Core;
using AutoModerator.Warnings;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using Utils.General;
using Utils.TimeSerieses;
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
            Context.Respond("cleared all internal state");
        });

        [Command("gpslist", "Show the list of GPS entities.")]
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

        [Command("inspect", "Show the time series of specified grid or player.")]
        [Permission(MyPromoteLevel.None)]
        public void Inspect() => this.CatchAndReport(() =>
        {
            var asNormalPlayer = (Context.Player?.PromoteLevel ?? MyPromoteLevel.Admin) == MyPromoteLevel.None;
            var playerId = Context.Player?.IdentityId ?? 0L;
            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var memberIds = faction?.Members.Keys.ToSet() ?? new HashSet<long> {playerId};

            var top = 5;
            var showOutlierTests = false;
            var specificGridIdOrNull = (long?) null;
            foreach (var arg in Context.Args)
            {
                if (CommandOption.TryGetOption(arg, out var option))
                {
                    if (option.TryParseInt("top", out top)) continue;
                    if (option.TryGetParameterlessBool("outlier", out showOutlierTests)) continue;

                    if (option.TryGetParameterlessBool("this", out _))
                    {
                        if (playerId == 0L)
                        {
                            Context.Respond("Players only: `this`", Color.Red);
                            return;
                        }

                        if (Context.Player.TryGetSelectedGrid(out var grid))
                        {
                            if (!grid.BigOwners.TryGetFirst(out var ownerId) || ownerId != playerId)
                            {
                                Context.Respond($"Not your grid: {grid.DisplayName}", Color.Red);
                                return;
                            }

                            specificGridIdOrNull = grid.EntityId;
                            continue;
                        }

                        Context.Respond("Grid not found", Color.Red);
                        return;
                    }

                    if (option.TryGetParameterlessBool("mine", out asNormalPlayer))
                    {
                        if (Context.Player?.PromoteLevel != MyPromoteLevel.Admin)
                        {
                            Context.Respond("Option allowed for admins only: all");
                            return;
                        }

                        continue;
                    }

                    Context.Respond($"Invalid option: {arg}", Color.Red);
                    return;
                }
            }

            // inspect specific entity
            if (Context.Args.TryGetElementAt(0, out var entityStr))
            {
                if (!long.TryParse(entityStr, out var entityId))
                {
                    if (!Plugin.TryTraverseEntityByName(entityStr, out var entity))
                    {
                        Context.Respond($"Entity not found by name: {entityStr}", Color.Red);
                        return;
                    }

                    entityId = entity.Id;
                }

                if (asNormalPlayer)
                {
                    if (!MyEntities.TryGetEntityById(entityId, out var entity))
                    {
                        Context.Respond("Entity not found", Color.Red);
                        return;
                    }

                    if (entity is IMyCharacter && !memberIds.Contains(entityId))
                    {
                        Context.Respond("Not you or your faction member", Color.Red);
                        return;
                    }

                    if (entity is MyCubeGrid grid)
                    {
                        if (!grid.BigOwners.TryGetFirst(out var ownerId))
                        {
                            Context.Respond("Not owned by anyone", Color.Red);
                            return;
                        }

                        if (!memberIds.Contains(ownerId))
                        {
                            Context.Respond("Not yours or your faction member's grid", Color.Red);
                            return;
                        }
                    }
                }

                InspectEntity(entityId, showOutlierTests);
                return;
            }

            var msgBuilder = new StringBuilder();
            msgBuilder.AppendLine();


            if (!specificGridIdOrNull.HasValue)
            {
                msgBuilder.AppendLine("Players:");
                var inspectablePlayers = new List<TrackedEntitySnapshot>();
                foreach (var s in OrderForInspection(Plugin.GetTrackedPlayers()))
                {
                    if (asNormalPlayer && !memberIds.Contains(s.Id)) continue;

                    inspectablePlayers.Add(s);
                }

                var warningStates = Plugin.GetWarningState();
                if (inspectablePlayers.Any())
                {
                    foreach (var s in inspectablePlayers.Take(top))
                    {
                        var warning = warningStates.GetOrElse(s.Id, null);
                        var line = MakeTrackedEntityLine(s, warning);
                        msgBuilder.AppendLine(line);
                    }
                }
                else
                {
                    msgBuilder.AppendLine("No tracked players found");
                }
            }

            msgBuilder.AppendLine("Grids:");

            var inspectableGrids = new List<TrackedEntitySnapshot>();
            foreach (var s in OrderForInspection(Plugin.GetTrackedGrids()).Take(top))
            {
                if (specificGridIdOrNull is long sid && sid != s.Id) continue;

                if (asNormalPlayer)
                {
                    if (!VRageUtils.TryGetCubeGridById(s.Id, out var grid)) continue;
                    if (!grid.BigOwners.TryGetFirst(out var ownerId)) continue;
                    if (!memberIds.Contains(ownerId)) continue;
                }

                inspectableGrids.Add(s);
            }

            if (inspectableGrids.Any())
            {
                foreach (var s in inspectableGrids.Take(top))
                {
                    var line = MakeTrackedEntityLine(s);
                    msgBuilder.AppendLine(line);
                }
            }
            else
            {
                msgBuilder.AppendLine("No tracked grids found");
            }

            Context.Respond(msgBuilder.ToString());
        });

        string MakeTrackedEntityLine(TrackedEntitySnapshot s, LagWarningCollection.PlayerState w = null)
        {
            var lagGraph = MakeOnelinerGraph(30, 1, s.LongLagNormal);

            var pinSecs = s.RemainingTime.TotalSeconds;
            var pinSecsNormal = pinSecs / Plugin.Config.PunishTime;
            var pinGraph = MakeOnelinerGraph(30, 1, pinSecsNormal, false);
            pinGraph = s.IsPinned ? $"{pinGraph} pin {pinSecs:0} secs" : "no pin";
            var warning = w?.Quest > LagWarningCollection.LagQuestState.None ? $"warning: {w.Quest} ({w.LastWarningLagNormal * 100:0}%)" : "";

            return $"{lagGraph} {pinGraph} {s.Name} ({s.Id}) {warning}";
        }

        static IEnumerable<TrackedEntitySnapshot> OrderForInspection(IEnumerable<TrackedEntitySnapshot> snapshots)
        {
            var pinnedResults = new List<TrackedEntitySnapshot>();
            var otherResults = new List<TrackedEntitySnapshot>();
            foreach (var snapshot in snapshots)
            {
                if (snapshot.IsPinned)
                {
                    pinnedResults.Add(snapshot);
                }
                else
                {
                    otherResults.Add(snapshot);
                }
            }

            pinnedResults.Sort(TrackedEntitySnapshot.LongLagComparer.Instance);
            otherResults.Sort(TrackedEntitySnapshot.LongLagComparer.Instance);
            return pinnedResults.Concat(otherResults);
        }

        void InspectEntity(long entityId, bool showOutlierTests)
        {
            if (!Plugin.TryGetTimeSeries(entityId, out var timeSeries))
            {
                Context.Respond("Time series not found", Color.Red);
                return;
            }

            if (!Plugin.TryGetTrackedEntity(entityId, out var entity))
            {
                Context.Respond("Tracked entity not found", Color.Red);
                return;
            }

            if (timeSeries.Count == 0)
            {
                Context.Respond("Time series found but empty", Color.Red);
                return;
            }

            var outlierTests = timeSeries.TestOutlier();
            var series = timeSeries.Zip(outlierTests);

            var msgBuilder = new StringBuilder();
            msgBuilder.AppendLine();

            msgBuilder.AppendLine($"{entity.Name} ({entity.Id})");
            msgBuilder.AppendLine($"Owner: {entity.OwnerName} ({entity.OwnerId})");

            if (entity.IsBlessed)
            {
                msgBuilder.AppendLine("Blessed (just spawned)");
            }

            msgBuilder.AppendLine($"Lag (evaluated): {entity.LongLagNormal * 100:0}%");
            msgBuilder.AppendLine(entity.IsPinned ? $"Pinned for next {entity.RemainingTime.TotalSeconds} seconds" : "Not pinned");

            if (Plugin.GetWarningState().TryGetValue(entityId, out var w))
            {
                msgBuilder.AppendLine($"Warning: {w.Quest} (Normal: {w.LastWarningLagNormal * 100:0}%)");
            }

            foreach (var (((_, normal), outlierTest), index) in series.Indexed())
            {
                const int MaxWidth = 30;

                var normalGraph = MakeOnelinerGraph(MaxWidth, 1, normal);
                var outlierGraph = showOutlierTests ? MakeOnelinerGraph(MaxWidth, 3, outlierTest) : "";
                msgBuilder.AppendLine($"{index:000} {normalGraph} {outlierGraph}");
            }

            Context.Respond(msgBuilder.ToString());
        }

        static string MakeOnelinerGraph(int maxWidth, double maxNormal, double normal, bool showLabel = true)
        {
            normal = normal.IsValid() ? normal : 0;
            var graphNormal = Math.Min(1, Math.Max(0, normal / maxNormal));
            var size = Math.Min(maxWidth, (int) (graphNormal * maxWidth));
            var graph0 = Enumerable.Repeat('¦', size);
            var graph1 = Enumerable.Repeat('\'', maxWidth - size);
            var graph = new string(graph0.Concat(graph1).ToArray());
            var label = showLabel ? $" {normal * 100:000}%" : "";
            return $"{graph}{label}";
        }

        [Command("gpsmute", "Mute broadcasting.")]
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

        [Command("gpsunmute", "Unmute broadcasting.")]
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

        [Command("gpsunmuteall", "Force every player to unmute broadcasting.")]
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

        [Command("profile", "Self-profiler for players. `-this` or `-name=` to profile a specific grid.")]
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
                        if (!grid.BigOwners.TryGetFirst(out var ownerId) || ownerId != playerId)
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
                msgBuilder.AppendLine("Block lags (% of total per player or grid):");

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