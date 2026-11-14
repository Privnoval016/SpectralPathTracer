using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The scene's wavelength distribution: the eye's weights mixed with the scene's light, uploaded as a CDF.
     * </summary>
     */
    public class WavelengthTable
    {
        #region Private Fields

        private readonly GrowableBuffer<float> _cdf = new GrowableBuffer<float>("PathTracerWavelengthCdf");
        private readonly float[] _visualWeights = WavelengthDistribution.VisualWeights(); // the eye's part never changes
        private float _lightShare = -1f; // nothing uploaded yet
        private readonly List<Vector4> _lines = new List<Vector4>(); // every line present in the scene integrated into the distribution

        #endregion

        /**
         * <summary>
         * Rebuilds and uploads the distribution if the scene's light or the share changed.
         * </summary>
         * <param name="lightChanged">True if the shapes, their materials or the sky changed.</param>
         * <param name="lightShare">How much of the distribution follows the scene's light, from 0 to 1.</param>
         * <param name="objects">The scene's object records, one per shape.</param>
         * <param name="materials">The material table the records point into.</param>
         * <param name="sky">The scene's sky.</param>
         * <returns>True if a new distribution was uploaded.</returns>
         */
        public bool Update(bool lightChanged, float lightShare, List<GpuObject> objects, MaterialTable materials, SceneSky sky)
        {
            if (!lightChanged && lightShare == _lightShare)
                return false;

            _lightShare = lightShare;
            Upload(SceneLight(objects, materials, sky, _lines));
            return true;
        }

        public BufferHandle Import(RenderGraph renderGraph)
        {
            return _cdf.Import(renderGraph);
        }

        public void Dispose()
        {
            _cdf.Dispose();
        }

        #region Helpers

        /**
         * <summary>
         * Sums every shape's emission and the sky at each nm, as authored, so brighter lights count for more.
         * </summary>
         * <param name="objects">The scene's object records.</param>
         * <param name="materials">The material table the records point into.</param>
         * <param name="sky">The scene's sky.</param>
         * <param name="lines">Filled with every line in the scene, which are too narrow to read at each nm.</param>
         * <returns>The smooth lights at each nm from 360 to 830.</returns>
         */
        private static float[] SceneLight(List<GpuObject> objects, MaterialTable materials, SceneSky sky, List<Vector4> lines)
        {
            float d65Scale = Cie.D65Scale();
            lines.Clear();

            // every shape's emission and the sky at each nm
            var light = new float[Cie.SampleCount];
            foreach (GpuObject gpuObject in objects)
            {
                if (materials.HasLines(gpuObject.MaterialIndex))
                {
                    lines.AddRange(materials.LinesOf(gpuObject.MaterialIndex));
                    continue;
                }

                for (int i = 0; i < light.Length; i++)
                    light[i] += materials.EmissionAt(gpuObject.MaterialIndex, Cie.FirstWavelength + i, d65Scale);
            }

            for (int i = 0; i < light.Length; i++)
                light[i] += sky.RadianceAt(Cie.FirstWavelength + i, d65Scale);


            return light;
        }

        private void Upload(float[] light)
        {
            float[] weights = WavelengthDistribution.Mix(_visualWeights, WavelengthDistribution.LightWeights(light, _lines), _lightShare);
            _cdf.Upload(new List<float>(WavelengthDistribution.Cdf(WavelengthDistribution.WithUniformFloor(weights))));
        }

        #endregion
    }
}
