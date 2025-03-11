#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
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

    internal class NullScope : IDisposable
    {
        public void Dispose()
        {
        }
    }

    internal class ErrorReportScope : IDisposable
    {
        private ErrorReport _report;

        public ErrorReportScope(ErrorReport report)
        {
            _report = ErrorReport.CurrentReport;
            ErrorReport.CurrentReport ??= report;
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

    /// <summary>
    /// Contains any errors or warnings issued during a single build operation.
    /// </summary>
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

        /// <summary>
        /// The name of the avatar being processed
        /// </summary>
        public string AvatarName { get; }
        /// <summary>
        /// The path (from the scene root) of the avatar being processed
        /// </summary>
        public string AvatarRootPath { get; }

        /// <summary>
        /// A list of reported errors.
        /// </summary>
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

        internal void AddError(IError error)
        {
            var context = CurrentContext;
            context.TheError = error;
            
            Errors = Errors.Add(context);
        }

        /// <summary>
        /// Adds an error to the currently active error report. If no error report is active, the error will simply be
        /// logged to the debug log. 
        /// </summary>
        /// <param name="error"></param>
        public static void ReportError(IError error)
        {
            if (error is StackTraceError e)
            {
                Debug.LogException(e.Exception);
            }
            else
            {
                Debug.LogWarning("[NDMF] Error Reported: " + error.ToMessage());
            }

            var contextObjs = ReferenceStack.ToList();
            contextObjs.Reverse();
            foreach (var context in contextObjs)
            {
                if (context != null) error.AddReference(context);
            }
            
            CurrentReport?.AddError(error);
        }

        /// <summary>
        /// Helper to report a SimpleError.
        /// </summary>
        /// <param name="localizer">The Localizer used to look up translations</param>
        /// <param name="errorSeverity">The severity of the error</param>
        /// <param name="key">The prefix used to find localization keys</param>
        /// <param name="args">Inline substitutions and unity objects to reference from the error</param>
        public static void ReportError(Localizer localizer, ErrorSeverity errorSeverity, string key,
            params object[] args)
        {
            ReportError(new InlineError(localizer, errorSeverity, key, args));
        }

        /// <summary>
        /// Helper to report an exception. This will generate an error of InternalError severity.
        /// </summary>
        /// <param name="e">Exception to report</param>
        /// <param name="additionalStackTrace">Additional information to append to the stack trace</param>
        public static void ReportException(Exception e, string additionalStackTrace = null)
        {
            var report = CurrentReport;

            Exception e_ = e;
            while (e_ != null && report != null)
            {
                if (report.ReportedExceptions.Contains(e_)) return;
                e_ = e_.InnerException;
            }
            
            ReportError(new StackTraceError(e, additionalStackTrace));
            report?.ReportedExceptions?.Add(e);
        }

        /// <summary>
        /// Attempts to find the original avatar that generated the report.
        /// </summary>
        /// <param name="av">The avatar root</param>
        /// <returns>true if the avatar was found, otherwise false</returns>
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

        /// <summary>
        /// Returns a disposable scope, within which all errors will reference a specific UnityObject.
        /// </summary>
        /// <param name="obj">The object to reference (can be null)</param>
        /// <returns>A disposable that will remove the object from the current scope when disposed.</returns>
        public static IDisposable WithContextObject([CanBeNull] UnityObject obj)
        {
            if (obj == null || CurrentReport == null) return new NullScope();
            
            var scope = new ReferenceStackScope(CurrentReport);
            ReferenceStack.Add(ObjectRegistry.GetReference(obj));

            return scope;
        }
        
        /// <summary>
        /// Executes a function, within which any errors will reference a specific UnityObject.
        /// Thrown exceptions will automatically be logged.
        /// </summary>
        /// <param name="obj">The object to reference</param>
        /// <param name="func">The function to invoke</param>
        /// <returns>The return value of func()</returns>
        public static T WithContextObject<T>([CanBeNull] UnityObject obj, Func<T> func)
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
        
        /// <summary>
        /// Executes a function, within which any errors will reference a specific UnityObject.
        /// Thrown exceptions will automatically be logged.
        /// </summary>
        /// <param name="obj">The object to reference</param>
        /// <param name="func">The function to invoke</param>
        /// <returns>The return value of func()</returns>
        public static void WithContextObject([CanBeNull] UnityObject obj, Action action)
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
                    throw;
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

        /// <summary>
        /// Runs the given action, capturing all errors and returning any errors generated.
        /// Intended for unit testing only.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static List<ErrorContext> CaptureErrors(Action action)
        {
            var report = new ErrorReport("test avatar", "test avatar");
            
            using (new ErrorReportScope(report))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    ReportException(e);
                }
            }
            
            return report.Errors.ToList();
        }

        /// <summary>
        /// Clears all error reports.
        /// </summary>
        public static void Clear()
        {
            Reports.Clear();
            CurrentReport = null;
        }
    }
}
