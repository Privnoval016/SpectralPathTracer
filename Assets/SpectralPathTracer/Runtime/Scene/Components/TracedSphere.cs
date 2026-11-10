using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * An analytic sphere the path tracer intersects.
     * </summary>
     */
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TracedSphere : TracedQuadric
    {
        #region Inspector Fields

        [field: Tooltip("Radius in the GameObject's local space.")]
        [field: Min(0.0001f)]
        [field: SerializeField] public float Radius { get; private set; } = 0.5f;

        #endregion

        #region Properties

        /**
         * <summary>
         * The sphere as a quadric in its own local space: x^2 + y^2 + z^2 - r^2 = 0.
         * </summary>
         */
        public override Matrix4x4 LocalQuadric
        {
            get
            {
                Matrix4x4 q = Matrix4x4.identity;
                q.m33 = -Radius * Radius;
                return q;
            }
        }

        #endregion
    }
}