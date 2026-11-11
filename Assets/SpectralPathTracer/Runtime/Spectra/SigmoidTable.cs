using System.IO;
using UnityEngine;

namespace SpectralPathTracer
{
    // the fitted sigmoid coefficients for every sRGB color (this is literally just a lookup table)
    public class SigmoidTable
    {
        #region Properties

        public int Resolution { get; }
        public float[] Brightness { get; } // the value of the largest channel at each step denser near 0 and 1
        public float[] Coefficients { get; } // c0, c1, c2 for a given nm

        #endregion

        public SigmoidTable(int resolution, float[] brightness, float[] coefficients)
        {
            Resolution = resolution;
            Brightness = brightness;
            Coefficients = coefficients;
        }

        /**
         * <summary>
         * Reads a table written by the sigmoid table builder (goat).
         * </summary>
         * <param name="bytes">The file's contents.</param>
         * <returns>The table.</returns>
         */
        public static SigmoidTable Read(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes));
            int resolution = reader.ReadInt32();

            var brightness = new float[resolution];
            for (int k = 0; k < resolution; k++)
                brightness[k] = reader.ReadSingle();

            var coefficients = new float[3 * resolution * resolution * resolution * 3];
            for (int n = 0; n < coefficients.Length; n++)
                coefficients[n] = reader.ReadSingle();


            return new SigmoidTable(resolution, brightness, coefficients);
        }

        /**
         * <summary>
         * Finds the sigmoid coefficients for a color by interpolating between the eight nearest fits.
         * </summary>
         * <param name="rgb">The linear sRGB color.</param>
         * <returns>c0, c1 and c2 for a wavelength in nm.</returns>
         */
        public Vector3 Lookup(Vector3 rgb)
        {
            // the table only covers [0, 1] so we need to scale down brighter inputs
            rgb = Vector3.Max(Vector3.zero, Vector3.Min(Vector3.one, rgb));

            int largest = rgb.x >= rgb.y && rgb.x >= rgb.z ? 0 : rgb.y >= rgb.z ? 1 : 2;
            float z = rgb[largest];

            // black has no ratios so it can just take the darkest fit
            if (z <= 0f)
                return Read(Index(largest, 0, 0, 0));

            // the other two channels as fractions of the largest in grid steps
            float steps = Resolution - 1;
            float x = rgb[(largest + 1) % 3] / z * steps;
            float y = rgb[(largest + 2) % 3] / z * steps;

            int i = Mathf.Min((int)x, Resolution - 2);
            int j = Mathf.Min((int)y, Resolution - 2);
            int k = BrightnessStep(z);

            float tx = x - i;
            float ty = y - j;
            float tz = (z - Brightness[k]) / (Brightness[k + 1] - Brightness[k]);


            // blend along x on the four edges of the cell, then along y, then along z and yay
            Vector3 front = Vector3.Lerp(
                Vector3.Lerp(Read(Index(largest, k, j, i)), Read(Index(largest, k, j, i + 1)), tx),
                Vector3.Lerp(Read(Index(largest, k, j + 1, i)), Read(Index(largest, k, j + 1, i + 1)), tx), ty);
            Vector3 back = Vector3.Lerp(
                Vector3.Lerp(Read(Index(largest, k + 1, j, i)), Read(Index(largest, k + 1, j, i + 1)), tx),
                Vector3.Lerp(Read(Index(largest, k + 1, j + 1, i)), Read(Index(largest, k + 1, j + 1, i + 1)), tx), ty);

            return Vector3.Lerp(front, back, tz); // so basically we just did a trilinear interpolation where the largest channel is used for brightness and the other two are ratios of it hoo ray
        }

        /**
         * <summary>
         * Where one fit's three coefficients start in the flat array.
         * </summary>
         * <param name="largest">Which channel is largest, 0 for red.</param>
         * <param name="k">The brightness step.</param>
         * <param name="j">The second ratio's step.</param>
         * <param name="i">The first ratio's step.</param>
         * <returns>The index of c0.</returns>
         */
        public int Index(int largest, int k, int j, int i)
        {
            return (((largest * Resolution + k) * Resolution + j) * Resolution + i) * 3;
        }

        #region Helpers

        private Vector3 Read(int index)
        {
            return new Vector3(Coefficients[index], Coefficients[index + 1], Coefficients[index + 2]);
        }

        /**
         * <summary>
         * Finds the brightness step just below a value wtf binary search
         * </summary>
         * <param name="z">The largest channel's value.</param>
         * <returns>The step k with Brightness[k] at most z, and k + 1 still inside the table.</returns>
         */
        private int BrightnessStep(float z)
        {
            int low = 0, high = Resolution - 2;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (Brightness[middle] <= z)
                    low = middle;
                else
                    high = middle - 1;
            }


            return low;
        }

        #endregion
    }
}
