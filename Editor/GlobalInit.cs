using nadena.dev.ndmf.runtime;
using UnityEditor;

namespace DefaultNamespace
{
    [InitializeOnLoad]
    public static class GlobalInit
    {
        static GlobalInit()
        {
            RuntimeUtil.delayCall = call => { EditorApplication.delayCall += () => call(); };
        }
    }
}