using UnityEngine;
using UnityEngine.Rendering;

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
         * <param name="size">The number of threads.</param>
         * <param name="groupSize">The thread group size.</param>
         * <returns>The number of thread groups.</returns>
         */
        protected static int GroupCount(int size, int groupSize) => (size + groupSize - 1) / groupSize;

        /**
         * <summary>
         * Sets the camera uniforms from Camera.hlsl on a shader.
         * </summary>
         */
        protected static void SetCamera(ComputeCommandBuffer cmd, ComputeShader shader, PinholeCamera camera)
        {
            cmd.SetComputeMatrixParam(shader, ShaderIDs.CameraToWorldID, camera.CameraToWorld);
            cmd.SetComputeFloatParam(shader, ShaderIDs.TanHalfFovYID, camera.TanHalfFov);
            cmd.SetComputeFloatParam(shader, ShaderIDs.AspectRatioID, camera.AspectRatio);
        }

        /**
         * <summary>
         * Sets the sampler uniforms from Sampler.hlsl on a shader.
         * </summary>
         * <param name="cmd">The command buffer to record into.</param>
         * <param name="shader">The shader to set them on.</param>
         * <param name="sampleIndex">This frame's sample index.</param>
         * <param name="outputScale">The output resolution divided by the render resolution.</param>
         */
        protected static void SetSampler(ComputeCommandBuffer cmd, ComputeShader shader, uint sampleIndex, Vector2 outputScale)
        {
            cmd.SetComputeIntParam(shader, ShaderIDs.SampleIndexID, (int)sampleIndex); // the bits arrive unchanged in the uint uniform
            cmd.SetComputeVectorParam(shader, ShaderIDs.OutputScaleID, outputScale);
        }

        #endregion
    }
}