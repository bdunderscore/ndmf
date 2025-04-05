using UnityEngine;

namespace nadena.dev.ndmf.runtime.components
{
    public static class NDMFDynamicBoneTemplate
    {
        const string PlatformDefault = "platformDefault"; 
        const string Hair = "hair";
        const string Ribbon = "ribbon";
        const string Skirt = "skirt";
    }
    
    public class NDMFDynamicBone : MonoBehaviour
    {
        private string m_parameterTemplate = "platformDefault";

        public string ParameterTemplate
        {
            get => m_parameterTemplate;
            set => m_parameterTemplate = value;
        }

        private float m_baseRadius = 0;
        
        public float BaseRadius
        {
            get => m_baseRadius;
            set => m_baseRadius = value;
        }
        
        // TODO: Add a way to set this curve
    }
}