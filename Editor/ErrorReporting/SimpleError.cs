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
        public abstract Localizer Localizer { get; }

        /// <summary>
        /// The key to use for the title of the error. By default, all other keys are derived from this TitleKey.
        /// </summary>
        public abstract string TitleKey { get; }

        /// <summary>
        /// String substitutions to insert into the title of the error display. You can reference these with
        /// e.g. `{0}`.
        /// </summary>
        public virtual string[] TitleSubst => Array.Empty<string>();

        /// <summary>
        /// The key to use for the details section of the error display. By default, this is the TitleKey + `:description`.
        /// </summary>
        public virtual string DetailsKey => TitleKey + ":description";

        /// <summary>
        /// String substitutions to insert into the details section of the error display. You can reference these with
        /// e.g. `{0}`.
        /// </summary>
        public virtual string[] DetailsSubst => Array.Empty<string>();

        /// <summary>
        /// The key to use for the hint section of the error display. By default, this is the TitleKey + `:hint`.
        /// This section should be used to provide a hint to the user about how to resolve the error.
        /// </summary>
        public virtual string HintKey => TitleKey + ":hint";

        /// <summary>
        /// String substitutions to insert into the hint section of the error display. You can reference these with
        /// e.g. `{0}`.
        /// </summary>
        public virtual string[] HintSubst => Array.Empty<string>();

        /// <summary>
        /// Any ObjectReferences to display to the user; the user will be able to click to jump to these objects.
        /// </summary>
        public List<ObjectReference> _references = new List<ObjectReference>();

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
        public virtual string FormatTitle()
        {
            return SafeSubstByKey(TitleKey, TitleSubst);
        }

        /// <summary>
        /// Returns the formatted details message for the error.
        /// </summary>
        /// <returns></returns>
        public virtual string FormatDetails()
        {
            return SafeSubstByKey(DetailsKey, DetailsSubst);
        }

        /// <summary>
        /// Returns the formatted hint message for the error.
        /// </summary>
        /// <returns></returns>
        public virtual string FormatHint()
        {
            return SafeSubstByKey(HintKey, HintSubst);
        }

        /// <summary>
        /// Substitutes placeholders like {0}, {1} in the localized string referenced by `key` with the values in
        /// `subst`. Unlike String.Format, this will not throw an exception if the number of substitutions does not
        /// match the number of placeholders.
        /// </summary>
        /// <param name="key">A localization key that references a string containing placeholders</param>
        /// <param name="subst"></param>
        /// <returns></returns>
        protected string SafeSubstByKey(string key, string[] subst)
        {
            if (!Localizer.TryGetLocalizedString(key, out var message))
            {
                return null;
            }

            return SafeSubst(message, subst);
        }

        /// <summary>
        /// Substitutes placeholders like {0}, {1} in the raw string `message` with the values in
        /// `subst`. Unlike String.Format, this will not throw an exception if the number of substitutions does not
        /// match the number of placeholders.
        /// </summary>
        /// <param name="message">The raw string containing placeholders</param>
        /// <param name="subst"></param>
        /// <returns></returns>
        protected static string SafeSubst(string message, string[] subst)
        {
            var matches = Pattern.Matches(message);
            int consumedUpTo = 0;

            StringBuilder sb = new StringBuilder();
            foreach (Match match in matches)
            {
                sb.Append(message.Substring(consumedUpTo, match.Index - consumedUpTo));
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

            sb.Append(message.Substring(consumedUpTo));

            return sb.ToString();
        }

        public void AddReference(ObjectReference obj)
        {
            if (obj != null) _references.Add(obj);
        }
    }
}