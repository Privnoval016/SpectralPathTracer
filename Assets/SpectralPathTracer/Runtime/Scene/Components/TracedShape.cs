using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * A shape the path tracer intersects. Registers itself with the scene while enabled.
     * </summary>
     */
    public abstract class TracedShape : MonoBehaviour
    {
        #region Inspector Fields

        [field: Tooltip("Surface material.")]
        [field: SerializeField] public SpectralMaterial Material { get; private set; }

        #endregion

        #region Properties

        public abstract ShapeType Type { get; }

        /**
         * <summary>
         * The shape's quadric in world space, or zero for shapes that aren't quadrics.
         * </summary>
         */
        public virtual Matrix4x4 WorldQuadric => Matrix4x4.zero;

        /**
         * <summary>
         * Maps world space into the space where the GPU intersects the shape's canonical form, such as the unit
         * square for a quad.
         * </summary>
         */
        public virtual Matrix4x4 WorldToShape => transform.worldToLocalMatrix;

        #endregion

        protected virtual void OnEnable()
        {
            TracedRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            TracedRegistry.Unregister(this);
        }

        protected virtual void OnValidate()
        {
            TracedRegistry.MarkChanged();
        }
    }
}