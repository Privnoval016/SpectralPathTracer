using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * An infinite plane through the GameObject's position, facing its local +y like Unity's Plane.
     * </summary>
     */
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TracedPlane : TracedQuadric
    {
        #region Properties

        /**
         * <summary>
         * The plane y = 0 as a quadric: x^T Q x = y.
         * </summary>
         */
        public override Matrix4x4 LocalQuadric
        {
            get
            {
                Matrix4x4 q = Matrix4x4.zero;
                q.m13 = 0.5f; // the two halves add up to the linear term y
                q.m31 = 0.5f;
                return q;
            }
        }

        #endregion
    }
}