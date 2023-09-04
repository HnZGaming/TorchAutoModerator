using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AutoModerator.Core;
using AutoModerator.Quests;
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
    public sealed partial class AutoModeratorCommandModule
    {
        [Command("inspect", "Show the time series of specified grid or player.")]
        [Permission(MyPromoteLevel.None)]
        public void Inspect() => this.CatchAndReportAsync(async () =>
        {
            WarnIfIdle();

            var calledByNormalPlayer = (Context.Player?.PromoteLevel ?? MyPromoteLevel.Admin) == MyPromoteLevel.None;
            var playerId = Context.Player?.IdentityId ?? 0L;
            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var memberIds = faction?.Members.Keys.ToSet() ?? new HashSet<long> { playerId };

            var top = 5;
            var showOutlierTests = false;
            var specificEntityIdOrNull = (long?)null;
            foreach (var arg in Context.Args)
            {
                if (!CommandOption.TryGetOption(arg, out var option)) continue;

                if (option.TryParseInt("top", out top)) continue;
                if (option.TryGetParameterlessBool("outlier", out showOutlierTests)) continue;

                if (option.IsParameterless("this"))
                {
                    var (found, grid) = await Context.Player.TryGetSelectedGrid();
                    if (!found)
                    {
                        Context.Respond("Grid not found", Color.Red);
                        return;
                    }

                    specificEntityIdOrNull = grid.EntityId;
                    continue;
                }

                if (option.TryParseLong("id", out var specificEntityId))
                {
                    if (!AutoModerator.TryGetEntity(specificEntityId, out _))
                    {
                        Context.Respond($"Entity not tracked: {specificEntityId}", Color.Red);
                        return;
                    }

                    specificEntityIdOrNull = specificEntityId;
                    continue;
                }

                if (option.TryParse("name", out var entityName))
                {
                    if (!AutoModerator.TryFindEntityByName(entityName, out var entity))
                    {
                        Context.Respond($"Entity not tracked by name: {entityName}", Color.Red);
                        return;
                    }

                    specificEntityIdOrNull = entity.Id;
                    continue;
                }

                if (option.IsParameterless("mine"))
                {
                    if (Context.Player?.PromoteLevel < MyPromoteLevel.Admin)
                    {
                        Context.Respond("Option allowed for admins only: mine", Color.Red);
                        return;
                    }

                    calledByNormalPlayer = true;
                    continue;
                }

                Context.Respond($"Invalid option: {arg}", Color.Red);
                return;
            }

            // inspect specific entity
            if (specificEntityIdOrNull is { } entityId)
            {
                if (calledByNormalPlayer)
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

            var players = new List<TrackedEntity>();
            foreach (var entity in AutoModerator.Players.GetLaggiestEntities())
            {
                if (calledByNormalPlayer)
                {
                    if (!memberIds.Contains(entity.Id)) continue;
                }

                players.Add(entity);
            }

            var grids = new List<TrackedEntity>();
            foreach (var entity in AutoModerator.Grids.GetLaggiestEntities().Take(top))
            {
                if (calledByNormalPlayer)
                {
                    if (!memberIds.Contains(entity.OwnerId)) continue;
                }

                grids.Add(entity);
            }

            var msgBuilder = new StringBuilder();
            msgBuilder.AppendLine();

            if (players.Any())
            {
                msgBuilder.AppendLine("Players:");

                foreach (var s in players)
                {
                    var warning = AutoModerator.TryFindQuestForEntity(s.Id, out var q) ? q : null;
                    var line = MakeTrackedEntityLine(s, warning);
                    msgBuilder.AppendLine(line);
                }
            }
            else
            {
                msgBuilder.AppendLine("No players");
            }

            if (grids.Any())
            {
                msgBuilder.AppendLine("Grids:");

                foreach (var s in grids)
                {
                    var warning = AutoModerator.TryFindQuestForEntity(s.Id, out var q) ? q : null;
                    var line = MakeTrackedEntityLine(s, warning);
                    msgBuilder.AppendLine(line);
                }
            }
            else
            {
                msgBuilder.AppendLine("No grids");
            }

            Context.Respond(msgBuilder.ToString(), "AutoModerator");
        });

        static string MakeTrackedEntityLine(TrackedEntity entity, QuestEntity quest)
        {
            var graph = MakeOnelinerGraph(30, 1, entity.LagNormal);
            var warning = (quest?.Quest ?? Quest.None) switch
            {
                Quest.None => "<ok>",
                Quest.MustProfileSelf => "<warn>",
                Quest.MustDelagSelf => "<warn>",
                Quest.MustWaitUnpinned => $"<punish:{entity.PinRemainingTime.TotalSeconds:0}sec>",
                Quest.Ended => "<ok>",
                _ => throw new ArgumentOutOfRangeException()
            };

            return $"{graph} {warning} {entity.Name}";
        }

        void InspectEntity(long entityId, bool showOutlierTests)
        {
            if (!AutoModerator.TryGetTimeSeries(entityId, out var timeSeries))
            {
                Context.Respond("Time series not found", Color.Red);
                return;
            }

            if (!AutoModerator.TryGetEntity(entityId, out var entity))
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

            msgBuilder.AppendLine($"Lag (evaluated): {entity.LagNormal * 100:0}%");
            msgBuilder.AppendLine(entity.IsPinned ? $"Pinned for next {entity.PinRemainingTime.TotalSeconds:0} seconds" : "Not pinned");

            if (AutoModerator.TryFindQuestForEntity(entityId, out var w))
            {
                msgBuilder.AppendLine($"Warning: {w.Quest} (Normal: {w.QuestLagNormal * 100:0}%)");
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
            var size = Math.Min(maxWidth, (int)(graphNormal * maxWidth));
            var graph0 = Enumerable.Repeat('¦', size);
            var graph1 = Enumerable.Repeat('\'', maxWidth - size);
            var graph = new string(graph0.Concat(graph1).ToArray());
            var label = showLabel ? $" {normal * 100:000}%" : "";
            return $"{graph}{label}";
        }
    }
}