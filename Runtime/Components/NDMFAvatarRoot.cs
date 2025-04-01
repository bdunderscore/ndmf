using JetBrains.Annotations;
using nadena.dev.ndmf;
using UnityEngine;

namespace nadena.dev.ndmf.runtime.components
{
    #if NDMF_MULTIPLATFORM
    [AddComponentMenu("NDMF/NDMF Avatar Root")]
    #else
    [AddComponentMenu("")]
    #endif
    // TODO
    //[PublicAPI]
    internal class NDMFAvatarRoot : MonoBehaviour, INDMFEditorOnly
    {
        
    }
}