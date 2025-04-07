using System;
using UnityEngine;

namespace nadena.dev.ndmf.multiplatform.components
{
    [Serializable]
    internal class OverrideProperty<T> : IOverrideProperty
    {
        [SerializeField]
        private bool m_override;
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