#region

using nadena.dev.ndmf.runtime;
using UnityEditor;

#endregion

namespace nadena.dev.ndmf
{
    [InitializeOnLoad]
    internal static class GlobalInit
    {
        static GlobalInit()
        {
            RuntimeUtil.delayCall = call => { EditorApplication.delayCall += () => call(); };
        }
    }
}