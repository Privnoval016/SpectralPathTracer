using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SpectralPathTracer
{
    // fits every grid point of the sigmoid table and writes it to a binary file so we can just load it at runtime with a lookup table
    public static class SigmoidTableBuilder
    {
        #region Constants

        public const int Resolution = 64; // steps per axis (reference x PBRT)
        public const string OutputPath = "Assets/SpectralPathTracer/Data/SigmoidTable.bytes"; // aaa

        private const int StartStep = Resolution / 5; // a mid brightness where itll converges easily (probably)

        #endregion

        /**
         * <summary>
         * Fits the whole table and writes it to the data folder.
         * </summary>
         */
        [MenuItem("Tools/Spectral Path Tracer/Build Sigmoid Table")]
        public static void Build()
        {
            float[] brightness = BrightnessSteps();
            var table = new SigmoidTable(Resolution, brightness, new float[3 * Resolution * Resolution * Resolution * 3]);
            Stopwatch stopwatch = Stopwatch.StartNew();

            // every slice is independent and writes its own part of the table
            Parallel.For(0, 3 * Resolution, slice => FitSlice(table, slice / Resolution, slice % Resolution));


            Write(table);
            Debug.Log($"Sigmoid table: {3 * Resolution * Resolution * Resolution} fits in {stopwatch.Elapsed.TotalSeconds:F1} s");
        }

        #region Helpers

        /**
         * <summary>
         * Fits one slice of the table, walking brightness out from the middle so each fit starts from its neighbor.
         * </summary>
         * <param name="table">The table being filled.</param>
         * <param name="largest">Which channel is largest, 0 for red.</param>
         * <param name="j">The second ratio's step.</param>
         */
        private static void FitSlice(SigmoidTable table, int largest, int j)
        {
            for (int i = 0; i < Resolution; i++)
            {
                Vector3 coefficients = Vector3.zero;
                for (int k = StartStep; k < Resolution; k++)
                    FitPoint(table, largest, k, j, i, ref coefficients);

                coefficients = Vector3.zero;
                for (int k = StartStep; k >= 0; k--)
                    FitPoint(table, largest, k, j, i, ref coefficients);
            }
        }

        /**
         * <summary>
         * Fits the color at one grid point and stores its coefficients in nm.
         * </summary>
         * <param name="table">The table being filled.</param>
         * <param name="largest">Which channel is largest, 0 for red.</param>
         * <param name="k">The brightness step.</param>
         * <param name="j">The second ratio's step.</param>
         * <param name="i">The first ratio's step.</param>
         * <param name="coefficients">The neighbor's fit going in, this point's fit coming out.</param>
         */
        private static void FitPoint(SigmoidTable table, int largest, int k, int j, int i, ref Vector3 coefficients)
        {
            float steps = Resolution - 1;
            float z = table.Brightness[k];

            // grid position where the largest channel is z and the others are ratios of it
            Vector3 rgb = Vector3.zero;
            rgb[largest] = z;
            rgb[(largest + 1) % 3] = i / steps * z;
            rgb[(largest + 2) % 3] = j / steps * z;

            SigmoidFit.Fit(rgb, ref coefficients);
            Vector3 nanometres = SigmoidFit.ToNanometres(coefficients);


            int index = table.Index(largest, k, j, i);
            table.Coefficients[index] = nanometres.x;
            table.Coefficients[index + 1] = nanometres.y;
            table.Coefficients[index + 2] = nanometres.z;
        }

        private static float[] BrightnessSteps()
        {
            var brightness = new float[Resolution];
            for (int k = 0; k < Resolution; k++)
                brightness[k] = Smoothstep(Smoothstep(k / (Resolution - 1f))); // do it twice so its more concentrated at the lwo end

            return brightness;
        }

        private static float Smoothstep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static void Write(SigmoidTable table)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            using (var writer = new BinaryWriter(File.Create(OutputPath)))
            {
                writer.Write(table.Resolution);
                foreach (float step in table.Brightness)
                    writer.Write(step);

                foreach (float coefficient in table.Coefficients)
                    writer.Write(coefficient);
            }


            AssetDatabase.ImportAsset(OutputPath);
        }

        #endregion
    }
}
