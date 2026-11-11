using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Repaints the Game view in Edit Mode while samples are still accumulating.
     * </summary>
     */
    [InitializeOnLoad]
    public static class AccumulationRepaint
    {
        #region Private Fields

        // the Game view's class is internal to the Editor, so it can only be found by name
        private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

        #endregion

        static AccumulationRepaint()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            // Play Mode renders every frame on its own
            if (Application.isPlaying || !AccumulationProgress.IsAccumulating || GameViewType == null)
                return;

            // QueuePlayerLoopUpdate doesn't redraw the Game view
            foreach (Object view in Resources.FindObjectsOfTypeAll(GameViewType))
                ((EditorWindow)view).Repaint();
        }
    }
}