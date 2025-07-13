#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.multiplatform.components;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace nadena.dev.ndmf.multiplatform.editor
{
    internal static class VRChatPlatformExtensions
    {
        // VRChat's GetOrAddComponent shadows NDMF's with an [Obsolete] method, so we need a different name...
        private static T GetOrMatchComponent<T>(this GameObject go, Predicate<T> match) where T : Component
        {
            foreach (var component in go.GetComponents<T>())
            {
                if (match(component))
                {
                    return component;
                }
            }
            
            return go.AddComponent<T>();
        }
        
        [InitializeOnLoadMethod]
        private static void Init()
        {
            nadena.dev.ndmf.vrchat.VRChatPlatform.GeneratePortableComponentsImpl = GeneratePortableComponents;
        }

        private static void GeneratePortableComponents(GameObject root, bool useUndo)
        {
            Dictionary<VRCPhysBoneCollider, PortableDynamicBoneCollider> colliders = new();
            Dictionary<Transform, PortableDynamicBone> explicitDynBones = new();

            foreach (var pdb in root.GetComponentsInChildren<PortableDynamicBone>(true))
            {
                if (pdb.Root != null) explicitDynBones[pdb.Root] = pdb;
            }
            
            foreach (var pbc in root.GetComponentsInChildren<VRCPhysBoneCollider>(true))
            {
                var portable = pbc.gameObject.AddComponent<PortableDynamicBoneCollider>();
                switch (pbc.shapeType)
                {
                    case VRCPhysBoneColliderBase.ShapeType.Capsule:
                        portable.ColliderType = PortableDynamicColliderType.Capsule;
                        break;
                    case VRCPhysBoneColliderBase.ShapeType.Sphere:
                        portable.ColliderType = PortableDynamicColliderType.Sphere;
                        break;
                    case VRCPhysBoneColliderBase.ShapeType.Plane:
                        portable.ColliderType = PortableDynamicColliderType.Plane;
                        break;
                    default:
                        // unknown type
                        Debug.Log("Unknown collider type " + pbc.shapeType);
                        UnityEngine.Object.DestroyImmediate(portable);
                        continue;
                }

                portable.Root = pbc.rootTransform;
                portable.Radius = pbc.radius;
                portable.Height = pbc.height;
                portable.PositionOffset = pbc.position;
                portable.RotationOffset = pbc.rotation;
                portable.InsideBounds = pbc.insideBounds;

                colliders[pbc] = portable;
            }
            
            foreach (var pb in root.GetComponentsInChildren<VRCPhysBone>())
            {
                var rootBone = pb.rootTransform ?? pb.transform;
                var portable = explicitDynBones.GetValueOrDefault(rootBone) ??
                               pb.gameObject.AddComponent<PortableDynamicBone>();

                portable.enabled = pb.enabled;
                portable.BaseRadius.WeakSet(pb.radius);
                portable.IsGrabbable.WeakSet(pb.allowGrabbing == VRCPhysBoneBase.AdvancedBool.True);
                portable.IgnoreSelf.WeakSet(false);
                portable.IgnoreTransforms.WeakSet(pb.ignoreTransforms.ToList());

                portable.Root = rootBone;
                portable.Colliders.WeakSet((pb.colliders ?? new())
                    .OfType<VRCPhysBoneCollider>()
                    .Select(c => colliders.GetValueOrDefault(c))
                    .Where(c => c != null)
                    .ToList());
                portable.IgnoreMultiChild.WeakSet(pb.multiChildType == VRCPhysBoneBase.MultiChildType.Ignore);

                if (pb.radiusCurve != null)
                {
                    var radiusCurve = new AnimationCurve(pb.radiusCurve.keys);
                    // We don't currently support endpoint bones, so try to correct the animation curve if they were present...
                    if (pb.endpointPosition.sqrMagnitude > 0 && rootBone != null)
                    {
                        var maxBoneDepth =
                            (float)GetMaxBoneDepth(rootBone, new HashSet<Transform>(pb.ignoreTransforms));
                        for (int i = 0; i < radiusCurve.keys.Length; i++)
                        {
                            radiusCurve.keys[i].time =
                                radiusCurve.keys[i].time * (maxBoneDepth) / (maxBoneDepth + 1);
                        }
                    }

                    portable.RadiusCurve.WeakSet(radiusCurve);
                }
            }
        }

        private static int GetMaxBoneDepth(Transform pbRootTransform, HashSet<Transform> ignores)
        {
            int max = 0;

            foreach (Transform t in pbRootTransform)
            {
                if (ignores.Contains(t)) continue;
                max = Math.Max(max, GetMaxBoneDepth(t, ignores));
            }

            return max + 1;
        }
    }
}