using UnityEditor;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Per-user switch for making the Main Camera follow the Scene view in Edit Mode.
     * </summary>
     */
    [FilePath("UserSettings/SpectralPathTracer/FollowSceneViewSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class FollowSceneViewSettings : ScriptableSingleton<FollowSceneViewSettings>
    {
        #region Properties

        [field: SerializeField] public bool Follow { get; set; }

        #endregion

        public void SaveSettings()
        {
            Save(true);
        }
    }
}