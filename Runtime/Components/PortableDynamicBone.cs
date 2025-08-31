#nullable enable

using System.Collections.Generic;
using JetBrains.Annotations;
using nadena.dev.ndmf.runtime;
using UnityEngine;

namespace nadena.dev.ndmf.multiplatform.components
{
    [AddComponentMenu("NDMF/Portable/NDMF Portable Dynamic Bone")]
    [PublicAPI]
    public class PortableDynamicBone : MonoBehaviour, INDMFEditorOnly
    {
        [SerializeField] private Transform? m_root;
        [SerializeField] private OverrideProperty<string> m_templateName = new();
        [SerializeField] private OverrideProperty<float> m_baseRadius = new();
        [SerializeField] private OverrideProperty<List<Transform>> m_ignoreTransforms = new();
        [SerializeField] private OverrideProperty<bool> m_isGrabbable = new(), m_ignoreSelf = new();
        [SerializeField] private OverrideProperty<List<PortableDynamicBoneCollider>> m_colliders = new();
        [SerializeField] private OverrideProperty<bool> m_ignoreMultiChild = new();
        [SerializeField] private OverrideProperty<AnimationCurve> m_radiusCurve = new();
        
        public Transform? Root
        {
            get => m_root;
            set
            {
                m_root = value;
            }
        }
        public OverrideProperty<string> TemplateName => m_templateName;
        public OverrideProperty<float> BaseRadius => m_baseRadius;
        public OverrideProperty<List<Transform>> IgnoreTransforms => m_ignoreTransforms;
        public OverrideProperty<bool> IsGrabbable => m_isGrabbable;
        public OverrideProperty<bool> IgnoreSelf => m_ignoreSelf;
        public OverrideProperty<List<PortableDynamicBoneCollider>> Colliders => m_colliders;
        public OverrideProperty<bool> IgnoreMultiChild => m_ignoreMultiChild;
        public OverrideProperty<AnimationCurve> RadiusCurve => m_radiusCurve;
        
        public static string GuessTemplateName(Component pb, Transform root)
        {
            var rootPath = RuntimeUtil.AvatarRootPath(root.gameObject);
            var path = RuntimeUtil.AvatarRootPath(pb.gameObject)!.ToLowerInvariant();
            
            if (rootPath == null)
            {
                return "generic";
            }
            
            foreach (var segment in rootPath.Split("/"))
            {
                var template = TemplateFromObjectName(segment);
                if (template != null) return template;
            }
            
            foreach (var segment in path.Split("/"))
            {
                var template = TemplateFromObjectName(segment);
                if (template != null) return template;
            }

            return "generic";
        }

        private static string? TemplateFromObjectName(string path)
        {
            path = path.ToLowerInvariant();
            if (path.Contains("pony") || path.Contains("twin")) return "long_hair";
            
            if (path.Contains("hair"))
            {
                return "hair";
            }

            if (path.Contains("tail")) return "tail";
            if (path.Contains("ear") || path.Contains("kemono") || path.Contains("mimi")) return "ear";
            if (path.Contains("breast")) return "breast";

            return null;
        }
    }
}