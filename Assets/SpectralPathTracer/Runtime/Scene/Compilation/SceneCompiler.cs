using System;
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
        private readonly List<EmitterState?> _cachedEmitters = new List<EmitterState?>();

        private readonly GrowableBuffer<GpuObject> _objects = new GrowableBuffer<GpuObject>("PathTracerObjects");
        private readonly GrowableBuffer<GpuMaterial> _materials = new GrowableBuffer<GpuMaterial>("PathTracerMaterials");

        private readonly List<float> _spectrumData = new List<float>(); // measured spectra, 95 values each
        private readonly GrowableBuffer<float> _spectrumTables = new GrowableBuffer<float>("PathTracerSpectra");

        private int _builtVersion = -1;
        private int _defaultMaterialIndex = -1;

        private bool _reportedMultipleSkies;

        private readonly SigmoidTable _sigmoidTable; // null when no table is assigned which leaves every spectrum black

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

                // Timeline sets an emitter's fields without OnValidate, so its values are compared like transforms
                if (!Nullable.Equals(StateOf(shapes[i]), _cachedEmitters[i]))
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
                    MaterialIndex = EmitterOf(shape) is Emitter emitter ? AddEmitterMaterial(shape.Material, emitter) : MaterialIndex(shape.Material),
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
                    _defaultMaterialIndex = AddMaterial(SpectralMaterial.DefaultAlbedo, null, Vector3.zero, null);

                return _defaultMaterialIndex;
            }

            if (!_materialIndices.TryGetValue(material, out int index))
            {
                (Vector3 emissionRgb, float[] emissionTable) = MaterialEmission(material);
                index = AddMaterial(material.Albedo, material.ReflectanceSpectrum, emissionRgb, emissionTable);
                _materialIndices.Add(material, index);
            }

            return index;
        }

        /**
         * <summary>
         * A shape's own material entry, with its emitter's light in place of the material's emission.
         * </summary>
         * <param name="material">The shape's material, or null for the default.</param>
         * <param name="emitter">The shape's emitter.</param>
         * <returns>The new entry's index.</returns>
         */
        private int AddEmitterMaterial(SpectralMaterial material, Emitter emitter)
        {
            (Vector3 emissionRgb, float[] emissionTable) = EmitterEmission(emitter);


            return material != null
                ? AddMaterial(material.Albedo, material.ReflectanceSpectrum, emissionRgb, emissionTable)
                : AddMaterial(SpectralMaterial.DefaultAlbedo, null, emissionRgb, emissionTable);
        }

        /**
         * <summary>
         * Adds one material entry from an albedo and an already resolved emission.
         * </summary>
         * <param name="albedo">The picker albedo, used when there is no measured reflectance.</param>
         * <param name="reflectance">A measured reflectance, or null.</param>
         * <param name="emissionRgb">The emission's linear sRGB color, for RGB mode and the sigmoid.</param>
         * <param name="emissionTable">The emission resampled for the GPU, or null to use the sigmoid.</param>
         * <returns>The entry's index.</returns>
         */
        private int AddMaterial(Color albedo, MeasuredSpectrum reflectance, Vector3 emissionRgb, float[] emissionTable)
        {
            Color linearAlbedo = albedo.linear;
            var albedoRgb = new Vector3(linearAlbedo.r, linearAlbedo.g, linearAlbedo.b);

            // a measured spectrum replaces the picker color in both modes, so RGB mode shows the same material
            if (reflectance != null)
                albedoRgb = reflectance.ReflectanceRgb();


            _materialData.Add(new GpuMaterial
            {
                AlbedoSpectrum = _sigmoidTable?.Reflectance(albedoRgb) ?? Vector4.zero,
                EmissionSpectrum = _sigmoidTable?.Unbounded(emissionRgb) ?? Vector4.zero,
                Albedo = albedoRgb,
                Emission = emissionRgb,
                Type = (int)MaterialType.Lambert,

                ReflectanceTable = reflectance != null ? AddSpectrumTable(reflectance.Resample()) : -1,
                EmissionTable = emissionTable != null ? AddSpectrumTable(emissionTable) : -1
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
            _cachedEmitters.Clear();

            // indexing a read-only list skips the boxed enumerator foreach would allocate
            IReadOnlyList<TracedShape> shapes = TracedRegistry.Shapes;
            for (int i = 0; i < shapes.Count; i++)
            {
                _cachedTransforms.Add(shapes[i].transform.localToWorldMatrix);
                _cachedEmitters.Add(StateOf(shapes[i]));
            }
        }

        /**
         * <summary>
         * Returns the emitter component of a shape, if any, and only if it is enabled.
         * </summary>
         * <param name="shape">The shape.</param>
         * <returns>The emitter, or null.</returns>
         */
        private static Emitter EmitterOf(TracedShape shape)
        {
            return shape.TryGetComponent(out Emitter emitter) && emitter.enabled ? emitter : null;
        }

        /**
         * <summary>
         * Returns the state of a shape's emitter, if any, and only if it is enabled.
         * </summary>
         * <param name="shape">The shape.</param>
         * <returns>The emitter's state, or null.</returns>
         */
        private static EmitterState? StateOf(TracedShape shape)
        {
            return EmitterOf(shape)?.State;
        }

        /**
         * <summary>
         * A material's emission as a color and, for a measured spectrum, a table.
         * </summary>
         * <param name="material">The material.</param>
         * <returns>The linear sRGB color and the table, or null for the sigmoid.</returns>
         */
        private static (Vector3, float[]) MaterialEmission(SpectralMaterial material)
        {
            float strength = material.EmissionStrength;
            if (material.EmissionSpectrum != null)
                return (material.EmissionSpectrum.EmissionRgb() * strength, material.EmissionSpectrum.Resample(material.EmissionSpectrum.EmissionNormalization() * strength));

            Color linear = material.EmissionColor.linear * strength;


            return (new Vector3(linear.r, linear.g, linear.b), null);
        }

        /**
         * <summary>
         * An emitter's emission as a color and, for a measured spectrum, a table.
         * </summary>
         * <param name="emitter">The emitter.</param>
         * <returns>The linear sRGB color and the table, or null for the sigmoid.</returns>
         */
        private static (Vector3, float[]) EmitterEmission(Emitter emitter)
        {
            float strength = emitter.Strength;
            switch (emitter.Kind)
            {
                case EmissionKind.Blackbody:
                    return (Blackbody.Rgb(emitter.Temperature) * strength, Blackbody.Resample(emitter.Temperature, strength));

                case EmissionKind.Measured when emitter.Spectrum != null:
                    return (emitter.Spectrum.EmissionRgb() * strength, emitter.Spectrum.Resample(emitter.Spectrum.EmissionNormalization() * strength));

                default:
                {
                    // the color scaled to Y = 1 first, so strength means the same thing for every kind
                    Color linear = emitter.Color.linear;
                    var rgb = new Vector3(linear.r, linear.g, linear.b);
                    float y = Cie.LinearSrgbToXyz(rgb).y;


                    return (y > 0f ? rgb / y * strength : Vector3.zero, null);
                }
            }
        }

        #endregion
    }
}