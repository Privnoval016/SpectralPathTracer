using UnityEngine;

namespace SpectralPathTracer
{
    public static class TestFrameConstants
    {
        /**
         * <summary>
         * Uploads frame constants and binds them to a shader for direct dispatches.
         * </summary>
         * <param name="shader">The shader to bind them to.</param>
         * <param name="constants">The constants to upload.</param>
         * <returns>The constant buffer, which the caller releases.</returns>
         */
        public static GraphicsBuffer Bind(ComputeShader shader, FrameConstants constants)
        {
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, FrameConstantsBuffer.Size);
            buffer.SetData(new[] { constants });
            shader.SetConstantBuffer(ShaderIDs.FrameConstantsID, buffer, 0, FrameConstantsBuffer.Size);
            return buffer;
        }
    }
}
