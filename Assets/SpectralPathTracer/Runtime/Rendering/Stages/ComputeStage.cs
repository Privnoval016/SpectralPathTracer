using UnityEngine;

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

        #endregion

        protected ComputeStage(ComputeShader shader, string kernelName)
        {
            Shader = shader;
            Kernel = shader.FindKernel(kernelName);

            shader.GetKernelThreadGroupSizes(Kernel, out uint groupSizeX, out uint groupSizeY, out _);
            GroupSizeX = (int)groupSizeX;
            GroupSizeY = (int)groupSizeY;
        }

        #region Helpers

        /**
         * <summary>
         * Number of thread groups needed to cover <paramref name="size"/> threads, rounded up.
         * </summary>
         */
        protected static int GroupCount(int size, int groupSize) => (size + groupSize - 1) / groupSize;

        #endregion
    }
}