using UnityEngine;

namespace nadena.dev.ndmf.config.runtime
{
    internal class NonPersistentConfig
        #if UNITY_EDITOR
        : UnityEditor.ScriptableSingleton<NonPersistentConfig>
        #endif
    {
        [SerializeField] public bool applyOnPlay = true;
    }
}