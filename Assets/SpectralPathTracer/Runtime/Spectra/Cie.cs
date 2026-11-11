using UnityEngine;

namespace SpectralPathTracer
{
    public static partial class Cie
    {
        #region Constants

        public const int FirstWavelength = 360; // the shortest wavelength the matching functions cover, in nm
        public const int LastWavelength = 830; // the longest, in nm
        public const int SampleCount = LastWavelength - FirstWavelength + 1; // one row per nm, 471 in all

        public const float YIntegral = 106.856895f; // the area under y-bar, so a flat spectrum of 1 gets Y = 1


        public const int D65First = 300; // D65's table starts lower, in nm
        public const int D65Step = 5; // and has one row every 5 nm

        #endregion

        #region Private Fields

        // funny matrix transformation from sRGB's red, green and blue primaries and its D65 white.
        private static readonly Matrix4x4 XyzToSrgb = new Matrix4x4
        {
            m00 =  3.2404542f, m01 = -1.5371385f, m02 = -0.4985314f,
            m10 = -0.9692660f, m11 =  1.8760108f, m12 =  0.0415560f,
            m20 =  0.0556434f, m21 = -0.2040259f, m22 =  1.0572252f,

            m33 = 1f
        };

        private static readonly Matrix4x4 SrgbToXyz = XyzToSrgb.inverse; // the way back, so there is no second table to get wrong

        #endregion

        /**
         * <summary>
         * Samples the three matching functions at a wavelength, interpolating between the 1 nm rows.
         * </summary>
         * <param name="lambda">The wavelength in nm.</param>
         * <returns>x-bar, y-bar and z-bar, or zero outside 360 to 830 nm.</returns>
         */
        public static Vector3 MatchingFunctions(float lambda)
        {
            float position = lambda - FirstWavelength;
            if (position < 0f || position > SampleCount - 1)
                return Vector3.zero;

            int row = Mathf.Min((int)position, SampleCount - 2); // the last wavelength interpolates from the row before
            float t = position - row;


            return new Vector3(Mathf.Lerp(X[row], X[row + 1], t), Mathf.Lerp(Y[row], Y[row + 1], t), Mathf.Lerp(Z[row], Z[row + 1], t));
        }

        /**
         * <summary>
         * Samples D65's relative power at a wavelength, interpolating between the 5 nm rows.
         * </summary>
         * <param name="lambda">The wavelength in nm.</param>
         * <returns>The relative power, or zero outside 300 to 830 nm.</returns>
         */
        public static float D65At(float lambda)
        {
            float position = (lambda - D65First) / D65Step;
            if (position < 0f || position > D65.Length - 1)
                return 0f;

            int row = Mathf.Min((int)position, D65.Length - 2);


            return Mathf.Lerp(D65[row], D65[row + 1], position - row);
        }

        /**
         * <summary>
         * Converts CIE XYZ to linear sRGB, whose white (1, 1, 1) is D65 at Y = 1.
         * </summary>
         * <param name="xyz">The color in XYZ.</param>
         * <returns>The linear sRGB color, which can be negative outside the sRGB gamut.</returns>
         */
        public static Vector3 XyzToLinearSrgb(Vector3 xyz)
        {
            return XyzToSrgb.MultiplyVector(xyz); // only the upper 3x3 needed to transform a vector
        }

        /**
         * <summary>
         * Converts linear sRGB to CIE XYZ.
         * </summary>
         * <param name="rgb">The linear sRGB color.</param>
         * <returns>The color in XYZ.</returns>
         */
        public static Vector3 LinearSrgbToXyz(Vector3 rgb)
        {
            return SrgbToXyz.MultiplyVector(rgb);
        }
    }
}
