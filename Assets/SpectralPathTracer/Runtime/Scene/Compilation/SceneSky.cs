using System.Collections.Generic;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Picks the scene's one sky each frame, and keeps its colors and their spectra.
     * </summary>
     */
    public class SceneSky
    {
        #region Private Fields

        private readonly SigmoidTable _sigmoidTable; // null when no table is assigned which leaves every spectrum black
        private bool _reportedMultipleSkies;

        #endregion

        #region Properties

        public SkyColors Colors { get; private set; }

        public Vector4 ZenithSpectrum { get; private set; }
        public Vector4 HorizonSpectrum { get; private set; }
        public Vector4 GroundSpectrum { get; private set; }

        #endregion

        /**
         * <summary>
         * Initializes a new instance of the <see cref="SceneSky"/> class.
         * </summary>
         * <param name="spectra">The table that turns RGB colors into spectra.</param>
         */
        public SceneSky(SigmoidTable spectra)
        {
            _sigmoidTable = spectra;
        }

        /**
         * <summary>
         * Picks this frame's sky, and refits its spectra if its colors changed.
         * </summary>
         * <param name="changed">True if the sky's colors changed this frame.</param>
         * <returns>False if more than one Sky is enabled.</returns>
         */
        public bool TryUpdate(out bool changed)
        {
            changed = false;
            if (!TryResolve(out SkyColors sky))
                return false;

            // sky colors change without a version bump so compare them directly
            changed = !sky.Zenith.Equals(Colors.Zenith) || !sky.Horizon.Equals(Colors.Horizon) || !sky.Ground.Equals(Colors.Ground);
            Colors = sky;

            // the sky is a light so its colors become unbounded spectra once per change
            if (changed)
            {
                ZenithSpectrum = Unbounded(sky.Zenith.linear);
                HorizonSpectrum = Unbounded(sky.Horizon.linear);
                GroundSpectrum = Unbounded(sky.Ground.linear);
            }

            return true;
        }

        /**
         * <summary>
         * The sky's radiance at one wavelength with its three colors averaged.
         * </summary>
         * <param name="lambda">The wavelength in nm.</param>
         * <param name="d65Scale">The factor that puts D65 at Y = 1.</param>
         * <returns>The averaged radiance.</returns>
         */
        public float RadianceAt(int lambda, float d65Scale)
        {
            float shape = SigmoidSpectrum.EvaluateScaled(ZenithSpectrum, lambda) + SigmoidSpectrum.EvaluateScaled(HorizonSpectrum, lambda) + SigmoidSpectrum.EvaluateScaled(GroundSpectrum, lambda);
            return shape / 3f * Cie.D65At(lambda) * d65Scale;
        }

        #region Helpers

        private Vector4 Unbounded(Color color)
        {
            return _sigmoidTable?.Unbounded(new Vector3(color.r, color.g, color.b)) ?? Vector4.zero;
        }

        /**
        * <summary>
        * Picks this frame's sky.
        * </summary>
        * <param name="sky">The sky to render with.</param>
        * <returns>False if more than one Sky is enabled.</returns>
        */
        private bool TryResolve(out SkyColors sky)
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

        #endregion
    }
}
