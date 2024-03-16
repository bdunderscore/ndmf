using UnityEngine;

namespace nadena.dev.ndmf.UnitTestSupport
{
    interface ITestInterface1
    {
    }

    [AddComponentMenu("")]
    internal class PTCInheritanceComponent : MonoBehaviour, ITestInterface1
    {
        
    }
}