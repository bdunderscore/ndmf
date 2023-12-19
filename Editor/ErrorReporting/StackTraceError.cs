using System;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf
{
    public class StackTraceError : IError
    {
        private Exception _e;
        
        public StackTraceError(Exception e)
        {
            this._e = e;
        }

        public ErrorCategory Category => ErrorCategory.InternalError;
        public VisualElement CreateVisualElement(ErrorReport report)
        {
            throw new NotImplementedException();
        }

        public string ToMessage()
        {
            return "Internal error: " + _e.Message + "\n\n" + _e.StackTrace;
        }
    }
}