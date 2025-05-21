using System;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.ndmf.multiplatform.components
{
    /// <summary>
    /// Properties on PortableDynamicBone inherit their values from the primary platform's configuration if present.
    /// This class represents that override.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [PublicAPI]
    [Serializable]
    public sealed class OverrideProperty<T> : IOverrideProperty
    {
        [SerializeField]
        private bool m_override;
        /// <summary>
        /// True if this property should override the value from the primary platform.
        /// </summary>
        public bool Override 
        {
            get => m_override;
            set
            {
                m_override = value;
            }
        }

        [SerializeField]
        private T m_value;
        public T Value
        {
            get => m_value;
            set
            {
                m_value = value;
            }
        }

        /// <summary>
        /// Sets the value of this property, but only if Override is false.
        /// </summary>
        /// <param name="value"></param>
        public void WeakSet(T value)
        {
            if (!m_override) 
            {
                m_value = value;
            }
        }
        
        public static implicit operator T(OverrideProperty<T> prop)
        {
            return prop.Value;
        }
    }
}