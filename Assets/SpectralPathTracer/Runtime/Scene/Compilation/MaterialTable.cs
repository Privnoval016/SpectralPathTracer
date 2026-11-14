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
        private readonly List<Vector4> _lineData = new List<Vector4>(); // spectral lines, (center, sigma, peak, 0) each

        private readonly GrowableBuffer<GpuMaterial> _materials = new GrowableBuffer<GpuMaterial>("PathTracerMaterials");
        private readonly GrowableBuffer<float> _spectrumTables = new GrowableBuffer<float>("PathTracerSpectra");
        private readonly GrowableBuffer<Vector4> _lines = new GrowableBuffer<Vector4>("PathTracerLines");

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
            _lineData.Clear();
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
            if (material.LineCount > 0)
                return LinesAt(material, lambda);

            if (material.EmissionTable >= 0)
                return TableAt(material.EmissionTable, lambda);

            // an RGB light's white is D65, with the fitted shape and its scale on top
            return SigmoidSpectrum.EvaluateScaled(material.EmissionSpectrum, lambda) * Cie.D65At(lambda) * d65Scale;
        }

        public bool HasLines(int index)
        {
            return _materialData[index].LineCount > 0;
        }

        /**
         * <summary>
         * A material entry's lines, as (center, sigma, peak, 0).
         * </summary>
         * <param name="index">The entry's index.</param>
         * <returns>The entry's lines, empty if it has none.</returns>
         */
        public List<Vector4> LinesOf(int index)
        {
            GpuMaterial material = _materialData[index];
            return _lineData.GetRange(material.LineStart, material.LineCount);
        }

        public void Upload()
        {
            // the material buffer needs to hold at least one record
            if (_materialData.Count == 0)
                MaterialIndex(null);

            // the spectrum buffer needs at least one value even when nothing is measured
            if (_spectrumData.Count == 0)
                _spectrumData.Add(0f);

            // and the line buffer at least one line
            if (_lineData.Count == 0)
                _lineData.Add(Vector4.zero);

            _materials.Upload(_materialData);
            _spectrumTables.Upload(_spectrumData);
            _lines.Upload(_lineData);
        }

        public BufferHandle ImportMaterials(RenderGraph renderGraph)
        {
            return _materials.Import(renderGraph);
        }

        public BufferHandle ImportSpectra(RenderGraph renderGraph)
        {
            return _spectrumTables.Import(renderGraph);
        }

        public BufferHandle ImportLines(RenderGraph renderGraph)
        {
            return _lines.Import(renderGraph);
        }

        public void Dispose()
        {
            _materials.Dispose();
            _spectrumTables.Dispose();
            _lines.Dispose();
        }

        #region Helpers

        private int MaterialIndex(SpectralMaterial material)
        {
            if (material == null)
            {
                if (_defaultMaterialIndex < 0)
                    _defaultMaterialIndex = AddMaterial(SpectralMaterial.DefaultAlbedo, null, ResolvedEmission.None);

                return _defaultMaterialIndex;
            }

            if (!_materialIndices.TryGetValue(material, out int index))
            {
                index = AddMaterial(material.Albedo, material.ReflectanceSpectrum, MaterialEmission(material));
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
            ResolvedEmission emission = EmitterEmission(emitter);


            return material != null
                ? AddMaterial(material.Albedo, material.ReflectanceSpectrum, emission)
                : AddMaterial(SpectralMaterial.DefaultAlbedo, null, emission);
        }

        /**
         * <summary>
         * Adds one material entry from an albedo and an already resolved emission.
         * </summary>
         * <param name="albedo">The picker albedo, used when there is no measured reflectance.</param>
         * <param name="reflectance">A measured reflectance, or null.</param>
         * <param name="emission">The emission's color, and its table or lines if it has them.</param>
         * <returns>The entry's index.</returns>
         */
        private int AddMaterial(Color albedo, MeasuredSpectrum reflectance, ResolvedEmission emission)
        {
            Color linearAlbedo = albedo.linear;
            var albedoRgb = new Vector3(linearAlbedo.r, linearAlbedo.g, linearAlbedo.b);

            // a measured spectrum replaces the picker color in both modes, so RGB mode shows the same material
            if (reflectance != null)
                albedoRgb = reflectance.ReflectanceRgb();


            _materialData.Add(new GpuMaterial
            {
                AlbedoSpectrum = _sigmoidTable?.Reflectance(albedoRgb) ?? Vector4.zero,
                EmissionSpectrum = _sigmoidTable?.Unbounded(emission.Rgb) ?? Vector4.zero,
                Albedo = albedoRgb,
                Emission = emission.Rgb,
                Type = (int)MaterialType.Lambert,

                ReflectanceTable = reflectance != null ? AddSpectrumTable(reflectance.Resample()) : -1,
                EmissionTable = emission.Table != null ? AddSpectrumTable(emission.Table) : -1,
                LineStart = _lineData.Count,
                LineCount = AddLines(emission.Lines)
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

        /**
         * <summary>
         * Appends a lamp's lines to the line buffer.
         * </summary>
         * <param name="lines">The lines, or null for none.</param>
         * <returns>How many lines were added.</returns>
         */
        private int AddLines(Vector4[] lines)
        {
            if (lines == null)
                return 0;

            _lineData.AddRange(lines);
            return lines.Length;
        }

        // the CPU twin of LinesAt
        private float LinesAt(GpuMaterial material, int lambda)
        {
            float value = 0f;
            for (int i = 0; i < material.LineCount; i++)
                value += LineSpectrum.Gaussian(_lineData[material.LineStart + i], lambda);


            return value;
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
         * <returns>The linear sRGB color, and the table for a measured spectrum.</returns>
         */
        private static ResolvedEmission MaterialEmission(SpectralMaterial material)
        {
            float strength = material.EmissionStrength;
            if (material.EmissionSpectrum != null)
                return new ResolvedEmission(material.EmissionSpectrum.EmissionRgb() * strength, material.EmissionSpectrum.Resample(material.EmissionSpectrum.EmissionNormalization() * strength));

            Color linear = material.EmissionColor.linear * strength;


            return new ResolvedEmission(new Vector3(linear.r, linear.g, linear.b));
        }

        /**
         * <summary>
         * An emitter's emission as a color and, for a measured spectrum, a table.
         * </summary>
         * <param name="emitter">The emitter.</param>
         * <returns>The linear sRGB color, and the table or lines when the spectrum has them.</returns>
         */
        private static ResolvedEmission EmitterEmission(Emitter emitter)
        {
            float strength = emitter.Strength;
            switch (emitter.Kind)
            {
                case EmissionKind.Blackbody:
                    return new ResolvedEmission(Blackbody.Rgb(emitter.Temperature) * strength, Blackbody.Resample(emitter.Temperature, strength));

                case EmissionKind.Measured when emitter.Spectrum != null:
                    return new ResolvedEmission(emitter.Spectrum.EmissionRgb() * strength, emitter.Spectrum.Resample(emitter.Spectrum.EmissionNormalization() * strength));

                case EmissionKind.Lines when emitter.Lines != null && emitter.Lines.Lines.Count > 0:
                    return new ResolvedEmission(emitter.Lines.EmissionRgb() * strength, lines: emitter.Lines.GpuLines(emitter.Lines.EmissionNormalization() * strength));

                default:
                {
                    // the color scaled to Y = 1 first, so strength means the same thing for every kind
                    Color linear = emitter.Color.linear;
                    var rgb = new Vector3(linear.r, linear.g, linear.b);
                    float y = Cie.LinearSrgbToXyz(rgb).y;


                    return new ResolvedEmission(y > 0f ? rgb / y * strength : Vector3.zero);
                }
            }
        }

        #endregion
    }
}
