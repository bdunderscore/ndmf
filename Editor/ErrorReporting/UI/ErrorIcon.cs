#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.ndmf.ui
{
    /// <summary>
    /// Displays a severity icon for a particular ErrorLevel.
    /// </summary>
#if UNITY_6000_0_OR_NEWER
    [UxmlElement]
#endif
    public sealed partial class ErrorIcon : VisualElement
    {
        private ErrorSeverity _severity;

        private Image _image;

        public ErrorIcon()
        {
            _image = new Image();
            Add(_image);
        }

        public ErrorSeverity Severity
        {
            get => _severity;
            set
            {
                _severity = value;
                UpdateIcon();
            }
        }

        private void UpdateIcon()
        {
            Texture2D tex;

            switch (_severity)
            {
                case ErrorSeverity.Information:
                    tex = EditorGUIUtility.FindTexture("d_console.infoicon");
                    break;
                case ErrorSeverity.NonFatal:
                    tex = EditorGUIUtility.FindTexture("d_console.warnicon");
                    break;
                default:
                    tex = EditorGUIUtility.FindTexture("d_console.erroricon");
                    break;
            }

            _image.image = tex;
        }
#if UNITY_6000_4_OR_NEWER
        [UxmlAttribute]
        public string category
        {
            get => _severity.ToString();
            set
            {
                if (value == null || !Enum.TryParse<ErrorSeverity>(value, out var categoryVal))
                {
                    categoryVal = ErrorSeverity.Error;
                }

                _severity = categoryVal;
            }
        }
#endif

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<ErrorIcon, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            UxmlStringAttributeDescription m_Category = new UxmlStringAttributeDescription { name = "category" };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield return new UxmlChildElementDescription(typeof(VisualElement)); }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);

                var elem = ve as ErrorIcon;

                var categoryStr = m_Category.GetValueFromBag(bag, cc);

                if (categoryStr == null || !Enum.TryParse<ErrorSeverity>(categoryStr, out var categoryVal))
                {
                    categoryVal = ErrorSeverity.Error;
                }

                elem.Severity = categoryVal;
            }
        }
#endif
    }
}