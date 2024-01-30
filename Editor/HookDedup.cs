using System.Runtime.CompilerServices;
using UnityEngine;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// Ensures that we don't run hooks more than once. This is in preparation for coordinating with VRCFury to have all
    /// nondestructive utilities independently execute hooks as part of Apply on Play, while deduplicating to ensure that
    /// we don't rerun hooks on the same avatar.
    /// </summary>
    internal static class HookDedup
    {
        internal class State
        {
            internal bool ranEarlyHook;
            internal bool ranOptimization;
        }

        private static ConditionalWeakTable<GameObject, State> _avatars = new ConditionalWeakTable<GameObject, State>();
        
        public static State RecordAvatar(GameObject root)
        {
            if (_avatars.TryGetValue(root, out var state))
            {
                return state;
            }

            state = new State();
            _avatars.Add(root, state);

            return state;
        }

        public static bool HasAvatar(GameObject root)
        {
            return _avatars.TryGetValue(root, out _);
        }
    }
}