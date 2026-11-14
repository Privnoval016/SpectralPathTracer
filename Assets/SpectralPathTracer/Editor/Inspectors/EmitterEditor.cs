using System.Collections.Generic;
using UnityEditor;

namespace SpectralPathTracer
{
    // shows only the fields the emitter's kind uses
    [CustomEditor(typeof(Emitter))]
    public class EmitterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // the script field stays read only, as the default inspector draws it
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

            DrawPropertiesExcluding(serializedObject, HiddenFields());
            serializedObject.ApplyModifiedProperties();
        }

        #region Helpers

        private string[] HiddenFields()
        {
            var kind = ((Emitter)target).Kind;
            var hidden = new List<string> { "m_Script" };

            if (kind != EmissionKind.Color)
                hidden.Add("<Color>k__BackingField");

            if (kind != EmissionKind.Measured)
                hidden.Add("<Spectrum>k__BackingField");

            if (kind != EmissionKind.Lines)
                hidden.Add("<Lines>k__BackingField");

            if (kind != EmissionKind.Blackbody)
                hidden.Add("<Temperature>k__BackingField");


            return hidden.ToArray();
        }

        #endregion
    }
}
