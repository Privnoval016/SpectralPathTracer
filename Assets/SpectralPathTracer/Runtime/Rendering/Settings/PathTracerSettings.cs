using UnityEngine;

namespace SpectralPathTracer
{
    [CreateAssetMenu(fileName = "PathTracerSettings", menuName = "Spectral Path Tracer/Path Tracer Settings")]
    public class PathTracerSettings : ScriptableObject
    {
        #region Inspector Fields
        
        [Header("Mode Settings")]
        
        [Tooltip("Whether to render the path tracer in edit mode.")]
        [SerializeField] private bool RenderInEditMode = true;
        [Tooltip("Whether to render the path tracer in play mode.")]
        [SerializeField] private bool RenderInPlayMode = true;

        [Tooltip("Fraction of the output resolution rendered in Edit Mode.")]
        [SerializeField, Range(0.1f, 1f)] private float editModeScale = 0.5f;

        [Tooltip("Fraction of the output resolution rendered in Play Mode and builds.")]
        [SerializeField, Range(0.1f, 1f)] private float playModeScale = 1f;


        [field: Header("Renderer Settings")]

        [field: Tooltip("Whether to pause the path tracer.")]
        [field: SerializeField] public bool Paused { get; private set; } = false;

        [field: Tooltip("Size of the final image in pixels, independent of the Game view size.")]
        [field: SerializeField] public Vector2Int OutputResolution { get; private set; } = new Vector2Int(1920, 1080);

        [field: Tooltip("How many times a path may bounce after the camera ray's first hit.")]
        [field: Range(0, 16)]
        [field: SerializeField] public int MaxBounces { get; private set; } = 16;

        [field: Tooltip("End dim paths at random and boost the survivors, so long paths cost less without darkening the image.")]
        [field: SerializeField] public bool RussianRoulette { get; private set; } = true;

        [field: Tooltip("Place each sample at a random spot inside its pixel, which anti-aliases edges as samples accumulate.")]
        [field: SerializeField] public bool PixelJitter { get; private set; } = true;

        [field: Tooltip("Carry four wavelengths per path instead of RGB colors.")]
        [field: SerializeField] public bool Spectral { get; private set; } = false;

        [field: Tooltip("Stop accumulating after this many samples per pixel; 0 keeps going forever.")]
        [field: Min(0)]
        [field: SerializeField] public int TargetSamples { get; private set; } = 1024;

        [field: Tooltip("GPU time to aim for per frame in milliseconds; 0 runs one full sample every frame.")]
        [field: Min(0f)]
        [field: SerializeField] public float FrameBudgetMs { get; private set; } = 33f;

        [field: Header("Display")]

        [field: Tooltip("Brightness in stops, where each +1 doubles it, 0 leaves the radiance unchanged.")]
        [field: Range(-10f, 10f)]
        [field: SerializeField] public float Exposure { get; private set; } = 0f;

        [field: Tooltip("Shows a linear gray ramp instead of the image, to check that sRGB encoding happens exactly once.")]
        [field: SerializeField] public bool ShowGrayRamp { get; private set; } = false;

        [field: Tooltip("Replace the image with one wavelength per column, 380 to 780 nm at equal power.")]
        [field: SerializeField] public bool ShowSpectrumStrip { get; private set; } = false;


        #endregion

        #region Properties

        public float OutputAspectRatio => (float)OutputResolution.x / OutputResolution.y;

        #endregion

        /**
         * <summary>
         * Whether the path tracer renders in Play Mode or Edit Mode.
         * </summary>
         * <param name="isPlaying">Whether the Editor is in Play Mode.</param>
         * <returns>True if it renders in that mode.</returns>
         */
        public bool IsEnabledFor(bool isPlaying) => isPlaying ? RenderInPlayMode : RenderInEditMode;

        /**
         * <summary>
         * The resolution to render at in Play Mode or Edit Mode.
         * </summary>
         * <param name="isPlaying">Whether the Editor is in Play Mode.</param>
         * <returns>The render resolution.</returns>
         */
        public Vector2Int RenderResolutionFor(bool isPlaying)
        {
            float scale = isPlaying ? playModeScale : editModeScale;
            return new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(OutputResolution.x * scale)),
                Mathf.Max(1, Mathf.RoundToInt(OutputResolution.y * scale)));
        }
    }
}