using System.Collections.Generic;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Every traced object enabled in the loaded scenes, added to the registry when enabled and removed when disabled.
     * </summary>
     */
    public static class TracedRegistry
    {
        #region Private Fields

        private static readonly List<Sky> SkyList = new List<Sky>();
        private static readonly List<TracedSphere> SphereList = new List<TracedSphere>();

        #endregion

        #region Properties

        public static IReadOnlyList<Sky> Skies => SkyList;
        public static IReadOnlyList<TracedSphere> Spheres => SphereList;

        /**
         * <summary>
         * Dirty flag for the scene registry incremented whenever a traced object is added or removed.
         * </summary>
         */
        public static int Version { get; private set; }

        #endregion

        public static void Register(Sky sky)
        {
            if (!SkyList.Contains(sky))
                SkyList.Add(sky);
        }

        public static void Unregister(Sky sky)
        {
            SkyList.Remove(sky);
        }

        public static void Register(TracedSphere sphere)
        {
            if (!SphereList.Contains(sphere))
                SphereList.Add(sphere);

            MarkChanged();
        }

        public static void Unregister(TracedSphere sphere)
        {
            SphereList.Remove(sphere);
            MarkChanged();
        }

        public static void MarkChanged()
        {
            Version++;
        }
    }
}