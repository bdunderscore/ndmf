using UnityEngine;

namespace nadena.dev.ndmf.runtime.components
{
    
#if NDMF_EXPERIMENTAL
    [AddComponentMenu("NDMF/NDMF Viewpoint")]
#else
    [AddComponentMenu("")]
#endif
    //[PublicAPI]
    [NDMFExperimental]
    public class NDMFViewpoint : MonoBehaviour, INDMFEditorOnly, IPortableAvatarConfigTag
    {
        // No configurable properties
    }
}