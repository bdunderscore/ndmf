using System.Linq;
using UnityEditor;

namespace nadena.dev.ndmf {
    [InitializeOnLoad]
    public class DefineSymbolsManager {
        private const string DefineName = "NDMF";

        static DefineSymbolsManager()
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone).Split(';').ToList();
            if (!defines.Contains(DefineName))
            {
                defines.Add(DefineName);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, string.Join(";", defines));
            }
        }
    }
}
