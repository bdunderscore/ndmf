#nullable enable

using System;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.ndmf.multiplatform.components
{
    [Serializable]
    public enum PortableDynamicColliderType
    {
        Sphere,
        Capsule,
        Plane
    }
    
    [AddComponentMenu("NDMF/Portable/NDMF Portable Dynamic Bone Collider")]
    [PublicAPI]
    public class PortableDynamicBoneCollider : MonoBehaviour, INDMFEditorOnly
    {
        [SerializeField] internal Transform? m_root;
        [SerializeField] internal PortableDynamicColliderType m_colliderType;
        [SerializeField] internal float m_radius;
        [SerializeField] internal float m_height;
        [SerializeField] internal Vector3 m_positionOffset;
        [SerializeField] internal Quaternion m_rotationOffset;
        [SerializeField] internal bool m_insideBounds;
    
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
        
        public bool InsideBounds
        {
            get => m_insideBounds;
            set => m_insideBounds = value;
        }
    }
}