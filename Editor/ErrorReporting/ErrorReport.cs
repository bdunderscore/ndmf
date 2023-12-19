#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    internal class ReferenceStackScope : IDisposable
    {
        private ErrorReport _report;
        private int _stackDepth;
        
        public ReferenceStackScope(ErrorReport report)
        {
            _report = report;
            _stackDepth = ErrorReport.ReferenceStack.Count;
        }
        
        public void Dispose()
        {
            ErrorReport.ReferenceStack.RemoveRange(_stackDepth, ErrorReport.ReferenceStack.Count - _stackDepth);
        }
    }

    internal class RestoreContextScope : IDisposable
    {
        private ErrorReport _report;
        private ErrorContext _priorContext;

        public RestoreContextScope(ErrorReport report)
        {
            this._report = report;
            this._priorContext = report.CurrentContext;
        }
        
        public void Dispose()
        {
            _report.CurrentContext = _priorContext;
        }
    }

    public sealed class ErrorReport
    {
        internal static List<ErrorReport> Reports = new List<ErrorReport>();
        internal static ErrorReport CurrentReport = null;
        internal static int editorFrame = 0;
        
        internal static List<ObjectReference> ReferenceStack = new List<ObjectReference>();

        private ErrorReport(string avatarName, string avatarPath)
        {
            AvatarName = avatarName;
            AvatarRootPath = avatarPath;
            Errors = ImmutableList<ErrorContext>.Empty;
        }

        public string AvatarName { get; }
        public string AvatarRootPath { get; }

        public ImmutableList<ErrorContext> Errors { get; private set; }

        internal ErrorContext CurrentContext = new ErrorContext();

        private HashSet<Exception> ReportedExceptions = new HashSet<Exception>();
        
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
            var context = CurrentContext;
            context.TheError = error;
            
            Errors = Errors.Add(context);
        }

        public static void ReportError(IError error)
        {
            Debug.LogWarning("[NDMF] Error Reported: " + error.ToMessage());

            var contextObjs = ReferenceStack.ToList();
            contextObjs.Reverse();
            foreach (var context in contextObjs)
            {
                if (context != null) error.AddReference(context);
            }
            
            CurrentReport?.AddError(error);
        }

        public static void ReportError(Localizer localizer, ErrorCategory errorCategory, string key,
            params object[] args)
        {
            ReportError(new InlineError(localizer, errorCategory, key, args));
        }

        public static void ReportException(Exception e)
        {
            var report = CurrentReport;

            Exception e_ = e;
            while (e_ != null)
            {
                if (report.ReportedExceptions.Contains(e_)) return;
                e_ = e_.InnerException;
            }
            
            ReportError(new StackTraceError(e));
            report.ReportedExceptions.Add(e);
        }

        public bool TryResolveAvatar(out GameObject av)
        {
            var scene = SceneManager.GetActiveScene();

            var firstPathElement = AvatarRootPath.Split('/')[0];
            var remaining = firstPathElement == AvatarRootPath ? null : AvatarRootPath.Substring(firstPathElement.Length + 1);
            
            foreach (var obj in scene.GetRootGameObjects())
            {
                if (obj.name == firstPathElement)
                {
                    if (remaining == null)
                    {
                        av = obj;
                        return true;
                    }
                    else
                    {
                        av = obj.transform.Find(remaining)?.gameObject;
                        return av != null;
                    }
                }
            }

            av = null;
            return false;
        }

        public IDisposable WithContextObject(UnityObject obj)
        {
            var scope = new ReferenceStackScope(this);
            ReferenceStack.Add(ObjectRegistry.GetReference(obj));

            return scope;
        }
        
        public T WithContextObject<T>(UnityObject obj, Func<T> func)
        {
            using (WithContextObject(obj))
            {
                try
                {
                    return func();
                }
                catch (Exception e)
                {
                    ReportException(e);
                    throw e;
                }
            }
        }
        
        public void WithContextObject(UnityObject obj, Action action)
        {
            using (WithContextObject(obj))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    ReportException(e);
                    throw e;
                }
            }
        }

        internal IDisposable WithContext(PluginBase thePlugin)
        {
            var scope = new RestoreContextScope(this);
            CurrentContext.Plugin = thePlugin;
            return scope;
        }
        
        internal IDisposable WithContextPassName(string name)
        {
            var scope = new RestoreContextScope(this);
            CurrentContext.PassName = name;
            return scope;
        }

        internal IDisposable WithExtensionContextTrace(Type extensionContext)
        {
            var scope = new RestoreContextScope(this);
            CurrentContext.ExtensionContext = extensionContext;
            return scope;
        }
    }

    internal class InlineError : SimpleError
    {
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
            }).Where(r => r != null).ToList();
        }

        protected override Localizer Localizer { get; }
        public override ErrorCategory Category { get; }
        protected override string TitleKey { get; }

        protected override string[] DetailsSubst => _subst;
        protected override string[] HintSubst => _subst;
    }
}