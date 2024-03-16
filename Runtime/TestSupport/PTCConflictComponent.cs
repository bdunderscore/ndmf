using UnityEngine;

namespace nadena.dev.ndmf.UnitTestSupport
{
    interface ITestInterface2
    {
    }

    [AddComponentMenu("")]
    internal class PTCConflictComponent : MonoBehaviour, ITestInterface1, ITestInterface2
    {
        
    }
}