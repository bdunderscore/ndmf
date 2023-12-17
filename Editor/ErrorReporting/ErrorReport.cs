#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.runtime;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf
{
    #region

    using UnityObject = Object;

    #endregion

    internal class ErrorReportScope : IDisposable
    {
        private ErrorReport _report;

        public ErrorReportScope(ErrorReport report)
        {
            _report = ErrorReport.CurrentReport;
            ErrorReport.CurrentReport = report;
        }

        public void Dispose()
        {
            ErrorReport.CurrentReport = _report;
        }
    }

    public sealed class ErrorReport
    {
        internal static List<ErrorReport> Reports = new List<ErrorReport>();
        internal static ErrorReport CurrentReport = null;
        internal static int editorFrame = 0;

        private ErrorReport(string avatarName, string avatarPath)
        {
            AvatarName = avatarName;
            AvatarRootPath = avatarPath;
            Errors = ImmutableList<IError>.Empty;
        }

        public string AvatarName { get; }
        public string AvatarRootPath { get; }

        public ImmutableList<IError> Errors { get; private set; }

        internal static ErrorReport Create(GameObject root, bool isClone)
        {
            if (Time.frameCount != editorFrame)
            {
                editorFrame = Time.frameCount;
                Reports.Clear();
            }

            var name = root.name;
            if (isClone && name.EndsWith("(Clone)"))
            {
                name = name.Substring(0, name.Length - 7);
            }

            var path = RuntimeUtil.RelativePath(null, root);
            if (isClone && path.EndsWith("(Clone)"))
            {
                path = path.Substring(0, path.Length - 7);
            }

            var report = new ErrorReport(name, path);
            Reports.Add(report);

            return report;
        }

        public void AddError(IError error)
        {
            Errors = Errors.Add(error);
        }

        public static void ReportError(IError error)
        {
            Debug.LogWarning("[NDMF] Error Reported: " + error.ToMessage());
            CurrentReport?.AddError(error);
        }

        public static void ReportError(Localizer localizer, ErrorCategory errorCategory, string key,
            params object[] args)
        {
            ReportError(new InlineError(localizer, errorCategory, key, args));
        }
    }

    internal class InlineError : SimpleError
    {
        private readonly ObjectReference[] _references;

        private readonly string[] _subst;

        public InlineError(Localizer localizer, ErrorCategory errorCategory, string key, params object[] args)
        {
            Localizer = localizer;
            Category = errorCategory;
            TitleKey = key;

            _subst = Array.ConvertAll(args, o => o.ToString());
            _references = args.Select(r =>
            {
                if (r is ObjectReference or)
                {
                    return or;
                }
                else if (r is UnityObject uo)
                {
                    return ObjectRegistry.GetReference(uo);
                }
                else
                {
                    return null;
                }
            }).Where(r => r != null).ToArray();
        }

        protected override Localizer Localizer { get; }
        public override ErrorCategory Category { get; }
        protected override string TitleKey { get; }

        protected override string[] DetailsSubst => _subst;
        protected override string[] HintSubst => _subst;
        protected override ObjectReference[] References => _references;
    }
}