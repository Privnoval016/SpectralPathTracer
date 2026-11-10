using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * A flat rectangle centered on the GameObject in its local XY plane, facing local -z like Unity's Quad.
     * </summary>
     */
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TracedQuad : TracedShape
    {
        #region Inspector Fields

        [field: Tooltip("Width and height in the GameObject's local space.")]
        [field: Min(0.0001f)]
        [field: SerializeField] public Vector2 Size { get; private set; } = Vector2.one;

        #endregion

        #region Properties

        public override ShapeType Type => ShapeType.Quad;

        public override Matrix4x4 WorldToShape
        {
            get
            {
                // divide by the size after worldToLocal, so the GPU always tests the unit square
                Matrix4x4 sizeToUnit = Matrix4x4.Scale(new Vector3(1f / Size.x, 1f / Size.y, 1f));
                return sizeToUnit * transform.worldToLocalMatrix;
            }
        }

        #endregion
    }
}