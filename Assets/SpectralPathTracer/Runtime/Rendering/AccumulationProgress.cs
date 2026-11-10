namespace SpectralPathTracer
{
    /**
     * <summary>
     * How far accumulation has got, reported by the pass every frame and read by Editor tools (the repaint loop and
     * the settings Inspector). Data only, like TracedRegistry.
     * </summary>
     */
    public static class AccumulationProgress
    {
        #region Properties

        public static int Samples { get; private set; }
        public static int TargetSamples { get; private set; }
        public static bool IsAccumulating { get; private set; }

        #endregion

        internal static void Report(int samples, int targetSamples, bool isAccumulating)
        {
            Samples = samples;
            TargetSamples = targetSamples;
            IsAccumulating = isAccumulating;
        }
    }
}