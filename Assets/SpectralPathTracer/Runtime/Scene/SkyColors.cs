using UnityEngine;

namespace SpectralPathTracer
{
    public readonly struct SkyColors
    {
        #region Properties

        public Color Zenith { get; }
        public Color Horizon { get; }
        public Color Ground { get; }

        /**
         * <summary>
         * The sky used when the scene has no Sky component.
         * </summary>
         */
        public static SkyColors Default { get; } = new SkyColors(
            new Color(0.25f, 0.45f, 0.85f),
            new Color(0.85f, 0.9f, 1f),
            new Color(0.3f, 0.28f, 0.25f));

        #endregion


        public SkyColors(Color zenith, Color horizon, Color ground)
        {
            Zenith = zenith;
            Horizon = horizon;
            Ground = ground;
        }
    }
}
