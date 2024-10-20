using UnityEngine;

namespace nadena.dev.ndmf.preview
{
    [AddComponentMenu("/")]
    internal class ProxyTagComponent : MonoBehaviour
    {
        internal bool Armed = true;

        private void OnDestroy()
        {
#if NDMF_DEBUG
            if (Armed)
            {
                Debug.LogWarning("Proxy object was destroyed improperly here!");
            }
#endif
        }
    }
}