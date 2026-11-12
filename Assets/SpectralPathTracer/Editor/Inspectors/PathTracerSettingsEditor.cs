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
            serializedObject.Update();

            // the script field stays read only, as the default inspector draws it
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

            DrawPropertiesExcluding(serializedObject, HiddenFields());
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            string target = AccumulationProgress.TargetSamples == 0 ? "no limit" : AccumulationProgress.TargetSamples.ToString();
            EditorGUILayout.LabelField("Samples", $"{AccumulationProgress.Samples} / {target}");
            EditorGUILayout.LabelField("Frame", $"{RenderStats.FrameMs:F1} ms, {RenderStats.WavesPerFrame} waves");

            // split screen runs every wave twice, so the counts cover both images
            string heading = ((PathTracerSettings)this.target).SplitScreen ? "Live paths per depth (both images)" : "Live paths per depth";
            _showLivePaths = EditorGUILayout.Foldout(_showLivePaths, heading, true);
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
         * The fields that don't apply with the current settings, so the inspector leaves them out.
         * </summary>
         * <returns>The serialized names to skip, including the script field drawn above.</returns>
         */
        private string[] HiddenFields()
        {
            var settings = (PathTracerSettings)this.target;
            var hidden = new List<string> { "m_Script" };

            if (!settings.IsEnabledFor(false))
                hidden.Add("editModeScale");

            if (!settings.IsEnabledFor(true))
                hidden.Add("playModeScale");

            // split screen renders both modes, so the single-mode switch does nothing
            if (settings.SplitScreen)
                hidden.Add("<Spectral>k__BackingField");
            else
                hidden.Add("<SplitPosition>k__BackingField");


            return hidden.ToArray();
        }

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