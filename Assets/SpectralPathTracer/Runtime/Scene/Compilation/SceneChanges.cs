using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Stores what the last scene build saw so the compiler rebuilds only when something changed.
     * </summary>
     */
    public class SceneChanges
    {
        #region Private Fields

        private readonly List<Matrix4x4> _transforms = new List<Matrix4x4>();
        private readonly List<EmitterState?> _emitters = new List<EmitterState?>();

        private int _builtVersion = -1;

        #endregion

        /**
         * <summary>
         * Compares the registered shapes against the last build.
         * </summary>
         * <returns>True if a shape was added, removed or edited, moved, or had its emitter changed.</returns>
         */
        public bool HasChanged()
        {
            if (_builtVersion != TracedRegistry.Version)
                return true;

            // a moved object changes no version, so compare every transform against the last build
            IReadOnlyList<TracedShape> shapes = TracedRegistry.Shapes;
            if (shapes.Count != _transforms.Count)
                return true;

            for (int i = 0; i < shapes.Count; i++)
            {
                if (!shapes[i].transform.localToWorldMatrix.Equals(_transforms[i])) // equals
                    return true;

                if (!Nullable.Equals(StateOf(shapes[i]), _emitters[i]))
                    return true;
            }

            return false;
        }

        /**
         * <summary>
         * Records the registered shapes as the last build.
         * </summary>
         */
        public void Remember()
        {
            _transforms.Clear();
            _emitters.Clear();

            IReadOnlyList<TracedShape> shapes = TracedRegistry.Shapes;
            for (int i = 0; i < shapes.Count; i++)
            {
                _transforms.Add(shapes[i].transform.localToWorldMatrix);
                _emitters.Add(StateOf(shapes[i]));
            }

            _builtVersion = TracedRegistry.Version;
        }

        #region Helpers

        /**
         * <summary>
         * Returns the state of a shape's emitter, if any, and only if it is enabled.
         * </summary>
         * <param name="shape">The shape.</param>
         * <returns>The emitter's state, or null.</returns>
         */
        private static EmitterState? StateOf(TracedShape shape)
        {
            return Emitter.On(shape)?.State;
        }

        #endregion
    }
}
