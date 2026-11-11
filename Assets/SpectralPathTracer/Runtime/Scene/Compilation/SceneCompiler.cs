using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Turns the registered scene objects into GPU buffers, rebuilding on scene changes, and picks the sky.
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

        private readonly GrowableBuffer<GpuObject> _objects = new GrowableBuffer<GpuObject>("PathTracerObjects");
        private readonly GrowableBuffer<GpuMaterial> _materials = new GrowableBuffer<GpuMaterial>("PathTracerMaterials");

        private int _builtVersion = -1;
        private int _defaultMaterialIndex = -1;

        private bool _reportedMultipleSkies;

        #endregion

        #region Properties

        public static Color DefaultAlbedo { get; } = new Color(0.8f, 0.8f, 0.8f);

        public int ObjectCount { get; private set; }
        public SkyColors Sky { get; private set; }

        #endregion

        /**
         * <summary>
         * Picks the sky, then rebuilds and uploads the scene buffers if anything changed since the last build.
         * </summary>
         * <param name="changed">True if the sky or the scene changed this frame.</param>
         * <returns>False if more than one Sky is enabled.</returns>
         */
        public bool TryUpdate(out bool changed)
        {
            changed = false;
            if (!TryResolveSky(out SkyColors sky))
                return false;

            // sky colors change without a version bump, so compare them directly
            changed = sky.Zenith != Sky.Zenith || sky.Horizon != Sky.Horizon || sky.Ground != Sky.Ground;
            Sky = sky;

            if (HasChanged())
            {
                Rebuild();
                Upload();
                CacheTransforms();
                _builtVersion = TracedRegistry.Version;
                changed = true;
            }

            return true;
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
            return new SceneBuffers(_objects.Import(renderGraph), _materials.Import(renderGraph), ObjectCount);
        }


        public void Dispose()
        {
            _objects.Dispose();
            _materials.Dispose();
        }

        #region Helpers

        private bool HasChanged()
        {
            if (!_objects.IsCreated || _builtVersion != TracedRegistry.Version)
                return true;

            // a moved object changes no version, so compare every transform against the last build
            IReadOnlyList<TracedShape> shapes = TracedRegistry.Shapes;
            if (shapes.Count != _cachedTransforms.Count)
                return true;

            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].transform.localToWorldMatrix != _cachedTransforms[i])
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

            foreach (TracedShape shape in TracedRegistry.Shapes)
            {
                // each shape supplies its own matrices, so every record is filled the same way
                var gpuObject = new GpuObject
                {
                    Quadric = shape.WorldQuadric,
                    WorldToLocal = shape.WorldToShape,
                    Type = (int)shape.Type,
                    MaterialIndex = MaterialIndex(shape.Material),
                    LightId = -1, // no light table yet
                    MediumIn = -1,
                    MediumOut = -1
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
                    _defaultMaterialIndex = AddMaterial(DefaultAlbedo, Color.black, 0f);

                return _defaultMaterialIndex;
            }

            if (!_materialIndices.TryGetValue(material, out int index))
            {
                index = AddMaterial(material.Albedo, material.EmissionColor, material.EmissionStrength);
                _materialIndices.Add(material, index);
            }

            return index;
        }

        private int AddMaterial(Color albedo, Color emissionColor, float emissionStrength)
        {
            Color linearAlbedo = albedo.linear;
            Color emission = emissionColor.linear * emissionStrength;
            _materialData.Add(new GpuMaterial
            {
                Albedo = new Vector3(linearAlbedo.r, linearAlbedo.g, linearAlbedo.b),
                Emission = new Vector3(emission.r, emission.g, emission.b),
                Type = (int)MaterialType.Lambert
            });
            return _materialData.Count - 1;
        }

        private void Upload()
        {
            _objects.Upload(_objectData);
            _materials.Upload(_materialData);
        }

        /**
        * <summary>
        * Picks this frame's sky.
        * </summary>
        * <param name="sky">The sky to render with.</param>
        * <returns>False if more than one Sky is enabled.</returns>
        */
        private bool TryResolveSky(out SkyColors sky)
        {
            IReadOnlyList<Sky> skies = TracedRegistry.Skies;

            if (skies.Count > 1)
            {
                if (!_reportedMultipleSkies)
                    Debug.LogError($"Path tracer: {skies.Count} Sky components are enabled; disable all but one.");

                _reportedMultipleSkies = true;
                sky = default;
                return false;
            }

            _reportedMultipleSkies = false;
            sky = skies.Count == 1 ? skies[0].Colors : SkyColors.Default;
            return true;
        }

        private void CacheTransforms()
        {
            _cachedTransforms.Clear();
            foreach (TracedShape shape in TracedRegistry.Shapes)
                _cachedTransforms.Add(shape.transform.localToWorldMatrix);
        }

        #endregion
    }
}