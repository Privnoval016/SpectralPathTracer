using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The scene's materials and the measured spectra they use, deduplicated by asset and uploaded as two tables.
     * </summary>
     */
    public class MaterialTable
    {
        #region Private Fields

        private readonly List<GpuMaterial> _materialData = new List<GpuMaterial>();

        /**
         * <summary>
         * Keyed by material asset, resolving to its index in the material table.
         * </summary>
         */
        private readonly Dictionary<SpectralMaterial, int> _materialIndices = new Dictionary<SpectralMaterial, int>();
        private int _defaultMaterialIndex = -1;

        private readonly List<float> _spectrumData = new List<float>(); // measured spectra, 95 values each

        private readonly GrowableBuffer<GpuMaterial> _materials = new GrowableBuffer<GpuMaterial>("PathTracerMaterials");
        private readonly GrowableBuffer<float> _spectrumTables = new GrowableBuffer<float>("PathTracerSpectra");

        private readonly SigmoidTable _sigmoidTable; // null when no table is assigned which leaves every spectrum black

        #endregion

        /**
         * <summary>
         * Initializes a new instance of the <see cref="MaterialTable"/> class.
         * </summary>
         * <param name="spectra">The table that turns RGB colors into spectra.</param>
         */
        public MaterialTable(SigmoidTable spectra)
        {
            _sigmoidTable = spectra;
        }

        /**
         * <summary>
         * Empties the tables before a rebuild.
         * </summary>
         */
        public void Clear()
        {
            _materialData.Clear();
            _spectrumData.Clear();
            _materialIndices.Clear();
            _defaultMaterialIndex = -1;
        }

        /**
         * <summary>
         * The material entry a shape renders with: its own entry if it has an emitter, otherwise its material's shared one.
         * </summary>
         * <param name="shape">The shape.</param>
         * <returns>The entry's index.</returns>
         */
        public int IndexFor(TracedShape shape)
        {
            return Emitter.On(shape) is Emitter emitter ? AddEmitterMaterial(shape.Material, emitter) : MaterialIndex(shape.Material);
        }

        /**
         * <summary>
         * A material entry's emitted radiance at one wavelength, chosen the way EmissionSpectrum chooses on the GPU.
         * </summary>
         * <param name="index">The entry's index.</param>
         * <param name="lambda">The wavelength in nm.</param>
         * <param name="d65Scale">The factor that puts D65 at Y = 1.</param>
         * <returns>The emitted radiance.</returns>
         */
        public float EmissionAt(int index, int lambda, float d65Scale)
        {
            GpuMaterial material = _materialData[index];
            if (material.EmissionTable >= 0)
                return TableAt(material.EmissionTable, lambda);

            // an RGB light's white is D65, with the fitted shape and its scale on top
            return SigmoidSpectrum.EvaluateScaled(material.EmissionSpectrum, lambda) * Cie.D65At(lambda) * d65Scale;
        }

        public void Upload()
        {
            // the material buffer needs to hold at least one record
            if (_materialData.Count == 0)
                MaterialIndex(null);

            // the spectrum buffer needs at least one value even when nothing is measured
            if (_spectrumData.Count == 0)
                _spectrumData.Add(0f);

            _materials.Upload(_materialData);
            _spectrumTables.Upload(_spectrumData);
        }

        public BufferHandle ImportMaterials(RenderGraph renderGraph)
        {
            return _materials.Import(renderGraph);
        }

        public BufferHandle ImportSpectra(RenderGraph renderGraph)
        {
            return _spectrumTables.Import(renderGraph);
        }

        public void Dispose()
        {
            _materials.Dispose();
            _spectrumTables.Dispose();
        }

        #region Helpers

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

        // the CPU twin of TabulatedAt, reading the 5 nm values this rebuild appended
        private float TableAt(int table, int lambda)
        {
            float position = Mathf.Clamp((lambda - (int)SpectrumTables.FirstWavelength) / (float)(int)SpectrumTables.Step, 0f, (int)SpectrumTables.Count - 1);
            int row = Mathf.Min((int)position, (int)SpectrumTables.Count - 2);
            int start = table * (int)SpectrumTables.Count;


            return Mathf.Lerp(_spectrumData[start + row], _spectrumData[start + row + 1], position - row);
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
