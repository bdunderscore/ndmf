using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.config.runtime
{
    internal class NonPersistentConfig : ScriptableSingleton<NonPersistentConfig>
    {
        [SerializeField] public bool applyOnPlay = true;
    }
}