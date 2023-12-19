#region

using System;
using System.Text;
using System.Text.RegularExpressions;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.ndmf
{
    public interface IError
    {
        ErrorCategory Category { get; }
        VisualElement CreateVisualElement(ErrorReport report);
        string ToMessage();
    }

    public abstract class SimpleError : IError
    {
        private static readonly Regex Pattern = new Regex("\\{([0-9])\\}");

        protected abstract Localizer Localizer { get; }

        protected abstract string TitleKey { get; }
        protected virtual string DetailsKey => TitleKey + ":description";
        protected virtual string[] DetailsSubst => Array.Empty<string>();
        protected virtual string HintKey => TitleKey + ":hint";
        protected virtual string[] HintSubst => Array.Empty<string>();

        public virtual ObjectReference[] References => Array.Empty<ObjectReference>();

        public abstract ErrorCategory Category { get; }

        public VisualElement CreateVisualElement(ErrorReport report)
        {
            return new SimpleErrorUI(report, this);
        }

        public string ToMessage()
        {
            var title = FormatTitle();
            var details = FormatDetails();

            if (details != null)
            {
                title += "\n\n" + details;
            }

            return title;
        }

        public string FormatTitle()
        {
            return Localizer.GetLocalizedString(TitleKey);
        }

        public string FormatDetails()
        {
            return SafeSubst(DetailsKey, DetailsSubst);
        }

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
    }
}