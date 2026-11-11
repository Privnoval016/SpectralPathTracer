using UnityEngine;

namespace SpectralPathTracer
{
    public class WaveScheduler
    {
        private const float Smoothing = 0.1f; // share of each new measurement's contribution to the average
        private const int MaxWavesPerFrame = 16;

        private double _lastTime = -1.0;
        private int _lastWaves;

        public int Cursor { get; private set; } = 0; // the next wave of the current sample
        public float MsPerWave { get; private set; } = 0f; // the average time per wave in milliseconds
        public int WavesPerFrame => _lastWaves;


        public void Restart() => Cursor = 0;

        public void Idle() => _lastTime = -1.0;



        /**
         * <summary>
         * Plans how many waves to dispatch this frame.
         * </summary>
         * <param name="budgetMs">The time budget in milliseconds, or 0 for no budget.</param>
         * <param name="wavesPerSample">The number of waves in a sample.</param>
         * <returns>The number of waves to dispatch this frame.</returns>
         */
        public int Plan(float budgetMs, int wavesPerSample)
        {
            double now = Time.realtimeSinceStartupAsDouble;

            if (_lastTime >= 0.0 && _lastWaves > 0)
            {
                double elapsed = now - _lastTime;
                float msPerWave = (float)(elapsed * 1000.0 / _lastWaves);
                MsPerWave = MsPerWave <= 0f ? msPerWave : Mathf.Lerp(MsPerWave, msPerWave, Smoothing);
            }

            _lastTime = now;

            int waves = wavesPerSample;

            if (budgetMs > 0f)
            {
                int fit = MsPerWave > 0f ? Mathf.FloorToInt(budgetMs / MsPerWave) : 1; // how many waves fit in the budget
                int cap = Mathf.Min(MaxWavesPerFrame, Mathf.Max(1, _lastWaves * 2)); // how many waves are available to dispatch this frame based on max usage

                waves = Mathf.Clamp(fit, 1, cap);
            }

            _lastWaves = waves;
            return waves;
        }


        /**
         * <summary>
         * Advances the cursor to the next wave of the current sample with wraparound.
         * </summary>
         * <param name="wavesPerSample">The number of waves in a sample.</param>
         * <returns>True if the cursor wrapped back to 0.</returns>
         */
        public bool Advance(int wavesPerSample)
        {
            Cursor = (Cursor + 1) % wavesPerSample;
            return Cursor == 0;
        }
    }
}
