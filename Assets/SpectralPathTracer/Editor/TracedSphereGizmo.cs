using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Draws each traced sphere as a wire sphere in the Scene view, tinted by its material's albedo.
     * </summary>
     */
    public static class TracedSphereGizmo
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void Draw(TracedSphere sphere, GizmoType gizmoType)
        {
            // the transform's matrix makes non-uniform scale show up as the same ellipsoid the tracer sees
            Gizmos.matrix = sphere.transform.localToWorldMatrix;
            Gizmos.color = sphere.Material != null ? sphere.Material.Albedo : SceneCompiler.DefaultAlbedo;
            Gizmos.DrawWireSphere(Vector3.zero, sphere.Radius);
        }
    }
}