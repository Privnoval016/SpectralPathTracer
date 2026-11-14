using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * A light's emission resolved into a color and a table or lines when the spectrum isn't a sigmoid (for the GPU)
     * </summary>
     */
    public readonly struct ResolvedEmission
    {
        #region Properties

        public Vector3 Rgb { get; } // linear sRGB, for RGB mode and the sigmoid
        public float[] Table { get; } // the spectrum every 5 nm, or null
        public Vector4[] Lines { get; } // the Gaussian lines as (center, sigma, peak, 0), or null

        /**
         * <summary>
         * No emission at all.
         * </summary>
         */
        public static ResolvedEmission None => new ResolvedEmission(Vector3.zero);

        #endregion


        public ResolvedEmission(Vector3 rgb, float[] table = null, Vector4[] lines = null)
        {
            Rgb = rgb;
            Table = table;
            Lines = lines;
        }
    }
}
