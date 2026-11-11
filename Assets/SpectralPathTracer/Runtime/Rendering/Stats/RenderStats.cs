using System.Collections.Generic;
using Unity.Collections;

namespace SpectralPathTracer
{
    public static class RenderStats
    {
        private static readonly uint[] LivePathCounts = new uint[LivePathStats.MaxDepths];

        public static IReadOnlyList<uint> LivePaths => LivePathCounts; // this will be a few frames old

        internal static void ReportLivePaths(NativeArray<uint> counts)
        {
            NativeArray<uint>.Copy(counts, LivePathCounts, LivePathCounts.Length);
        }
    }
}
