#region

using System;
using UnityEditor;
using UnityEngine;

#endregion

/// <summary>
/// This component will self destruct one frame after load, unless the KeepAlive field is set to a live object.
/// </summary>
[AddComponentMenu("")]
public class SelfDestructComponent : MonoBehaviour
{
    [NonSerialized] public object KeepAlive; // don't destroy when non-null (non-serialized field)

    void OnValidate()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall += () =>
        {
            if (this != null && KeepAlive == null)
            {
                DestroyImmediate(gameObject);
            }
        };
#endif
    }
}