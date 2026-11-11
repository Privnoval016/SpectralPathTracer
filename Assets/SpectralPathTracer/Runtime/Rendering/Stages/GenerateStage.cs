using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    public class GenerateStage : ComputeStage
    {
        #region Private Fields

        private const string KernelName = "Generate";

        #endregion

        private class PassData
        {
            public ComputeShader Shader;
            public int Kernel;
            public BufferHandle Rays;
            public BufferHandle Paths;
            public BufferHandle QueueA;
            public BufferHandle Counters;
            public BufferHandle Args;
            public readonly int[] Resolution = new int[2];
            public int WaveStart;
            public int RayCount;
            public uint SampleIndex;
            public int Groups;
            public BufferHandle Constants;
            public LocalKeyword SpectralKeyword;
            public bool Spectral;
        }

        public GenerateStage(ComputeShader shader) : base(shader, KernelName)
        {
        }

        /**
         * <summary>
         * Records the pass that writes one camera ray per path slot in a wave and starts its path.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <param name="frame">This frame's context.</param>
         * <param name="waveStart">The first pixel the wave covers.</param>
         * <param name="count">How many pixels, and so path slots, the wave covers.</param>
         * <param name="sampleIndex">The sample the wave's paths belong to.</param>
         * <returns>The wave's buffers, valid for this frame's render graph.</returns>
         */
        public PathBuffers Record(RenderGraph renderGraph, in FrameContext frame, int waveStart, int count, uint sampleIndex)
        {
            var desc = new BufferDesc(count, UnsafeUtility.SizeOf<GpuRay>(), GraphicsBuffer.Target.Structured) { name = "PathTracerRays" };
            BufferHandle rays = renderGraph.CreateBuffer(desc);

            var hitDesc = new BufferDesc(count, UnsafeUtility.SizeOf<GpuHit>(), GraphicsBuffer.Target.Structured) { name = "PathTracerHits" };
            BufferHandle hits = renderGraph.CreateBuffer(hitDesc);

            var pathDesc = new BufferDesc(count, UnsafeUtility.SizeOf<GpuPathState>(), GraphicsBuffer.Target.Structured) { name = "PathTracerPaths" };
            BufferHandle paths = renderGraph.CreateBuffer(pathDesc);

            var queueDesc = new BufferDesc(count, sizeof(uint), GraphicsBuffer.Target.Structured) { name = "PathTracerQueueA" };
            BufferHandle queueA = renderGraph.CreateBuffer(queueDesc);
            queueDesc.name = "PathTracerQueueB";
            BufferHandle queueB = renderGraph.CreateBuffer(queueDesc);

            // raw buffers, since DX12 won't take indirect args or atomics from structured ones
            var counterDesc = new BufferDesc(2, sizeof(uint), GraphicsBuffer.Target.Raw) { name = "PathTracerQueueCounters" };
            BufferHandle counters = renderGraph.CreateBuffer(counterDesc);
            var argsDesc = new BufferDesc(3, sizeof(uint), GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw) { name = "PathTracerDispatchArgs" };
            BufferHandle args = renderGraph.CreateBuffer(argsDesc);

            using var builder = renderGraph.AddComputePass("Path Tracer: Generate", out PassData passData);

            passData.Shader = Shader;
            passData.Kernel = Kernel;
            passData.Rays = rays;
            passData.Paths = paths;
            passData.QueueA = queueA;
            passData.Counters = counters;
            passData.Args = args;
            passData.Resolution[0] = frame.RenderSize.x;
            passData.Resolution[1] = frame.RenderSize.y;
            passData.WaveStart = waveStart;
            passData.RayCount = count;
            passData.SampleIndex = sampleIndex;
            passData.Groups = GroupCount(count, GroupSizeX);
            passData.Constants = frame.Constants;
            passData.SpectralKeyword = SpectralKeyword;
            passData.Spectral = frame.Params.Spectral;

            builder.UseBuffer(rays, AccessFlags.Write);
            builder.UseBuffer(paths, AccessFlags.Write);
            builder.UseBuffer(queueA, AccessFlags.Write);
            builder.UseBuffer(counters, AccessFlags.Write);
            builder.UseBuffer(args, AccessFlags.Write);
            builder.UseBuffer(frame.Constants, AccessFlags.Read);
            builder.AllowGlobalStateModification(true); // the keyword is state on the shader, outside the graph's resources

            // static to reject captured variables from the outer scope
            builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
            {
                var cmd = context.cmd;
                SetSpectral(cmd, data.Shader, data.SpectralKeyword, data.Spectral);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.RaysID, data.Rays);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.PathsID, data.Paths);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.QueueOutID, data.QueueA);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.CountersID, data.Counters);
                cmd.SetComputeBufferParam(data.Shader, data.Kernel, ShaderIDs.ArgsID, data.Args);
                cmd.SetComputeIntParams(data.Shader, ShaderIDs.ResolutionID, data.Resolution);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.WaveStartID, data.WaveStart);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.RayCountID, data.RayCount);
                cmd.SetComputeIntParam(data.Shader, ShaderIDs.SampleIndexID, (int)data.SampleIndex); // the bits arrive unchanged in the uint uniform
                SetFrameConstants(cmd, data.Shader, data.Constants);
                cmd.DispatchCompute(data.Shader, data.Kernel, data.Groups, 1, 1);
            });

            return new PathBuffers(rays, hits, paths, waveStart, count, queueA, queueB, counters, args);
        }
    }
}