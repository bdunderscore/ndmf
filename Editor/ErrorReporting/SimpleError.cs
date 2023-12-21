using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// Base class for errors that obtain their messages from the Localization system.
    /// </summary>
    public abstract class SimpleError : IError
    {
        private static readonly Regex Pattern = new Regex("\\{([0-9])\\}");

        /// <summary>
        /// The Localizer to use to look up strings.
        /// </summary>
        protected abstract Localizer Localizer { get; }

        /// <summary>
        /// The key to use for the title of the error. By default, all other keys are derived from this TitleKey.
        /// </summary>
        protected abstract string TitleKey { get; }
        /// <summary>
        /// The key to use for the details section of the error display. By default, this is the TitleKey + `:description`.
        /// </summary>
        protected virtual string DetailsKey => TitleKey + ":description";
        /// <summary>
        /// String substitutions to insert into the details section of the error display. You can reference these with
        /// e.g. `{0}`.
        /// </summary>
        protected virtual string[] DetailsSubst => Array.Empty<string>();
        /// <summary>
        /// The key to use for the hint section of the error display. By default, this is the TitleKey + `:hint`.
        /// This section should be used to provide a hint to the user about how to resolve the error.
        /// </summary>
        protected virtual string HintKey => TitleKey + ":hint";
        /// <summary>
        /// String substitutions to insert into the hint section of the error display. You can reference these with
        /// e.g. `{0}`.
        /// </summary>
        protected virtual string[] HintSubst => Array.Empty<string>();
        
        /// <summary>
        /// Any ObjectReferences to display to the user; the user will be able to click to jump to these objects.
        /// </summary>
        protected List<ObjectReference> _references = new List<ObjectReference>();
        /// <summary>
        /// Any ObjectReferences to display to the user; the user will be able to click to jump to these objects.
        /// By default this just returns the `_references` protected field.
        /// </summary>
        public virtual ObjectReference[] References => _references.ToArray();

        /// <summary>
        /// The severity of the error.
        /// </summary>
        public abstract ErrorSeverity Severity { get; }

        public virtual VisualElement CreateVisualElement(ErrorReport report)
        {
            return new SimpleErrorUI(report, this);
        }

        public virtual string ToMessage()
        {
            var title = FormatTitle();
            var details = FormatDetails();

            if (details != null)
            {
                title += "\n\n" + details;
            }

            return title;
        }

        /// <summary>
        /// Returns the formatted title of the error.
        /// </summary>
        /// <returns></returns>
        public string FormatTitle()
        {
            return Localizer.GetLocalizedString(TitleKey);
        }

        /// <summary>
        /// Returns the formatted details message for the error.
        /// </summary>
        /// <returns></returns>
        public string FormatDetails()
        {
            return SafeSubst(DetailsKey, DetailsSubst);
        }

        /// <summary>
        /// Returns the formatted hint message for the error.
        /// </summary>
        /// <returns></returns>
        public string FormatHint()
        {
            return SafeSubst(HintKey, HintSubst);
        }

        private string SafeSubst(string key, string[] subst)
        {
            if (!Localizer.TryGetLocalizedString(key, out var value))
            {
                return null;
            }

            var matches = Pattern.Matches(value);
            int consumedUpTo = 0;

            StringBuilder sb = new StringBuilder();
            foreach (Match match in matches)
            {
                sb.Append(value.Substring(consumedUpTo, match.Index - consumedUpTo));
                consumedUpTo = match.Index + match.Length;

                if (int.TryParse(match.Groups[1].Value, out var substIndex) && substIndex >= 0 &&
                    substIndex < subst.Length)
                {
                    sb.Append(subst[substIndex]);
                }
                else
                {
                    sb.Append(match.Value);
                }
            }

            sb.Append(value.Substring(consumedUpTo));

            return sb.ToString();
        }
        
        public void AddReference(ObjectReference obj)
        {
            if (obj != null) _references.Add(obj);
        }
    }
}