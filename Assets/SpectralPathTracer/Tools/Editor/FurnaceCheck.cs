using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpectralPathTracer
{
    // in a uniform sky of 1 every lossless material reads exactly 1 at any sample count, so after a change
    // anything else in the furnace means a weight, a pdf or a leak is wrong
    public static class FurnaceCheck
    {
        // the Game view's class is internal to the Editor, so it can only be found by name
        private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

        /**
         * <summary>
         * Measures the accumulated image and logs its mean, min, max and sample counts.
         * </summary>
         */
        [MenuItem("Tools/Spectral Path Tracer/Furnace Check")]
        public static void Run()
        {
            AccumulationCapture.Request(Report);

            // a converged image stops repainting, so draw one more frame to take the request
            foreach (Object view in Resources.FindObjectsOfTypeAll(GameViewType))
                ((EditorWindow)view).Repaint();
        }

        private static void Report(Color[] means, Vector2Int size)
        {
            Vector4 min = Vector4.positiveInfinity;
            Vector4 max = Vector4.negativeInfinity;
            double r = 0, g = 0, b = 0; // half a million floats summed as floats would drift

            foreach (Color mean in means)
            {
                min = Vector4.Min(min, mean);
                max = Vector4.Max(max, mean);
                r += mean.r;
                g += mean.g;
                b += mean.b;
            }


            int n = means.Length;
            Debug.Log($"Furnace at {size.x} by {size.y}: mean ({r / n:F5}, {g / n:F5}, {b / n:F5}), " +
                      $"min {((Vector3)min).ToString("F5")}, max {((Vector3)max).ToString("F5")}, samples {min.w} to {max.w}");
        }
    }
}
