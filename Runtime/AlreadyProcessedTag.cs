using System;
using UnityEngine;

namespace nadena.dev.ndmf.runtime
{
    [AddComponentMenu("")] [NDMFInternal]
    internal class AlreadyProcessedTag : MonoBehaviour
    {
        // VRCF creates this tag via reflection, but we're not actually done processing yet.
        // We add this boolean so we can ignore any tags created surrepitiously by VRCF...
        internal bool processingCompleted;
        
        private void OnValidate()
        {
            hideFlags = HideFlags.HideAndDontSave;
        }
    }
}