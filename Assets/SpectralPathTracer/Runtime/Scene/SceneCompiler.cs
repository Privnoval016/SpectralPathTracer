using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Turns the registered scene objects into GPU buffers, rebuilding on scene changes.
     * </summary>
     */
    public class SceneCompiler
    {
        #region Private Fields

        private readonly List<GpuObject> _objectData = new List<GpuObject>();
        private readonly List<GpuMaterial> _materialData = new List<GpuMaterial>();

        /**
         * <summary>
         * Keyed by material asset, resolving to its index in the material table.
         * </summary>
         */
        private readonly Dictionary<SpectralMaterial, int> _materialIndices = new Dictionary<SpectralMaterial, int>();

        private readonly List<Matrix4x4> _cachedTransforms = new List<Matrix4x4>();

        private GraphicsBuffer _objects;
        private GraphicsBuffer _materials;

        private int _builtVersion = -1;
        private int _defaultMaterialIndex = -1;

        #endregion

        #region Properties

        public static Color DefaultAlbedo { get; } = new Color(0.8f, 0.8f, 0.8f);

        public int ObjectCount { get; private set; }

        #endregion

        /**
         * <summary>
         * Rebuilds and uploads the scene buffers if anything changed since the last build.
         * </summary>
         */
        public void Update()
        {
            if (!HasChanged())
                return;

            Rebuild();
            Upload();
            CacheTransforms();
            _builtVersion = TracedRegistry.Version;
        }

        /**
         * <summary>
         * Imports the scene buffers into this frame's render graph.
         * </summary>
         * <param name="renderGraph">The render graph recording this frame.</param>
         * <returns>The scene buffers for this frame.</returns>
         */
        public SceneBuffers Import(RenderGraph renderGraph)
        {
            return new SceneBuffers(renderGraph.ImportBuffer(_objects), renderGraph.ImportBuffer(_materials), ObjectCount);
        }


        public void Dispose()
        {
            _objects?.Release();
            _objects = null;
            _materials?.Release();
            _materials = null;
        }

        #region Helpers

        private bool HasChanged()
        {
            if (_objects == null || _builtVersion != TracedRegistry.Version)
                return true;

            // a moved object changes no version, so compare every transform against the last build
            IReadOnlyList<TracedSphere> spheres = TracedRegistry.Spheres;
            if (spheres.Count != _cachedTransforms.Count)
                return true;

            for (int i = 0; i < spheres.Count; i++)
            {
                if (spheres[i].transform.localToWorldMatrix != _cachedTransforms[i])
                    return true;
            }

            return false;
        }

        private void Rebuild()
        {
            _objectData.Clear();
            _materialData.Clear();
            _materialIndices.Clear();
            _defaultMaterialIndex = -1;

            foreach (TracedSphere sphere in TracedRegistry.Spheres)
            {
                // M^-T Q M^-1 moves the local quadric into world space, scale and rotation included
                Matrix4x4 worldToLocal = sphere.transform.worldToLocalMatrix;
                var gpuObject = new GpuObject
                {
                    Quadric = worldToLocal.transpose * sphere.LocalQuadric * worldToLocal,
                    MaterialIndex = MaterialIndex(sphere.Material)
                };
                _objectData.Add(gpuObject);
            }

            // the material buffer needs to hold at least one record
            if (_materialData.Count == 0)
                MaterialIndex(null);

            ObjectCount = _objectData.Count;
        }

        private int MaterialIndex(SpectralMaterial material)
        {
            if (material == null)
            {
                if (_defaultMaterialIndex < 0)
                    _defaultMaterialIndex = AddMaterial(DefaultAlbedo);

                return _defaultMaterialIndex;
            }

            if (!_materialIndices.TryGetValue(material, out int index))
            {
                index = AddMaterial(material.Albedo);
                _materialIndices.Add(material, index);
            }

            return index;
        }

        private int AddMaterial(Color albedo)
        {
            Color linear = albedo.linear;
            _materialData.Add(new GpuMaterial { Albedo = new Vector3(linear.r, linear.g, linear.b) });
            return _materialData.Count - 1;
        }

        private void Upload()
        {
            EnsureCapacity(ref _objects, _objectData.Count, UnsafeUtility.SizeOf<GpuObject>(), "PathTracerObjects");
            EnsureCapacity(ref _materials, _materialData.Count, UnsafeUtility.SizeOf<GpuMaterial>(), "PathTracerMaterials");

            _objects.SetData(_objectData);
            _materials.SetData(_materialData);
        }

        private void CacheTransforms()
        {
            _cachedTransforms.Clear();
            foreach (TracedSphere sphere in TracedRegistry.Spheres)
                _cachedTransforms.Add(sphere.transform.localToWorldMatrix);
        }

        /**
         * <summary>
         * Makes sure the buffer holds at least <paramref name="count"/> elements, doubling
         * its capacity when it has to grow.
         * </summary>
         * <param name="buffer">The buffer to check.</param>
         * <param name="count">The number of elements needed.</param>
         * <param name="stride">The size of one element in bytes.</param>
         * <param name="name">The buffer's debug name.</param>
         */
        private static void EnsureCapacity(ref GraphicsBuffer buffer, int count, int stride, string name)
        {
            int needed = Mathf.Max(1, count);
            if (buffer != null && buffer.count >= needed)
                return;

            int capacity = buffer == null ? 1 : buffer.count;
            while (capacity < needed)
                capacity *= 2;

            buffer?.Release();
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, stride) { name = name };
        }

        #endregion
    }
}