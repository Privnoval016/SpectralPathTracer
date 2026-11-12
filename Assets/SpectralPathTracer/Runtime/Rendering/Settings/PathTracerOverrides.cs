namespace SpectralPathTracer
{
    public static class PathTracerOverrides
    {
        #region Private Fields

        private static bool _restartRequested;

        #endregion

        #region Properties

        public static int? TargetSamples { get; set; }
        public static float? FrameBudgetMs { get; set; }
        public static float? SplitPosition { get; set; }
        public static float? LockedWavelength { get; set; } // nm, which locks the wavelength while set
        public static bool? Spectral { get; set; } // a tool forcing one mode also turns split screen off

        #endregion

        public static void Clear()
        {
            TargetSamples = null;
            FrameBudgetMs = null;
            SplitPosition = null;
            LockedWavelength = null;
            Spectral = null;
        }

        /**
         * <summary>
         * Starts accumulation over on the next frame the renderer records.
         * </summary>
         */
        public static void RequestRestart()
        {
            _restartRequested = true;
        }

        /**
         * <summary>
         * Reads and clears the restart request.
         * </summary>
         * <returns>True once for each request.</returns>
         */
        internal static bool TakeRestart()
        {
            bool requested = _restartRequested;
            _restartRequested = false;
            return requested;
        }
    }
}