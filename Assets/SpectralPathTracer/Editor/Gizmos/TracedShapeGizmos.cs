using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Draws each traced shape as a wireframe in the Scene view, tinted by its material's albedo.
     * </summary>
     */
    public static class TracedShapeGizmos
    {
        #region Private Fields

        private const float PlaneGizmoSize = 10f;

        #endregion

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawSphere(TracedSphere sphere, GizmoType gizmoType)
        {
            // the transform's matrix makes non-uniform scale show up as the same ellipsoid the tracer sees
            Gizmos.matrix = sphere.transform.localToWorldMatrix;
            Gizmos.color = ShapeColor(sphere);
            Gizmos.DrawWireSphere(Vector3.zero, sphere.Radius);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawQuad(TracedQuad quad, GizmoType gizmoType)
        {
            Gizmos.matrix = quad.transform.localToWorldMatrix;
            Gizmos.color = ShapeColor(quad);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(quad.Size.x, quad.Size.y, 0f));
            Gizmos.DrawLine(Vector3.zero, Vector3.back * 0.25f); // the front side, local -z
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawPlane(TracedPlane plane, GizmoType gizmoType)
        {
            Gizmos.matrix = plane.transform.localToWorldMatrix;
            Gizmos.color = ShapeColor(plane);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(PlaneGizmoSize, 0f, PlaneGizmoSize));
            Gizmos.DrawLine(Vector3.zero, Vector3.up);
        }

        #region Helpers

        private static Color ShapeColor(TracedShape shape)
        {
            return shape.Material != null ? shape.Material.Albedo : SceneCompiler.DefaultAlbedo;
        }

        #endregion
    }
}