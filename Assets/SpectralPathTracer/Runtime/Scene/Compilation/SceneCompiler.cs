using System.Collections.Generic;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Turns the registered scene objects into GPU buffers, rebuilding on scene changes, and picks the sky.
     * </summary>
     */
    public class SceneCompiler
    {
        #region Private Fields

        private readonly List<GpuObject> _objectData = new List<GpuObject>();
        private readonly GrowableBuffer<GpuObject> _objects = new GrowableBuffer<GpuObject>("PathTracerObjects");

        private readonly SceneChanges _changes = new SceneChanges();
        private readonly MaterialTable _materials;
        private readonly WavelengthTable _wavelengths = new WavelengthTable();

        #endregion

        #region Properties

        public int ObjectCount { get; private set; }
        public SceneSky Sky { get; }

        #endregion

        /**
         * <summary>
         * Initializes a new instance of the <see cref="SceneCompiler"/> class.
         * </summary>
         * <param name="spectra">The table that turns RGB colors into spectra.</param>
         */
        public SceneCompiler(SigmoidTable spectra)
        {
            _materials = new MaterialTable(spectra);
            Sky = new SceneSky(spectra);
        }

        /**
         * <summary>
         * Picks the sky, then rebuilds and uploads the scene buffers if anything changed since the last build.
         * </summary>
         * <param name="lightShare">How much of the wavelength distribution follows the scene's lights.</param>
         * <param name="changed">True if the sky, the scene or the light share changed this frame.</param>
         * <returns>False if more than one Sky is enabled.</returns>
         */
        public bool TryUpdate(float lightShare, out bool changed)
        {
            changed = false;
            if (!Sky.TryUpdate(out bool skyChanged))
                return false;

            bool sceneChanged = !_objects.IsCreated || _changes.HasChanged();
            if (sceneChanged)
            {
                Rebuild();
                _objects.Upload(_objectData);
                _materials.Upload();
                _changes.Remember();
            }

            // the distribution follows the lights and the sky, so it's rebuilt when either changes, or the share does
            bool distributionChanged = _wavelengths.Update(sceneChanged || skyChanged, lightShare, _objectData, _materials, Sky);

            changed = skyChanged || sceneChanged || distributionChanged;
            return true;
        }

        /**
         * <summary>
         * Imports the scene buffers into this frame's render graph.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <returns>The scene buffers for this frame.</returns>
         */
        public SceneBuffers Import(RenderGraph renderGraph)
        {
            return new SceneBuffers(_objects.Import(renderGraph), _materials.ImportMaterials(renderGraph), _materials.ImportSpectra(renderGraph), _materials.ImportLines(renderGraph), _wavelengths.Import(renderGraph), ObjectCount);
        }


        public void Dispose()
        {
            _objects.Dispose();
            _materials.Dispose();
            _wavelengths.Dispose();
        }

        #region Helpers

        private void Rebuild()
        {
            _objectData.Clear();
            _materials.Clear();

            IReadOnlyList<TracedShape> shapes = TracedRegistry.Shapes;
            for (int i = 0; i < shapes.Count; i++)
            {
                TracedShape shape = shapes[i];

                // each shape supplies its own matrices, so every record is filled the same way
                var gpuObject = new GpuObject
                {
                    Quadric = shape.WorldQuadric,
                    WorldToLocal = shape.WorldToShape,
                    Type = (int)shape.Type,
                    MaterialIndex = _materials.IndexFor(shape),
                    LightId = -1, // no light table yet
                    MediumIn = -1,
                    MediumOut = -1
                };
                _objectData.Add(gpuObject);
            }

            ObjectCount = _objectData.Count;
        }

        #endregion
    }
}
