using UnityEngine;

namespace SpectralPathTracer
{
    [CreateAssetMenu(fileName = "SpectralMaterial", menuName = "Spectral Path Tracer/Spectral Material")]
    public class SpectralMaterial : ScriptableObject
    {
        #region Inspector Fields

        [field: Tooltip("Fraction of incoming light the surface reflects (between 0 and 1).")]
        [field: ColorUsage(false, false)]
        [field: SerializeField] public Color Albedo { get; private set; } = DefaultAlbedo;

        [field: Header("Emission")]

        [field: Tooltip("Hue of the emitted light.")]
        [field: ColorUsage(false, false)]
        [field: SerializeField] public Color EmissionColor { get; private set; } = Color.white;

        [field: Tooltip("Emitted radiance scale; 0 means the surface doesn't glow.")]
        [field: Min(0f)]
        [field: SerializeField] public float EmissionStrength { get; private set; } = 0f;

        #endregion

        #region Properties

        /**
         * <summary>
         * The albedo of new materials, and of shapes with no material.
         * </summary>
         */
        public static Color DefaultAlbedo { get; } = new Color(0.8f, 0.8f, 0.8f);

        #endregion

        private void OnValidate()
        {
            TracedRegistry.MarkChanged();
        }
    }
}