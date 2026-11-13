using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The scene's GPU buffers imported into this frame's render graph.
     * </summary>
     */
    public readonly struct SceneBuffers
    {
        #region Properties

        public BufferHandle Objects { get; }
        public BufferHandle Materials { get; }
        public BufferHandle Spectra { get; }
        public BufferHandle WavelengthCdf { get; }
        public int ObjectCount { get; }

        #endregion

        public SceneBuffers(BufferHandle objects, BufferHandle materials, BufferHandle spectra, BufferHandle wavelengthCdf, int objectCount)
        {
            Objects = objects;
            Materials = materials;
            Spectra = spectra;
            WavelengthCdf = wavelengthCdf;
            ObjectCount = objectCount;
        }

        /**
         * <summary>
         * Declares that a pass reads every scene table.
         * </summary>
         * <param name="builder">The pass's builder.</param>
         */
        public void Declare(IBaseRenderGraphBuilder builder)
        {
            builder.UseBuffer(Objects, AccessFlags.Read);
            builder.UseBuffer(Materials, AccessFlags.Read);
            builder.UseBuffer(Spectra, AccessFlags.Read);
            builder.UseBuffer(WavelengthCdf, AccessFlags.Read);
        }

        /**
         * <summary>
         * Binds every scene table declared in SceneData.hlsl to a kernel.
         * </summary>
         * <param name="cmd">The command buffer to record into.</param>
         * <param name="shader">The shader to bind them to.</param>
         * <param name="kernel">The kernel to bind them to.</param>
         */
        public void Bind(ComputeCommandBuffer cmd, ComputeShader shader, int kernel)
        {
            cmd.SetComputeBufferParam(shader, kernel, ShaderIDs.ObjectsID, Objects);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIDs.MaterialsID, Materials);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIDs.SpectraID, Spectra);
            cmd.SetComputeBufferParam(shader, kernel, ShaderIDs.WavelengthCdfID, WavelengthCdf);
            cmd.SetComputeIntParam(shader, ShaderIDs.ObjectCountID, ObjectCount);
        }
    }
}
