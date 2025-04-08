#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.multiplatform.components;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace nadena.dev.ndmf.multiplatform.editor
{
    internal static class VRChatPlatformExtensions
    {
        [InitializeOnLoadMethod]
        private static void Init()
        {
            nadena.dev.ndmf.vrchat.VRChatPlatform.GeneratePortableComponentsImpl = GeneratePortableComponents;
        }

        private static void GeneratePortableComponents(GameObject root, bool useUndo)
        {
            Dictionary<VRCPhysBoneCollider, PortableDynamicBoneCollider> colliders = new();
            
            foreach (var pbc in root.GetComponentsInChildren<VRCPhysBoneCollider>())
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
                portable.OriginalComponent = pbc;
                portable.Radius = pbc.radius;
                portable.Height = pbc.height;
                portable.PositionOffset = pbc.position;
                portable.RotationOffset = pbc.rotation;

                colliders[pbc] = portable;
            }
            
            foreach (var pb in root.GetComponentsInChildren<VRCPhysBone>())
            {
                var portable = pb.gameObject.AddComponent<PortableDynamicBone>();
                portable.IsWeak = true;
                portable.TemplateName.WeakSet(PortableDynamicBone.GuessTemplateName(pb, pb.rootTransform != null ? pb.rootTransform : pb.transform));
                portable.OriginalComponent = pb;
                portable.BaseRadius.WeakSet(pb.radius);
                portable.IsGrabbable.WeakSet(pb.allowGrabbing == VRCPhysBoneBase.AdvancedBool.True);
                portable.IgnoreSelf.WeakSet(false);
                portable.IgnoreTransforms.WeakSet(pb.ignoreTransforms.ToList());

                portable.Root.WeakSet(pb.rootTransform);
                portable.Colliders.WeakSet(pb.colliders
                    .OfType<VRCPhysBoneCollider>()
                    .Select(c => colliders.GetValueOrDefault(c))
                    .Where(c => c != null)
                    .ToList());
            }
        }
    }
}