using System.Collections.Generic;
using UnityEditor;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The settings Inspector with a live sample count under the usual fields.
     * </summary>
     */
    [CustomEditor(typeof(PathTracerSettings))]
    public class PathTracerSettingsEditor : Editor
    {
        #region Private Fields

        private bool _showLivePaths = true;

        #endregion

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            string target = AccumulationProgress.TargetSamples == 0 ? "no limit" : AccumulationProgress.TargetSamples.ToString();
            EditorGUILayout.LabelField("Samples", $"{AccumulationProgress.Samples} / {target}");

            _showLivePaths = EditorGUILayout.Foldout(_showLivePaths, "Live paths per depth", true);
            if (_showLivePaths)
                DrawLivePaths(((PathTracerSettings)this.target).MaxBounces);
        }

        public override bool RequiresConstantRepaint()
        {
            return AccumulationProgress.IsAccumulating;
        }

        #region Helpers

        /**
         * <summary>
         * Lists each depth's live path count and its share of the paths that started.
         * </summary>
         * <param name="maxBounces">The deepest depth to list.</param>
         */
        private static void DrawLivePaths(int maxBounces)
        {
            IReadOnlyList<uint> livePaths = RenderStats.LivePaths;
            float started = livePaths[0];

            EditorGUI.indentLevel++;
            for (int depth = 0; depth <= maxBounces && depth < livePaths.Count; depth++)
            {
                float share = started > 0f ? livePaths[depth] / started : 0f;
                EditorGUILayout.LabelField($"Depth {depth}", $"{livePaths[depth]:N0}  ({share:P1})");
            }
            EditorGUI.indentLevel--;
        }

        #endregion
    }
}