using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * A path tracer stage that runs one compute kernel. Holds what every such stage needs: the shader, its
     * kernel index and its thread group size.
     * </summary>
     */
    public abstract class ComputeStage
    {
        #region Properties

        protected ComputeShader Shader { get; }
        protected int Kernel { get; }
        protected int GroupSizeX { get; }
        protected int GroupSizeY { get; }
        protected LocalKeyword SpectralKeyword { get; } // invalid for kernels with no spectral variant

        #endregion

        protected ComputeStage(ComputeShader shader, string kernelName)
        {
            Shader = shader;
            Kernel = shader.FindKernel(kernelName);

            shader.GetKernelThreadGroupSizes(Kernel, out uint groupSizeX, out uint groupSizeY, out _);
            GroupSizeX = (int)groupSizeX;
            GroupSizeY = (int)groupSizeY;

            SpectralKeyword = shader.keywordSpace.FindKeyword("SPECTRAL");
        }

        #region Helpers

        /**
         * <summary>
         * Logs an error when the kernel's group size differs from the one its dispatch assumes.
         * </summary>
         * <param name="expected">The group size the dispatch assumes.</param>
         */
        protected void CheckGroupSize(int expected)
        {
            if (GroupSizeX != expected)
                Debug.LogError($"Path tracer: {Shader.name} runs {GroupSizeX} threads per group but its dispatch assumes {expected}.");
        }

        /**
         * <summary>
         * Number of thread groups needed to cover <paramref name="size"/> threads, rounded up.
         * </summary>
         * <param name="size">The number of threads.</param>
         * <param name="groupSize">The thread group size.</param>
         * <returns>The number of thread groups.</returns>
         */
        protected static int GroupCount(int size, int groupSize) => (size + groupSize - 1) / groupSize;

        /**
         * <summary>
         * Binds this frame's constant buffer to a shader.
         * </summary>
         * <param name="cmd">The command buffer to record into.</param>
         * <param name="shader">The shader to bind it to.</param>
         * <param name="constants">This frame's constant buffer.</param>
         */
        protected static void SetFrameConstants(ComputeCommandBuffer cmd, ComputeShader shader, BufferHandle constants)
        {
            cmd.SetComputeConstantBufferParam(shader, ShaderIDs.FrameConstantsID, constants, 0, FrameConstantsBuffer.Size);
        }

        /**
         * <summary>
         * Picks the spectral or RGB variant for this pass's dispatches.
         * </summary>
         * <param name="cmd">The pass's command buffer.</param>
         * <param name="shader">The stage's shader.</param>
         * <param name="keyword">The stage's SPECTRAL keyword.</param>
         * <param name="spectral">Whether to run the spectral variant.</param>
         */
        protected static void SetSpectral(ComputeCommandBuffer cmd, ComputeShader shader, in LocalKeyword keyword, bool spectral)
        {
            // set both ways, since the state stays on the shader after the pass
            cmd.SetKeyword(shader, keyword, spectral);
        }

        #endregion
    }
}