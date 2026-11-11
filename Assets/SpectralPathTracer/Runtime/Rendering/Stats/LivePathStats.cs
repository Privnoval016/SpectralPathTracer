using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class LivePathStats
    {
        public const int MaxDepths = 32; // room for 16 + 1 depths

        private readonly uint[] _zeroes = new uint[MaxDepths];
        private readonly Action<AsyncGPUReadbackRequest> _onReadback;

        private GraphicsBuffer _counts;

        private class ClearPassData
        {
            public BufferHandle Counts;
            public uint[] Zeroes;
        }

        private class ReadbackPassData
        {
            public BufferHandle Counts;
            public Action<AsyncGPUReadbackRequest> OnReadback;
        }

        public LivePathStats()
        {
            _onReadback = OnReadback; // cached so readbacks dont allocate a new delegate every frame
        }

        /**
         * <summary>
         * Records a clear pass that resets the live path counts to zero.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <returns>The buffer handle for the live path counts.</returns>
         */
        public BufferHandle RecordClear(RenderGraph renderGraph)
        {
            _counts ??= new GraphicsBuffer(GraphicsBuffer.Target.Raw, MaxDepths, sizeof(uint))
            {
                name = "PathTracerLivePaths"
            };

            BufferHandle counts = renderGraph.ImportBuffer(_counts);

            using var builder = renderGraph.AddComputePass("Path Tracer: Clear Stats", out ClearPassData passData);
            passData.Counts = counts;
            passData.Zeroes = _zeroes;
            builder.UseBuffer(counts, AccessFlags.Write);
            builder.SetRenderFunc(static (ClearPassData data, ComputeGraphContext context) =>
            {
                context.cmd.SetBufferData(data.Counts, data.Zeroes);
            });

            return counts;
        }

        /**
         * <summary>
         * Records a readback pass that reads the live path counts back to the CPU.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="counts">The buffer handle for the live path counts.</param>
         */
        public void RecordReadback(RenderGraph renderGraph, BufferHandle counts)
        {
            using var builder = renderGraph.AddUnsafePass("Path Tracer: Readback Stats", out ReadbackPassData passData);
            passData.Counts = counts;
            passData.OnReadback = _onReadback;
            builder.UseBuffer(counts, AccessFlags.Read);
            builder.AllowPassCulling(false); // this might get culled since it doesnt output anything
            builder.SetRenderFunc(static (ReadbackPassData data, UnsafeGraphContext context) =>
            {
                context.cmd.RequestAsyncReadback(data.Counts, data.OnReadback);
            });
        }

        public void Dispose()
        {
            _counts?.Release();
            _counts = null;
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
                return;

            RenderStats.ReportLivePaths(request.GetData<uint>());   
        }
    }
}
