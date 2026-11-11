using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Per-user settings for the ray fan overlay.
     * </summary>
     */
    [FilePath("UserSettings/SpectralPathTracer/RayFanSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class RayFanSettings : ScriptableSingleton<RayFanSettings>
    {
        #region Properties

        /**
         * <summary>
         * Path tracer settings that supply the output aspect ratio for the rays.
         * </summary>
         */
        [field: SerializeField] public PathTracerSettings PathTracerSettings { get; set; }

        [field: SerializeField] public int Columns { get; set; } = 8;
        [field: SerializeField] public int Rows { get; set; } = 5;

        /**
         * <summary>
         * World-space length of each drawn ray.
         * </summary>
         */
        [field: SerializeField] public float RayLength { get; set; } = 5f;

        /**
         * <summary>
         * Line thickness in screen pixels.
         * </summary>
         */
        [field: SerializeField] public float Thickness { get; set; } = 2f;

        /**
         * <summary>
         * Color each ray the way the kernel colors its pixel (direction mapped to RGB).
         * </summary>
         */
        [field: SerializeField] public bool ColorByDirection { get; set; } = true;

        [field: SerializeField] public Color RayColor { get; set; } = Color.yellow;

        /**
         * <summary>
         * Draw the image plane: the rectangle 1 unit in front of the camera that the rays pass through.
         * </summary>
         */
        [field: SerializeField] public bool ShowImagePlane { get; set; } = true;

        /**
         * <summary>
         * Draw the four corner rays, the edges of the view.
         * </summary>
         */
        [field: SerializeField] public bool ShowFrustumEdges { get; set; } = true;

        #endregion

        /**
         * <summary>
         * Writes the current settings to disk.
         * </summary>
         */
        public void Persist()
        {
            Save(true);
        }
    }
}
