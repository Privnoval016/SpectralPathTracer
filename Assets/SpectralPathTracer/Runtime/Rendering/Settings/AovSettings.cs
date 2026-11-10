using UnityEngine;

namespace SpectralPathTracer
{
    [CreateAssetMenu(fileName = "AovSettings", menuName = "Spectral Path Tracer/AOV Settings")]
    public class AovSettings : ScriptableObject
    {
        #region Inspector Fields

        [field: Tooltip("What the image shows instead of the rendered result.")]
        [field: SerializeField] public Aov Aov { get; private set; } = Aov.None;

        [Header("Depth")]

        [field: Tooltip("Planar depth shown as black.")]
        [field: SerializeField] public float DepthNear { get; private set; } = 0f;

        [field: Tooltip("Planar depth shown as white.")]
        [field: SerializeField] public float DepthFar { get; private set; } = 20f;

        #endregion
    }
}