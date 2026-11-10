using UnityEngine;

namespace SpectralPathTracer
{
    [CreateAssetMenu(fileName = "SpectralMaterial", menuName = "Spectral Path Tracer/Spectral Material")]
    public class SpectralMaterial : ScriptableObject
    {
        #region Inspector Fields

        [field: Tooltip("Fraction of incoming light the surface reflects (between 0 and 1).")]
        [field: ColorUsage(false, false)]
        [field: SerializeField] public Color Albedo { get; private set; } = new Color(0.8f, 0.8f, 0.8f);

        #endregion

        private void OnValidate()
        {
            TracedRegistry.MarkChanged();
        }
    }
}