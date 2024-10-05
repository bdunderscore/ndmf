using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.config.runtime
{
    internal class NonPersistentConfig
        #if UNITY_EDITOR
        : ScriptableSingleton<NonPersistentConfig>
        #endif
    {
        [SerializeField] public bool applyOnPlay = true;
        [SerializeField] public bool applyOnBuild = true;
    }
}