using UnityEngine;

namespace SpectralPathTracer
{
    public enum EmissionKind
    {
        Color, // the color on top of D65, like a material's emission
        Measured, // a MeasuredSpectrum
        Blackbody, // Planck's law at a temperature
        Lines // a LineSpectrum's sharp lines
    }

    // overrides the emission of the shape's material, on a component so Timeline can animate it
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TracedShape))]
    public class Emitter : MonoBehaviour
    {
        #region Inspector Fields

        [field: Tooltip("Where the light's spectrum comes from.")]
        [field: SerializeField] public EmissionKind Kind { get; private set; } = EmissionKind.Blackbody;

        [field: Tooltip("The light's color, for the Color kind.")]
        [field: ColorUsage(false, false)]
        [field: SerializeField] public Color Color { get; private set; } = Color.white;

        [field: Tooltip("The measured emission, for the Measured kind.")]
        [field: SerializeField] public MeasuredSpectrum Spectrum { get; private set; }

        [field: Tooltip("The lamp's lines, for the Lines kind.")]
        [field: SerializeField] public LineSpectrum Lines { get; private set; }

        [field: Tooltip("The temperature in kelvin, for the Blackbody kind.")]
        [field: Range(Blackbody.MinTemperature, Blackbody.MaxTemperature)]
        [field: SerializeField] public float Temperature { get; set; } = 6504f; // public so a sweep can drive it

        [field: Tooltip("Brightness as luminance Y, the same at every color and temperature.")]
        [field: Min(0f)]
        [field: SerializeField] public float Strength { get; private set; } = 1f;

        #endregion

        #region Properties

        public EmitterState State => new EmitterState(Kind, Color, Spectrum, Lines, Temperature, Strength);

        #endregion

        private void OnEnable()
        {
            TracedRegistry.MarkChanged();
        }

        private void OnDisable()
        {
            TracedRegistry.MarkChanged();
        }

        private void OnValidate()
        {
            TracedRegistry.MarkChanged();
        }

        /**
         * <summary>
         * Returns the emitter component of a shape, if any, and only if it is enabled.
         * </summary>
         * <param name="shape">The shape.</param>
         * <returns>The emitter, or null.</returns>
         */
        public static Emitter On(TracedShape shape)
        {
            return shape.TryGetComponent(out Emitter emitter) && emitter.enabled ? emitter : null;
        }
    }
}
