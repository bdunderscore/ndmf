using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.ui
{
    using UnityObject = UnityEngine.Object;

    /// <summary>
    /// VisualElement used to provide a consistent UI for selecting an object referenced by error messages.
    /// </summary>
    public sealed class ObjectSelector : VisualElement
    {
        /// <summary>
        /// Attempt to resolve an ObjectReference to a unity object and create a UI to display it.
        /// </summary>
        /// <param name="report">Error report this selector is referencing (used to find the avatar root)</param>
        /// <param name="reference">Object reference to represent</param>
        /// <param name="selector">The ObjectSelector to be created</param>
        /// <returns></returns>
        public static bool TryCreate(ErrorReport report, ObjectReference reference, out ObjectSelector selector)
        {
            selector = null;

            if (!reference.TryResolve(report, out var obj))
            {
                return false;
            }

            if (obj == null)
            {
                reference.TryResolve(report, out obj);
                return false;
            }

            selector = new ObjectSelector(obj);
            return true;
        }

        private readonly UnityObject _target;

        private ObjectSelector(UnityObject obj)
        {
            _target = obj;

            var styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/nadena.dev.ndmf/Editor/ErrorReporting/UI/Resources/ObjectSelector.uss");
            styleSheets.Add(styleSheet);

            AddToClassList("selection-button");

            var tex = EditorGUIUtility.FindTexture("d_Search Icon");
            var icon = new Image { image = tex };
            Add(icon);

            var button = new Button(() =>
            {
                if (_target != null)
                {
                    Selection.activeObject = _target;
                    if (EditorUtility.IsPersistent(_target))
                    {
                        EditorUtility.FocusProjectWindow();
                    }

                    EditorGUIUtility.PingObject(_target);
                }
            });

            //button.Add(new Label("[" + typeName + "] " + target.name));
            var name = _target.name;

            if (_target is Component c) name = c.gameObject.name;

            button.text = "[" + _target.GetType().Name + "] " + name;
            Add(button);
        }
    }
}