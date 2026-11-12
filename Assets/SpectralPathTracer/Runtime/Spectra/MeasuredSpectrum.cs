using UnityEngine;

namespace SpectralPathTracer
{
    // a spectrum from real measurements or drawn by hand, as a curve over wavelength in nm
    [CreateAssetMenu(fileName = "MeasuredSpectrum", menuName = "Spectral Path Tracer/Measured Spectrum")]
    public class MeasuredSpectrum : ScriptableObject
    {
        #region Inspector Fields

        [field: Tooltip("The value at each wavelength in nm; outside its keys it holds the first and last values.")]
        [field: SerializeField] public AnimationCurve Curve { get; private set; } = AnimationCurve.Linear(360f, 1f, 830f, 1f);

        #endregion

        /**
         * <summary>
         * Samples the spectrum at a wavelength.
         * </summary>
         * <param name="lambda">The wavelength in nm.</param>
         * <returns>The value, holding the end values outside the measured range.</returns>
         */
        public float Evaluate(float lambda)
        {
            Keyframe[] keys = Curve.keys;

            // a wall doesn't stop reflecting where the measurement stopped
            float first = keys[0].time;
            float last = keys[keys.Length - 1].time;


            return Curve.Evaluate(Mathf.Clamp(lambda, first, last));
        }

        /**
         * <summary>
         * Resamples the spectrum onto the GPU's fixed grid.
         * </summary>
         * <param name="scale">A factor every value is multiplied by.</param>
         * <returns>One value every 5 nm from 360 to 830 nm.</returns>
         */
        public float[] Resample(float scale = 1f)
        {
            var values = new float[(int)SpectrumTables.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = Evaluate((int)SpectrumTables.FirstWavelength + i * (int)SpectrumTables.Step) * scale;

            return values;
        }

        /**
         * <summary>
         * The color this spectrum has as a reflectance lit by D65.
         * </summary>
         * <returns>The linear sRGB albedo.</returns>
         */
        public Vector3 ReflectanceRgb()
        {
            Vector3 xyz = Vector3.zero;
            float whiteY = 0f;

            for (int lambda = Cie.FirstWavelength; lambda <= Cie.LastWavelength; lambda++)
            {
                Vector3 light = Cie.D65At(lambda) * Cie.MatchingFunctions(lambda); // D65 seen by the eye at this wavelength
                xyz += Evaluate(lambda) * light;
                whiteY += light.y;
            }

            // dividing by D65's own Y makes a reflectance of 1 everywhere sRGB white
            return Cie.XyzToLinearSrgb(xyz / whiteY);
        }

        /**
         * <summary>
         * The color of this spectrum as a light, at a brightness of Y = 1.
         * </summary>
         * <returns>The linear sRGB color of the light's shape.</returns>
         */
        public Vector3 EmissionRgb()
        {
            Vector3 xyz = Integrate();
            return Cie.XyzToLinearSrgb(xyz / xyz.y);
        }

        /**
         * <summary>
         * The factor that brings this spectrum, as a light, to Y = 1 through the renderer's estimator.
         * </summary>
         * <returns>The integral of y-bar over the integral of the spectrum times y-bar.</returns>
         */
        public float EmissionNormalization()
        {
            return Cie.YIntegral / Integrate().y;
        }

        private void OnValidate()
        {
            TracedRegistry.MarkChanged(); // materials that use this spectrum rebuild
        }

        #region Helpers

        private Vector3 Integrate()
        {
            Vector3 xyz = Vector3.zero;
            for (int lambda = Cie.FirstWavelength; lambda <= Cie.LastWavelength; lambda++)
                xyz += Evaluate(lambda) * Cie.MatchingFunctions(lambda); // 1 nm steps, so each row is worth 1 nm

            return xyz;
        }

        #endregion
    }
}
