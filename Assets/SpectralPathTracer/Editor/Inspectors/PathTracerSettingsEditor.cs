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
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            string target = AccumulationProgress.TargetSamples == 0 ? "no limit" : AccumulationProgress.TargetSamples.ToString();
            EditorGUILayout.LabelField("Samples", $"{AccumulationProgress.Samples} / {target}");
        }

        public override bool RequiresConstantRepaint()
        {
            return AccumulationProgress.IsAccumulating;
        }
    }
}