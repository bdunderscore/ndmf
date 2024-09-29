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
            RuntimeUtil.DelayCall = call => { EditorApplication.delayCall += () => call(); };
        }
    }
}