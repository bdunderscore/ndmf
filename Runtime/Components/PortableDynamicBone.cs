﻿#nullable enable

using System.Collections.Generic;
using JetBrains.Annotations;
using nadena.dev.ndmf.runtime;
using UnityEngine;

namespace nadena.dev.ndmf.multiplatform.components
{
#if NDMF_EXPERIMENTAL
    [AddComponentMenu("NDMF/Portable/NDMF Portable Dynamic Bone")]
#else
    [AddComponentMenu("")]
#endif
    internal class PortableDynamicBone : MonoBehaviour, INDMFEditorOnly
    {
        [SerializeField] [HideInInspector] private Component m_originalComponent = new();
        [SerializeField] private OverrideProperty<Transform> m_root = new();
        [SerializeField] private OverrideProperty<string> m_templateName = new();
        [SerializeField] private OverrideProperty<float> m_baseRadius = new();
        [SerializeField] private OverrideProperty<List<Transform>> m_ignoreTransforms = new();
        [SerializeField] private OverrideProperty<bool> m_isGrabbable = new(), m_ignoreSelf = new();
        [SerializeField] private OverrideProperty<List<PortableDynamicBoneCollider>> m_colliders = new();
        
        public Component OriginalComponent
        {
            get => m_originalComponent;
            set => m_originalComponent = value;
        }

        public OverrideProperty<Transform> Root => m_root;
        public OverrideProperty<string> TemplateName => m_templateName;
        public OverrideProperty<float> BaseRadius => m_baseRadius;
        public OverrideProperty<List<Transform>> IgnoreTransforms => m_ignoreTransforms;
        public OverrideProperty<bool> IsGrabbable => m_isGrabbable;
        public OverrideProperty<bool> IgnoreSelf => m_ignoreSelf;
        public OverrideProperty<List<PortableDynamicBoneCollider>> Colliders => m_colliders;

        [SerializeField] [HideInInspector] internal bool m_isWeak = false;
        public bool IsWeak
        {
            get => m_isWeak;
            set => m_isWeak = value;
        }
        
        public static string GuessTemplateName(Component pb, Transform root)
        {
            var rootPath = RuntimeUtil.AvatarRootPath(root.gameObject);
            var path = RuntimeUtil.AvatarRootPath(pb.gameObject)!.ToLowerInvariant();
            
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