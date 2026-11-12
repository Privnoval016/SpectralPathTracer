using System.Collections.Generic;
using UnityEngine;
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
        private readonly List<GpuMaterial> _materialData = new List<GpuMaterial>();

        /**
         * <summary>
         * Keyed by material asset, resolving to its index in the material table.
         * </summary>
         */
        private readonly Dictionary<SpectralMaterial, int> _materialIndices = new Dictionary<SpectralMaterial, int>();

        private readonly List<Matrix4x4> _cachedTransforms = new List<Matrix4x4>();

        private readonly GrowableBuffer<GpuObject> _objects = new GrowableBuffer<GpuObject>("PathTracerObjects");
        private readonly GrowableBuffer<GpuMaterial> _materials = new GrowableBuffer<GpuMaterial>("PathTracerMaterials");

        private readonly List<float> _spectrumData = new List<float>(); // measured spectra, 95 values each
        private readonly GrowableBuffer<float> _spectrumTables = new GrowableBuffer<float>("PathTracerSpectra");

        private int _builtVersion = -1;
        private int _defaultMaterialIndex = -1;

        private bool _reportedMultipleSkies;

        private readonly SigmoidTable _sigmoidTable; // null when no table is assigned, which leaves every spectrum black

        #endregion

        #region Properties

        public int ObjectCount { get; private set; }
        public SkyColors Sky { get; private set; }

        public Vector4 SkyZenithSpectrum { get; private set; }
        public Vector4 SkyHorizonSpectrum { get; private set; }
        public Vector4 SkyGroundSpectrum { get; private set; }

        #endregion

        /**
         * <summary>
         * Initializes a new instance of the <see cref="SceneCompiler"/> class.
         * </summary>
         * <param name="spectra">The table that turns RGB colors into spectra.</param>
         */
        public SceneCompiler(SigmoidTable spectra)
        {
            _sigmoidTable = spectra;
        }

        /**
         * <summary>
         * Picks the sky, then rebuilds and uploads the scene buffers if anything changed since the last build.
         * </summary>
         * <param name="changed">True if the sky or the scene changed this frame.</param>
         * <returns>False if more than one Sky is enabled.</returns>
         */
        public bool TryUpdate(out bool changed)
        {
            changed = false;
            if (!TryResolveSky(out SkyColors sky))
                return false;

            // sky colors change without a version bump, so compare them directly
            changed = sky.Zenith != Sky.Zenith || sky.Horizon != Sky.Horizon || sky.Ground != Sky.Ground;
            Sky = sky;

            // the sky is a light, so its colors become unbounded spectra once per change
            if (changed)
            {
                SkyZenithSpectrum = Unbounded(sky.Zenith.linear);
                SkyHorizonSpectrum = Unbounded(sky.Horizon.linear);
                SkyGroundSpectrum = Unbounded(sky.Ground.linear);
            }

            if (HasChanged())
            {
                Rebuild();
                Upload();
                CacheTransforms();
                _builtVersion = TracedRegistry.Version;
                changed = true;
            }

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
            return new SceneBuffers(_objects.Import(renderGraph), _materials.Import(renderGraph), _spectrumTables.Import(renderGraph), ObjectCount);
        }


        public void Dispose()
        {
            _objects.Dispose();
            _materials.Dispose();
            _spectrumTables.Dispose();
        }

        #region Helpers

        private Vector4 Unbounded(Color color)
        {
            return _sigmoidTable?.Unbounded(new Vector3(color.r, color.g, color.b)) ?? Vector4.zero;
        }

        private bool HasChanged()
        {
            if (!_objects.IsCreated || _builtVersion != TracedRegistry.Version)
                return true;

            // a moved object changes no version, so compare every transform against the last build
            IReadOnlyList<TracedShape> shapes = TracedRegistry.Shapes;
            if (shapes.Count != _cachedTransforms.Count)
                return true;

            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].transform.localToWorldMatrix != _cachedTransforms[i])
                    return true;
            }

            return false;
        }

        private void Rebuild()
        {
            _objectData.Clear();
            _materialData.Clear();
            _spectrumData.Clear();
            _materialIndices.Clear();
            _defaultMaterialIndex = -1;

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
                    MaterialIndex = MaterialIndex(shape.Material),
                    LightId = -1, // no light table yet
                    MediumIn = -1,
                    MediumOut = -1
                };
                _objectData.Add(gpuObject);
            }

            // the material buffer needs to hold at least one record
            if (_materialData.Count == 0)
                MaterialIndex(null);

            ObjectCount = _objectData.Count;
        }

        private int MaterialIndex(SpectralMaterial material)
        {
            if (material == null)
            {
                if (_defaultMaterialIndex < 0)
                    _defaultMaterialIndex = AddMaterial(SpectralMaterial.DefaultAlbedo, Color.black, 0f, null, null);

                return _defaultMaterialIndex;
            }

            if (!_materialIndices.TryGetValue(material, out int index))
            {
                index = AddMaterial(material.Albedo, material.EmissionColor, material.EmissionStrength, material.ReflectanceSpectrum, material.EmissionSpectrum);
                _materialIndices.Add(material, index);
            }

            return index;
        }

        private int AddMaterial(Color albedo, Color emissionColor, float emissionStrength, MeasuredSpectrum reflectance, MeasuredSpectrum emission)
        {
            Color linearAlbedo = albedo.linear;
            Color linearEmission = emissionColor.linear * emissionStrength;
            var albedoRgb = new Vector3(linearAlbedo.r, linearAlbedo.g, linearAlbedo.b);
            var emissionRgb = new Vector3(linearEmission.r, linearEmission.g, linearEmission.b);

            // a measured spectrum replaces the picker color in both modes, so RGB mode shows the same material
            if (reflectance != null)
                albedoRgb = reflectance.ReflectanceRgb();

            if (emission != null)
                emissionRgb = emission.EmissionRgb() * emissionStrength;


            _materialData.Add(new GpuMaterial
            {
                AlbedoSpectrum = _sigmoidTable?.Reflectance(albedoRgb) ?? Vector4.zero,
                EmissionSpectrum = _sigmoidTable?.Unbounded(emissionRgb) ?? Vector4.zero,
                Albedo = albedoRgb,
                Emission = emissionRgb,
                Type = (int)MaterialType.Lambert,

                ReflectanceTable = reflectance != null ? AddSpectrumTable(reflectance.Resample()) : -1,
                EmissionTable = emission != null ? AddSpectrumTable(emission.Resample(emission.EmissionNormalization() * emissionStrength)) : -1
            });
            return _materialData.Count - 1;
        }

        /**
         * <summary>
         * Appends one resampled spectrum to the spectrum buffer.
         * </summary>
         * <param name="values">The spectrum on the fixed 5 nm grid.</param>
         * <returns>The table's index, counted in whole spectra.</returns>
         */
        private int AddSpectrumTable(float[] values)
        {
            int index = _spectrumData.Count / (int)SpectrumTables.Count;
            _spectrumData.AddRange(values);


            return index;
        }

        private void Upload()
        {
            _objects.Upload(_objectData);
            _materials.Upload(_materialData);

            // the spectrum buffer needs at least one value even when nothing is measured
            if (_spectrumData.Count == 0)
                _spectrumData.Add(0f);

            _spectrumTables.Upload(_spectrumData);
        }

        /**
        * <summary>
        * Picks this frame's sky.
        * </summary>
        * <param name="sky">The sky to render with.</param>
        * <returns>False if more than one Sky is enabled.</returns>
        */
        private bool TryResolveSky(out SkyColors sky)
        {
            IReadOnlyList<Sky> skies = TracedRegistry.Skies;

            if (skies.Count > 1)
            {
                if (!_reportedMultipleSkies)
                    Debug.LogError($"Path tracer: {skies.Count} Sky components are enabled; disable all but one.");

                _reportedMultipleSkies = true;
                sky = default;
                return false;
            }

            _reportedMultipleSkies = false;
            sky = skies.Count == 1 ? skies[0].Colors : SkyColors.Default;
            return true;
        }

        private void CacheTransforms()
        {
            _cachedTransforms.Clear();

            // indexing a read-only list skips the boxed enumerator foreach would allocate
            IReadOnlyList<TracedShape> shapes = TracedRegistry.Shapes;
            for (int i = 0; i < shapes.Count; i++)
                _cachedTransforms.Add(shapes[i].transform.localToWorldMatrix);
        }

        #endregion
    }
}