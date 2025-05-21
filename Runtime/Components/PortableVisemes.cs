#nullable enable

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.ndmf.runtime.components
{
    [AddComponentMenu("NDMF/NDMF Portable Visemes")]
    [PublicAPI]
    public class PortableBlendshapeVisemes : MonoBehaviour, INDMFEditorOnly, IPortableAvatarConfigTag
    {
        [Serializable]
        public struct Shape
        {
            /// <summary>
            /// Corresponds to a viseme key in CommonAvatarInfo
            /// </summary>
            public string? VisemeName;
            public string? Blendshape;
        }
        
        [SerializeField]
        internal SkinnedMeshRenderer? m_targetRenderer;
        public SkinnedMeshRenderer? TargetRenderer { get => m_targetRenderer; set => m_targetRenderer = value; }

        [SerializeField]
        internal List<Shape> m_shapes = new();
        public List<Shape> Shapes
        {
            get => m_shapes;
            set => m_shapes = value;
        }
    }
}