using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoModerator.Punishes;
using AutoModerator.Punishes.Broadcasts;
using AutoModerator.Quests;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Helpers;
using Torch.API.Managers;
using Utils.General;
using Utils.TimeSerieses;
using Utils.Torch;

namespace AutoModerator.Core
{
    public sealed class AutoModerator
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public interface IConfig :
            GridTracker.IConfig,
            PlayerTracker.IConfig,
            BroadcastListenerCollection.IConfig,
            EntityGpsBroadcaster.IConfig,
            QuestEntity.IConfig,
            PunishExecutor.IConfig,
            PunishChatFeed.IConfig
        {
            bool IsEnabled { get; }
            double FirstIdleTime { get; }
            double IntervalFrequency { get; }
            bool EnablePunishChatFeed { get; }
            IEnumerable<string> ExemptBlockTypePairs { get; }
            void RemovePunishExemptBlockType(string input);
        }

        readonly IConfig _config;
        readonly GridTracker _grids;
        readonly PlayerTracker _players;
        readonly EntityGpsBroadcaster _entityGpsBroadcaster;
        readonly BroadcastListenerCollection _gpsReceivers;
        readonly QuestTracker _questTracker;
        readonly PunishExecutor _punishExecutor;
        readonly PunishChatFeed _punishChatFeed;
        readonly IChatManagerServer _chatManager;
        readonly BlockTypePairCollection _exemptBlockTypePairs;

        public AutoModerator(IConfig config, IChatManagerServer chatManager)
        {
            _config = config;
            _chatManager = chatManager;
            _exemptBlockTypePairs = new BlockTypePairCollection();
            _grids = new GridTracker(config);
            _players = new PlayerTracker(config);
            _gpsReceivers = new BroadcastListenerCollection(config);
            _entityGpsBroadcaster = new EntityGpsBroadcaster(config);
            _questTracker = new QuestTracker(config, chatManager);
            _punishExecutor = new PunishExecutor(config, _exemptBlockTypePairs);
            _punishChatFeed = new PunishChatFeed(config, _chatManager);
        }

        public bool IsIdle { get; private set; } = true;
        public IReadOnlyDictionary<long, TrackedEntity> Players => _players.Entities;
        public IReadOnlyDictionary<long, TrackedEntity> Grids => _grids.Entities;

        public void Close()
        {
            _entityGpsBroadcaster.ClearGpss();
            _questTracker.Clear();
        }

        public async Task Main(CancellationToken canceller)
        {
            Log.Info("started main");

            _entityGpsBroadcaster.ClearGpss();
            _questTracker.Clear();

            // Wait for some time during the session startup
            await TaskUtils.Delay(() => _config.FirstIdleTime.Seconds(), 1.Seconds(), canceller);

            IsIdle = false;

            Log.Info("started collector loop");

            // MAIN LOOP
            while (!canceller.IsCancellationRequested)
            {
                if (!_config.IsEnabled)
                {
                    _grids.Clear();
                    _players.Clear();
                    _entityGpsBroadcaster.ClearGpss();
                    _questTracker.Clear();
                    _punishExecutor.Clear();
                    _punishChatFeed.Clear();

                    await Task.Delay(1.Seconds(), canceller);
                    return;
                }

                using var _ = CustomProfiling.Profile("AutoModerator.Main");

                await Profile(canceller);
                FixExemptBlockTypeCollection();
                Warn();
                AnnouncePunishments();
                await Punish(canceller);
                await AnnounceDeletedGrids(canceller);

                Log.Debug("interval done");
            }
        }

        async Task Profile(CancellationToken canceller)
        {
            // auto profile
            var mask = new GameEntityMask(null, null, null);
            using (var gridProfiler = new GridProfiler(mask))
            using (var playerProfiler = new PlayerProfiler(mask))
            using (ProfilerResultQueue.Profile(gridProfiler))
            using (ProfilerResultQueue.Profile(playerProfiler))
            {
                Log.Trace("auto-profile started");
                gridProfiler.MarkStart();
                playerProfiler.MarkStart();
                await Task.Delay(_config.IntervalFrequency.Seconds(), canceller);
                Log.Trace("auto-profile done");

                _grids.Update(gridProfiler.GetResult());
                _players.Update(playerProfiler.GetResult());
            }

            Log.Trace("profile done");
        }

        void Warn()
        {
            var doesPunish = _config.PunishType != PunishType.None;
            Log.Debug($"punishment type: {_config.PunishType}, warning for punishment: {doesPunish}");

            var sources = new List<QuestSource>();
            var players = _players.Entities;
            var grids = _grids.GetLaggiestGridsByOwners();
            foreach (var (playerId, (player, grid)) in players.Zip(grids))
            {
                if (playerId == 0) continue; // grid not owned

                var src = CreateWarningSource(player, grid, playerId, doesPunish);
                if (src.LagNormal >= _config.QuestLagNormal || src.Pin > TimeSpan.Zero)
                {
                    sources.Add(src);
                }
            }

            _questTracker.Update(sources);

            Log.Trace("warnings done");
        }

        static QuestSource CreateWarningSource(TrackedEntity player, TrackedEntity grid, long playerId, bool doesPunish)
        {
            // note: there's a chance that `player` or `grid` is null
            var playerName = player?.Name ?? grid?.OwnerName;
            var playerLag = player?.LagNormal ?? 0;
            var playerPin = doesPunish ? player?.PinRemainingTime ?? TimeSpan.Zero : TimeSpan.Zero;
            var gridLag = grid?.LagNormal ?? 0;
            var gridPin = doesPunish ? grid?.PinRemainingTime ?? TimeSpan.Zero : TimeSpan.Zero;
            var src = new QuestSource(playerId, playerName, playerLag, playerPin, grid?.Id ?? 0, gridLag, gridPin);
            return src;
        }

        void AnnouncePunishments()
        {
            if (!_config.EnablePunishChatFeed)
            {
                _punishChatFeed.Clear();
                return;
            }

            var sources = new List<PunishSource>();
            var pinnedGrids = _grids.GetPinnedGridsByOwners();
            var pinnedPlayers = _players.Entities.GetLaggiestEntities(true).ToDictionary(p => p.Id);
            foreach (var (_, (grid, player)) in pinnedGrids.Zip(pinnedPlayers))
            {
                var source = CreatePunishmentSource(grid, player);
                sources.Add(source);
            }

            _punishChatFeed.Update(sources);
        }

        static PunishSource CreatePunishmentSource(TrackedEntity grid, TrackedEntity player)
        {
            // note: there's a chance that `player` or `grid` is null
            var playerId = player?.Id ?? grid?.OwnerId ?? 0; // shouldn't be 0 tho
            var lagNormal = Math.Max(grid?.LagNormal ?? 0, player?.LagNormal ?? 0);
            var playerName = player?.Name ?? grid?.OwnerName;
            var factionTag = player?.FactionTag ?? grid?.FactionTag;
            var gridId = grid?.Id ?? 0;
            var gridName = grid?.Name ?? "<none>";
            var isPinned = (grid?.IsPinned ?? false) || (player?.IsPinned ?? false);
            var source = new PunishSource(playerId, playerName, factionTag, gridId, gridName, lagNormal, isPinned);
            return source;
        }

        void FixExemptBlockTypeCollection()
        {
            var invalidInputs = new List<string>();

            _exemptBlockTypePairs.Clear();
            foreach (var rawInput in _config.ExemptBlockTypePairs)
            {
                if (!_exemptBlockTypePairs.TryAdd(rawInput))
                {
                    invalidInputs.Add(rawInput);
                    Log.Warn($"Removed invalid block type pair: {rawInput}");
                }
            }

            // remove invalid items from the config
            foreach (var invalidInput in invalidInputs)
            {
                _config.RemovePunishExemptBlockType(invalidInput);
            }
        }

        async Task Punish(CancellationToken canceller)
        {
            await PunishBlocks();
            await BroadcastLaggyGrids(canceller);
        }

        async Task PunishBlocks()
        {
            if (_config.PunishType != PunishType.Damage &&
                _config.PunishType != PunishType.Shutdown)
            {
                _punishExecutor.Clear();
                return;
            }

            var punishSources = new Dictionary<long, PunishSource>();
            foreach (var player in _players.Entities.GetLaggiestEntities(true))
            {
                var playerId = player.Id;
                if (!_grids.TryGetLaggiestGridOwnedBy(playerId, out var laggiestGrid)) continue;

                var src = CreatePunishmentSource(laggiestGrid, player);
                punishSources[src.GridId] = src;
            }

            foreach (var grid in _grids.Entities.GetLaggiestEntities(true))
            {
                var src = CreatePunishmentSource(grid, null);
                punishSources[src.GridId] = src;
            }

            await _punishExecutor.Update(punishSources);

            Log.Trace("punishment done");
        }

        async Task BroadcastLaggyGrids(CancellationToken canceller)
        {
            if (_config.PunishType != PunishType.Broadcast)
            {
                _entityGpsBroadcaster.ClearGpss();
                return;
            }

            var allGpsSources = new Dictionary<long, GridGpsSource>();

            foreach (var (player, rank) in _players.Entities.GetLaggiestEntities(true).Indexed())
            {
                var playerId = player.Id;
                if (!_grids.TryGetLaggiestGridOwnedBy(playerId, out var laggiestGrid)) continue;

                var gpsSource = new GridGpsSource(laggiestGrid.Id, player.LagNormal, player.PinRemainingTime, rank);
                allGpsSources[gpsSource.GridId] = gpsSource;
            }

            foreach (var (grid, rank) in _grids.Entities.GetLaggiestEntities(true).Indexed())
            {
                var gpsSource = new GridGpsSource(grid.Id, grid.LagNormal, grid.PinRemainingTime, rank);
                allGpsSources[gpsSource.GridId] = gpsSource;
            }

            var targetIdentityIds = _gpsReceivers.GetReceiverIdentityIds();
            await _entityGpsBroadcaster.ReplaceGpss(allGpsSources.Values, targetIdentityIds, canceller);

            Log.Trace("broadcast done");
        }

        async Task AnnounceDeletedGrids(CancellationToken canceller)
        {
            // stop tracking deleted grids & report cheating
            // we're doing this right here to get the max chance of grabbing the owner name
            var lostGrids = new List<TrackedEntity>();

            await GameLoopObserver.MoveToGameLoop(canceller);

            foreach (var (_, trackedGrid) in _grids.Entities)
            {
                if (!MyEntities.TryGetEntityById(trackedGrid.Id, out _))
                {
                    lostGrids.Add(trackedGrid);
                }
            }

            await TaskUtils.MoveToThreadPool(canceller);

            foreach (var lostGrid in lostGrids)
            {
                _grids.StopTracking(lostGrid.Id);

                // high-profile grid deleted
                if (lostGrid.LagNormal >= _config.QuestLagNormal)
                {
                    var gridName = lostGrid.Name;
                    var ownerName = lostGrid.OwnerName;
                    Log.Warn($"Laggy grid deleted by player: {gridName}: {ownerName}");

                    if (_config.EnablePunishChatFeed)
                    {
                        _chatManager.SendMessage(_config.PunishReportChatName, 0, $"Laggy grid deleted by player: {gridName}: {ownerName}");
                    }
                }
            }

            Log.Trace("announcing deleted entities done");
        }

        public IEnumerable<MyGps> GetAllGpss()
        {
            return _entityGpsBroadcaster.GetGpss();
        }

        public void OnSelfProfiled(long playerId)
        {
            _questTracker.OnSelfProfiled(playerId);
        }

        public void ClearCache()
        {
            _grids.Clear();
            _players.Clear();
        }

        public void ClearQuestForUser(long playerId)
        {
            _questTracker.Remove(playerId);
        }

        public bool TryGetTimeSeries(long entityId, out ITimeSeries<double> timeSeries)
        {
            return _grids.Entities.TryGetTimeSeries(entityId, out timeSeries) ||
                   _players.Entities.TryGetTimeSeries(entityId, out timeSeries);
        }

        public bool TryGetEntity(long entityId, out TrackedEntity entity)
        {
            return _grids.Entities.TryGetValue(entityId, out entity) ||
                   _players.Entities.TryGetValue(entityId, out entity);
        }

        public bool TryFindEntityByName(string name, out TrackedEntity entity)
        {
            return _grids.Entities.TryFindEntityByName(name, out entity) ||
                   _players.Entities.TryFindEntityByName(name, out entity);
        }

        public bool TryFindPlayerNameById(long playerId, out string name)
        {
            if (_grids.TryGetLaggiestGridOwnedBy(playerId, out var grid))
            {
                name = grid.OwnerName;
                return true;
            }

            if (_players.Entities.TryGetValue(playerId, out var p))
            {
                name = p.Name;
                return true;
            }

            name = default;
            return false;
        }

        public bool TryFindPlayerByName(string playerName, out long playerId)
        {
            if (_grids.TryFindGridOwnerByName(playerName, out var p))
            {
                playerId = p.PlayerId;
                return true;
            }

            if (_players.Entities.TryFindEntityByName(playerName, out var pp))
            {
                playerId = pp.Id;
                return true;
            }

            playerId = default;
            return false;
        }

        public bool TryGetLaggiestGridOwnedBy(long ownerId, out TrackedEntity grid)
        {
            return _grids.TryGetLaggiestGridOwnedBy(ownerId, out grid);
        }

        public bool TryFindQuestForEntity(long entityId, out QuestEntity questEntity)
        {
            return _questTracker.TryFindQuestForEntity(entityId, out questEntity);
        }
    }
}