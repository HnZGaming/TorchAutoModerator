using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoModerator.Core;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Quests
{
    public sealed class LaggyPlayerSnapshotCreator
    {
        public async Task<IEnumerable<LaggyPlayerSnapshot>> CreateSnapshots(
            IEnumerable<TrackedEntitySnapshot> playerLagSnapshots,
            IEnumerable<TrackedEntitySnapshot> gridLagSnapshots,
            CancellationToken canceller)
        {
            var laggyPlayerSnapshots = new List<LaggyPlayerSnapshot>();

            await GameLoopObserver.MoveToGameLoop(canceller);

            var laggiestGridSnapshots = gridLagSnapshots
                .OrderByDescending(s => s.LongLagNormal)
                .ToDictionaryDescending(s => VRageUtils.GetOwnerPlayerId(s.EntityId), s => s);

            await TaskUtils.MoveToThreadPool(canceller);

            var laggiestPlayerSnapshots = playerLagSnapshots
                .ToDictionary(p => p.EntityId);

            var zip = laggiestGridSnapshots.Zip(laggiestPlayerSnapshots, default, default);
            foreach (var (playerId, (gridSnapshot, playerSnapshot)) in zip)
            {
                var signatureSnapshot = gridSnapshot.LongLagNormal > playerSnapshot.LongLagNormal ? gridSnapshot : playerSnapshot;
                var laggyPlayerSnapshot = new LaggyPlayerSnapshot(
                    playerId,
                    signatureSnapshot.LongLagNormal,
                    signatureSnapshot.RemainingTime > TimeSpan.Zero);

                laggyPlayerSnapshots.Add(laggyPlayerSnapshot);
            }

            return laggyPlayerSnapshots;
        }
    }
}