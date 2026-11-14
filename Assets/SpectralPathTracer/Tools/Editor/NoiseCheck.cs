using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SpectralPathTracer
{
    // how far the open scene's image is from a high-sample render of the same scene, so a sampling change can be
    // measured in numbers instead of judged by eye
    public static class NoiseCheck
    {
        #region Constants

        private const string Folder = "Recordings/NoiseReferences"; // git-ignored, next to the movie frames

        #endregion

        // the Game view's class is internal to the Editor, so it can only be found by name
        private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

        /**
         * <summary>
         * Saves the accumulated image as the open scene's reference, ideally after thousands of samples.
         * </summary>
         */
        [MenuItem("Tools/Spectral Path Tracer/Noise Check/Save Reference")]
        public static void SaveReference()
        {
            Capture(Save);
        }

        /**
         * <summary>
         * Logs how far the accumulated image is from the open scene's reference.
         * </summary>
         */
        [MenuItem("Tools/Spectral Path Tracer/Noise Check/Compare")]
        public static void Compare()
        {
            Capture(Report);
        }

        #region Helpers

        private static void Capture(Action<Vector3[], Vector2Int, int> onCaptured)
        {
            // split screen captures only the RGB half's buffer, which isn't the image being measured
            PathTracerSettings settings = FurnaceCheck.LoadSettings();
            if (settings != null && settings.SplitScreen && !PathTracerOverrides.Spectral.HasValue)
            {
                Debug.LogWarning("Noise check: turn split screen off first, so the capture is the image being measured.");
                return;
            }

            AccumulationCapture.Request((means, size) => onCaptured(LinearSrgb(means), size, (int)means[0].a));

            // a converged image stops repainting, so draw one more frame to take the request
            foreach (Object view in Resources.FindObjectsOfTypeAll(GameViewType))
                ((EditorWindow)view).Repaint();
        }

        /**
         * <summary>
         * Turns captured means into linear sRGB, so RGB and spectral images are measured in the same units.
         * </summary>
         * <param name="means">Each pixel's mean, XYZ in spectral mode.</param>
         * <returns>Each pixel's linear sRGB color.</returns>
         */
        private static Vector3[] LinearSrgb(Color[] means)
        {
            bool xyz = FurnaceCheck.IsSpectral(out float lockedWavelength) && lockedWavelength == 0f;

            var colors = new Vector3[means.Length];
            for (int i = 0; i < means.Length; i++)
            {
                var color = new Vector3(means[i].r, means[i].g, means[i].b);
                colors[i] = xyz ? Cie.XyzToLinearSrgb(color) : color;
            }


            return colors;
        }

        private static void Save(Vector3[] colors, Vector2Int size, int samples)
        {
            Directory.CreateDirectory(Folder);
            using (var writer = new BinaryWriter(File.Create(ReferencePath())))
            {
                writer.Write(size.x);
                writer.Write(size.y);
                writer.Write(samples);
                foreach (Vector3 color in colors)
                {
                    writer.Write(color.x);
                    writer.Write(color.y);
                    writer.Write(color.z);
                }
            }

            Debug.Log($"Noise check: saved a {size.x} by {size.y} reference at {samples} samples to {ReferencePath()}");
        }

        private static void Report(Vector3[] colors, Vector2Int size, int samples)
        {
            if (!TryLoad(size, out Vector3[] reference, out int referenceSamples))
                return;

            // squared differences summed per channel in double, since half a million floats would drift
            double r = 0, g = 0, b = 0, relative = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                Vector3 difference = colors[i] - reference[i];
                r += difference.x * difference.x;
                g += difference.y * difference.y;
                b += difference.z * difference.z;
                relative += Relative(difference.x, reference[i].x) + Relative(difference.y, reference[i].y) + Relative(difference.z, reference[i].z);
            }

            int n = colors.Length;
            double rmse = Math.Sqrt((r + g + b) / (3.0 * n));

            // relative MSE weighs every pixel by its own brightness, so a bright lamp can't drown out the walls
            Debug.Log($"Noise check: relMSE {relative / (3.0 * n):F5}, RMSE {rmse:F5}, per channel ({Math.Sqrt(r / n):F5}, {Math.Sqrt(g / n):F5}, {Math.Sqrt(b / n):F5}), " +
                      $"at {samples} samples against a {referenceSamples} sample reference");
        }

        /**
         * <summary>
         * One channel's squared error over its reference value squared, the measure rendering papers compare noise by.
         * </summary>
         * <param name="difference">The channel's difference from the reference.</param>
         * <param name="reference">The channel's reference value.</param>
         * <returns>The relative squared error.</returns>
         */
        private static double Relative(float difference, float reference)
        {
            return difference * difference / (reference * reference + 0.01); // the 0.01 keeps black pixels from dividing by zero
        }

        private static bool TryLoad(Vector2Int size, out Vector3[] reference, out int samples)
        {
            reference = null;
            samples = 0;
            if (!File.Exists(ReferencePath()))
            {
                Debug.LogWarning($"Noise check: no reference at {ReferencePath()}; render one to many samples and save it first.");
                return false;
            }

            using var reader = new BinaryReader(File.OpenRead(ReferencePath()));
            var referenceSize = new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
            samples = reader.ReadInt32();
            if (referenceSize != size)
            {
                Debug.LogWarning($"Noise check: the reference is {referenceSize.x} by {referenceSize.y} but the image is {size.x} by {size.y}.");
                return false;
            }

            reference = new Vector3[size.x * size.y];
            for (int i = 0; i < reference.Length; i++)
                reference[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());


            return true;
        }

        private static string ReferencePath()
        {
            return Path.Combine(Folder, SceneManager.GetActiveScene().name + ".bin");
        }

        #endregion
    }
}
