using UnityEngine;

namespace SpectralPathTracer
{
    // sweeps an emitter's temperature evenly in mireds, so Timeline only has to animate a 0 to 1 progress
    [ExecuteAlways]
    [RequireComponent(typeof(Emitter))]
    public class TemperatureSweep : MonoBehaviour
    {
        #region Constants

        private const float MiredsPerKelvin = 1e6f; // a mired is 10^6 / T

        #endregion

        #region Private Fields

        private Emitter _emitter;

        #endregion

        #region Inspector Fields

        [field: Tooltip("The temperature at progress 0, in kelvin.")]
        [field: Range(1000f, 20000f)]
        [field: SerializeField] public float StartTemperature { get; private set; } = 1800f;

        [field: Tooltip("The temperature at progress 1, in kelvin.")]
        [field: Range(1000f, 20000f)]
        [field: SerializeField] public float EndTemperature { get; private set; } = 10000f;

        [field: Tooltip("How far along the sweep the emitter is; Timeline animates this.")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float Progress { get; private set; }

        #endregion

        #region Properties

        /**
         * <summary>
         * The temperature at the current progress, stepping evenly in mireds.
         * </summary>
         */
        public float Temperature => MiredsPerKelvin / Mathf.Lerp(MiredsPerKelvin / StartTemperature, MiredsPerKelvin / EndTemperature, Progress);

        #endregion

        private void OnEnable()
        {
            _emitter = GetComponent<Emitter>();
        }

        // animation writes progress before LateUpdate, and the scene compiles after it
        private void LateUpdate()
        {
            _emitter.Temperature = Temperature;
        }
    }
}
