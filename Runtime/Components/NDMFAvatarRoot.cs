using JetBrains.Annotations;
using nadena.dev.ndmf;
using UnityEngine;

namespace nadena.dev.ndmf.runtime.components
{
    #if NDMF_EXPERIMENTAL
    [AddComponentMenu("NDMF/NDMF Avatar Root")]
    #else
    [AddComponentMenu("")]
    #endif
    // TODO
    //[PublicAPI]
    [NDMFExperimental]
    internal class NDMFAvatarRoot : MonoBehaviour, INDMFEditorOnly
    {
        
    }
}