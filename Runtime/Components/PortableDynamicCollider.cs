#nullable enable

using System;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.ndmf.multiplatform.components
{
    [Serializable]
    internal enum PortableDynamicColliderType
    {
        Sphere,
        Capsule,
        Plane
    }
    
    #if NDMF_EXPERIMENTAL
    [AddComponentMenu("NDMF/Portable/NDMF Portable Dynamic Bone Collider")]
    #else
    [AddComponentMenu("")]
    #endif
    internal class PortableDynamicBoneCollider : MonoBehaviour, INDMFEditorOnly
    {
        [SerializeField] private Transform? m_root;
        [SerializeField] private PortableDynamicColliderType m_colliderType;
        [SerializeField] private float m_radius;
        [SerializeField] private float m_height;
        [SerializeField] private Vector3 m_positionOffset;
        [SerializeField] private Quaternion m_rotationOffset;
    
        public Transform? Root
        {
            get => m_root;
            set => m_root = value;
        }
        
        public PortableDynamicColliderType ColliderType
        {
            get => m_colliderType;
            set => m_colliderType = value;
        }
        
        public float Radius
        {
            get => m_radius;
            set => m_radius = value;
        }
        
        public float Height
        {
            get => m_height;
            set => m_height = value;
        }
        
        public Vector3 PositionOffset
        {
            get => m_positionOffset;
            set => m_positionOffset = value;
        }
        
        public Quaternion RotationOffset
        {
            get => m_rotationOffset;
            set => m_rotationOffset = value;
        }
    }
}