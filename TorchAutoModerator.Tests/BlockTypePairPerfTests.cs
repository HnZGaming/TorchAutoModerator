using System;
using NBench;

namespace TorchAutoModerator.Tests
{
    public sealed class BlockTypePairPerfTests : PerformanceTestSuite<BlockTypePairPerfTests>
    {
        readonly Type _type = typeof(BlockTypePairPerfTests);

        [PerfBenchmark(RunMode = RunMode.Iterations, TestMode = TestMode.Test)]
        [MemoryAssertion(MemoryMetric.TotalBytesAllocated, MustBe.LessThan, 1)]
        public void TestTypeNameMemoryAllocation()
        {
            _ = _type.Name;
        }
    }
}