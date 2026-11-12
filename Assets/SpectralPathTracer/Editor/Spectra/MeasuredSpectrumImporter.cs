using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    // turns a CSV of measurements into MeasuredSpectrum assets, one per column after the wavelength
    public static class MeasuredSpectrumImporter
    {
        /**
         * <summary>
         * Imports the CSV selected in the Project window, writing the assets next to it.
         * </summary>
         */
        [MenuItem("Tools/Spectral Path Tracer/Import Spectra From CSV")]
        public static void ImportSelected()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".csv"))
            {
                Debug.LogError("Spectrum import: select a .csv file in the Project window first.");
                return;
            }

            Import(path);
        }

        /**
         * <summary>
         * Reads a CSV whose first column is nm and whose header row names each other column.
         * </summary>
         * <param name="path">The CSV's asset path.</param>
         */
        public static void Import(string path)
        {
            string[] lines = File.ReadAllLines(path);
            string[] names = lines[0].Split(',');

            for (int column = 1; column < names.Length; column++)
            {
                var curve = new AnimationCurve();
                for (int row = 1; row < lines.Length; row++)
                {
                    string[] cells = lines[row].Split(',');
                    if (cells.Length <= column)
                        continue;

                    // invariant, so a comma locale can't misread the decimal point
                    float lambda = float.Parse(cells[0], CultureInfo.InvariantCulture);
                    float value = float.Parse(cells[column], CultureInfo.InvariantCulture);
                    curve.AddKey(lambda, value);
                }

                // straight lines between measurements, since the data says nothing about the curve between them
                for (int key = 0; key < curve.length; key++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                }


                Save(path, names[column].Trim(), curve);
            }

            AssetDatabase.SaveAssets();
        }

        #region Helpers

        private static void Save(string csvPath, string column, AnimationCurve curve)
        {
            string assetPath = $"{Path.GetDirectoryName(csvPath)}/{Path.GetFileNameWithoutExtension(csvPath)}{column}.asset";

            // update an existing asset in place, so materials keep their reference after a reimport
            var spectrum = AssetDatabase.LoadAssetAtPath<MeasuredSpectrum>(assetPath);
            if (spectrum == null)
            {
                spectrum = ScriptableObject.CreateInstance<MeasuredSpectrum>();
                AssetDatabase.CreateAsset(spectrum, assetPath);
            }

            var serialized = new SerializedObject(spectrum);
            serialized.FindProperty("<Curve>k__BackingField").animationCurveValue = curve;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
