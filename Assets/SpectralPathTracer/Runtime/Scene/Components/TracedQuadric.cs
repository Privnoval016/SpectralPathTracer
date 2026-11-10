using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * A shape described by a quadric in its local space, moved to world space by its transform.
     * </summary>
     */
    public abstract class TracedQuadric : TracedShape
    {
        #region Properties

        public override ShapeType Type => ShapeType.Quadric;

        /**
         * <summary>
         * The shape's quadric in its own local space.
         * </summary>
         */
        public abstract Matrix4x4 LocalQuadric { get; }

        public override Matrix4x4 WorldQuadric
        {
            get
            {
                // M^-T Q M^-1 moves the local quadric into world space, scale and rotation included
                Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
                return worldToLocal.transpose * LocalQuadric * worldToLocal;
            }
        }

        #endregion
    }
}