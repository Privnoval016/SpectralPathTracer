using UnityEngine;
using UnityEngine.Playables;

namespace SpectralPathTracer
{
    [DisallowMultipleComponent]
    public class MovieMode : MonoBehaviour
    {
        #region Private Fields

        private int _frame;
        private int _stepReport;

        private bool _wasRunInBackground;

        #endregion

        #region Inspector Fields

        [field: Tooltip("The Timeline that animates the scene.")]
        [field: SerializeField] public PlayableDirector Director { get; private set; }


        [field: Tooltip("Movie frames per second of Timeline time.")]
        [field: Min(1)]
        [field: SerializeField] public int FrameRate { get; private set; } = 30;

        [field: Tooltip("Samples per pixel in every saved frame.")]
        [field: Min(1)]
        [field: SerializeField] public int SamplesPerFrame { get; private set; } = 256;

        [field: Tooltip("GPU target time per Unity frame while the movie renders, in milliseconds.")]
        [field: Min(1f)]
        [field: SerializeField] public float FrameBudgetMs { get; private set; } = 250f;

        #endregion

        #region Properties

        public int FrameCount => Mathf.FloorToInt((float)Director.duration * FrameRate) + 1;

        #endregion

        private void OnEnable()
        {
            _wasRunInBackground = Application.runInBackground;
            Application.runInBackground = true;

            PathTracerOverrides.TargetSamples = SamplesPerFrame;
            PathTracerOverrides.FrameBudgetMs = FrameBudgetMs;

            Director.timeUpdateMode = DirectorUpdateMode.Manual;
            _frame = 0;


            StepTo(_frame);
        }

        private void OnDisable()
        {
            Application.runInBackground = _wasRunInBackground;
            PathTracerOverrides.Clear();
        }

        private void Update()
        {
            // a report from before the step will still describe the last movie frame
            if (AccumulationProgress.Reports == _stepReport || AccumulationProgress.Samples < SamplesPerFrame)
                return;

            Debug.Log($"Movie frame {_frame + 1} of {FrameCount} reached {AccumulationProgress.Samples} samples");

            _frame++;
            if (_frame >= FrameCount)
            {
                enabled = false;
                return;
            }
            

            StepTo(_frame);
        }

        /**
         * <summary>
         * Moves the Timeline to a movie frame and applies it to the scene.
         * </summary>
         * <param name="frame">The movie frame to show.</param>
         */
        private void StepTo(int frame)
        {
            Director.time = (double)frame / FrameRate;
            Director.Evaluate();
            PathTracerOverrides.RequestRestart();
            _stepReport = AccumulationProgress.Reports;
        }
    }
}