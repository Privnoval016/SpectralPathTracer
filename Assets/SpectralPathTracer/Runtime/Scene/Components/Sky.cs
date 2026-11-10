using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * The scene's environment.
     * </summary>
     */
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class Sky : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("Color straight up.")]
        [SerializeField, ColorUsage(false, true)] private Color zenithColor = SkyColors.Default.Zenith;

        [Tooltip("Color at the horizon.")]
        [SerializeField, ColorUsage(false, true)] private Color horizonColor = SkyColors.Default.Horizon;

        [Tooltip("Color below the horizon.")]
        [SerializeField, ColorUsage(false, true)] private Color groundColor = SkyColors.Default.Ground;

        #endregion


        #region Properties

        public SkyColors Colors => new SkyColors(zenithColor, horizonColor, groundColor);

        #endregion

        private void OnEnable()
        {
            TracedRegistry.Register(this);
        }

        private void OnDisable()
        {
            TracedRegistry.Unregister(this);
        }
    }
}
