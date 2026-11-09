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

        #endregion

        #region Properties

        public static IReadOnlyList<Sky> Skies => SkyList;

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
    }
}
