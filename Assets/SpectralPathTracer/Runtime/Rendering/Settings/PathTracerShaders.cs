using UnityEngine;

namespace SpectralPathTracer
{
    [CreateAssetMenu(fileName = "PathTracerShaders", menuName = "Spectral Path Tracer/Path Tracer Shaders")]
    public class PathTracerShaders : ScriptableObject
    {
        #region Inspector Fields

        [field: Tooltip("Writes one camera ray per pixel.")]
        [field: SerializeField] public ComputeShader Generate { get; private set; }

        [field: Tooltip("Finds where each ray hits the scene.")]
        [field: SerializeField] public ComputeShader Intersect { get; private set; }

        [field: Tooltip("Turns each hit into light and the next bounce.")]
        [field: SerializeField] public ComputeShader Shade { get; private set; }

        [field: Tooltip("Writes each path's gathered light to its pixel.")]
        [field: SerializeField] public ComputeShader Resolve { get; private set; }

        [field: Tooltip("Sizes each bounce's dispatch from the queue of live paths.")]
        [field: SerializeField] public ComputeShader Args { get; private set; }

        [field: Tooltip("Turns linear radiance into displayable values.")]
        [field: SerializeField] public ComputeShader Display { get; private set; }

        #endregion

        #region Properties

        public bool IsComplete => Generate != null && Intersect != null && Shade != null && Resolve != null && Display != null && Args != null;

        #endregion
    }
}