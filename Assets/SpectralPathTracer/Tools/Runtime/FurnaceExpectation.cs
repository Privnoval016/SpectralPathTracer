using UnityEngine;

namespace SpectralPathTracer
{
    [DisallowMultipleComponent]
    public class FurnaceExpectation : MonoBehaviour
    {
        #region Inspector Fields

        [field: Tooltip("The radiance the furnace should read, for every pixel or for the mean.")]
        [field: SerializeField] public float Expected { get; private set; } = 1f;

        [field: Tooltip("How far a reading may stray from the expected value.")]
        [field: Min(0f)]
        [field: SerializeField] public float Tolerance { get; private set; } = 1e-4f;

        [field: Tooltip("Whether every pixel must match, for setups without noise, or only the mean.")]
        [field: SerializeField] public bool EveryPixel { get; private set; } = true;

        #endregion
    }
}
