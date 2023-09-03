using System;
using UnityEngine;

namespace nadena.dev.ndmf.runtime
{
    [AddComponentMenu("")]
    internal class AlreadyProcessedTag : MonoBehaviour
    {
        private void OnValidate()
        {
            hideFlags = HideFlags.HideAndDontSave;
        }
    }
}