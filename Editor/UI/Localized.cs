using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.ui
{
    public class Localized : VisualElement
    {
        private static Dictionary<Type, Action<VisualElement>> _localizers =
            new Dictionary<Type, Action<VisualElement>>();
        
        public new class UxmlFactory : UxmlFactory<Localized, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            UxmlStringAttributeDescription m_Folder = new UxmlStringAttributeDescription {name = "folder"};
            
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get
                {
                    yield return new UxmlChildElementDescription(typeof (VisualElement));
                }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);

                var elem = ve as Localized;
                
                elem.folder = m_Folder.GetValueFromBag(bag, cc);
            }
        }
        
        public string folder { get; set; }

        public Localized()
        {
            RegisterCallback<GeometryChangedEvent>(Init);
        }

        private void Init(GeometryChangedEvent evt)
        {
            WalkTree(this);
            UnregisterCallback<GeometryChangedEvent>(Init);
        }

        private static void WalkTree(VisualElement elem)
        {
            var ty = elem.GetType();

            GetLocalizationOperation(ty)(elem);

            foreach (var child in elem.Children())
            {
                WalkTree(child);
            }
        }

        private static Action<VisualElement> GetLocalizationOperation(Type ty)
        {
            if (!_localizers.TryGetValue(ty, out var action))
            {
                PropertyInfo m_label = ty.GetProperty("text") ?? ty.GetProperty("label");

                if (m_label == null)
                {
                    action = _elem => { };
                }
                else
                {
                    action = elem =>
                    {
                        var cur_label = m_label.GetValue(elem) as string;
                        if (cur_label != null && cur_label.StartsWith("##"))
                        {
                            var key = cur_label.Substring(2);

                            var new_label = "label: " + key;
                            var new_tooltip = "tooltip: " + key;

                            m_label.SetValue(elem, new_label);
                            elem.tooltip = new_tooltip;
                        }
                    };
                }

                _localizers[ty] = action;
            }

            return action;
        }
    }
}