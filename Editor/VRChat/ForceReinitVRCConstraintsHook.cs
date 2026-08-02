#if NDMF_VRCSDK3_AVATARS_VRC_CONSTRAINTS
#region

using System;
using System.Reflection;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDKBase.Editor.BuildPipeline;

#endregion

namespace nadena.dev.ndmf.VRChat
{
    internal class ForceReinitVRCConstraintsHook : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => int.MaxValue;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (Application.isPlaying)
            {
                var awake = typeof(VRCConstraintBase).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                var isRuntimeTargetTransformAssigned = typeof(VRCConstraintBase).GetField("_isRuntimeTargetTransformAssigned", BindingFlags.NonPublic | BindingFlags.Instance);
                if (awake != null && isRuntimeTargetTransformAssigned != null)
                {
                    foreach (var collider in avatarGameObject.GetComponentsInChildren<VRCConstraintBase>(true))
                    {
                        isRuntimeTargetTransformAssigned.SetValue(collider, false);
                        awake.Invoke(collider, null);
                    }
                }
                else
                {
                    Debug.LogError("VRCConstraintBase.Awake() or _isRuntimeTargetTransformAssigned couldn't find. skipping re-initializing VRCConstraint");
                }
                VRCDynamicsScheduler.UpdateConstraints(true);
            }

            return true;
        }
    }
}

#endif
