using UnityEditor;

namespace nadena.dev.ndmf.preview
{
    [FilePath("nadena.dev.ndmf/NDMFPreviewPrefs.asset", FilePathAttribute.Location.ProjectFolder)]
    public class NDMFPreviewPrefs : ScriptableSingleton<NDMFPreviewPrefs>
    {
        public bool EnablePreview = true;
    }
}