using System;
using UnityEngine;

namespace nadena.dev.build_framework.runtime
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