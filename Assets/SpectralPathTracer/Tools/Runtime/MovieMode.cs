using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
        private bool _capturing;
        private string _folder;

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

        [field: Tooltip("Folder for the frames, relative to the project root; each Timeline gets its own subfolder.")]
        [field: SerializeField] public string OutputFolder { get; private set; } = "Recordings";

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
            _capturing = false;

            _folder = Path.Combine(OutputFolder, Director.playableAsset.name);
            Directory.CreateDirectory(_folder);


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
            if (_capturing || AccumulationProgress.Reports == _stepReport || AccumulationProgress.Samples < SamplesPerFrame)
                return;

            _capturing = true;
            DisplayCapture.Request(OnCaptured);
        }

        /**
         * <summary>
         * Saves a finished movie frame and steps to the next one.
         * </summary>
         * <param name="pixels">The frame's sRGB pixels, bottom row first.</param>
         * <param name="size">The frame's size in pixels.</param>
         */
        private void OnCaptured(Color32[] pixels, Vector2Int size)
        {
            // a capture can still land after the movie was stopped
            if (!enabled)
                return;

            byte[] png = ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)size.x, (uint)size.y);
            File.WriteAllBytes(Path.Combine(_folder, $"frame_{_frame:D4}.png"), png);
            Debug.Log($"Movie frame {_frame + 1} of {FrameCount} saved");

            _capturing = false;
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