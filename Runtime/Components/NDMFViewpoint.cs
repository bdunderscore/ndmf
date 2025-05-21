using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.ndmf.runtime.components
{
    
    [AddComponentMenu("NDMF/NDMF Viewpoint")]
    [PublicAPI]
    public class NDMFViewpoint : MonoBehaviour, INDMFEditorOnly, IPortableAvatarConfigTag
    {
        // No configurable properties
    }
}