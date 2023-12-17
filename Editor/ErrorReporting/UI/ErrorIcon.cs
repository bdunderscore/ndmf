#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.ndmf.ui
{
    public sealed class ErrorIcon : VisualElement
    {
        private ErrorCategory _category;

        private Image _image;

        public ErrorIcon()
        {
            _image = new Image();
            Add(_image);
        }

        public ErrorCategory Category
        {
            get => _category;
            set
            {
                _category = value;
                UpdateIcon();
            }
        }

        private void UpdateIcon()
        {
            Texture2D tex;

            switch (_category)
            {
                case ErrorCategory.Information:
                    tex = EditorGUIUtility.FindTexture("d_console.infoicon");
                    break;
                case ErrorCategory.NonFatal:
                    tex = EditorGUIUtility.FindTexture("d_console.warnicon");
                    break;
                default:
                    tex = EditorGUIUtility.FindTexture("d_console.erroricon");
                    break;
            }

            _image.image = tex;
        }

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

                if (categoryStr == null || !Enum.TryParse<ErrorCategory>(categoryStr, out var categoryVal))
                {
                    categoryVal = ErrorCategory.Error;
                }

                elem.Category = categoryVal;
            }
        }
    }
}