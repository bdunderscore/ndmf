using System;
using System.Runtime.InteropServices;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf
{
    public class StackTraceError : SimpleError
    {
        private Exception _e;
        
        public StackTraceError(Exception e)
        {
            this._e = e;
        }

        protected override Localizer Localizer => NDMFLocales.L;
        protected override string TitleKey => "Errors:InternalError";
        public override ErrorCategory Category => ErrorCategory.InternalError;

        protected override string[] DetailsSubst => new []
        {
            _e.GetType().Name
        };

        public override VisualElement CreateVisualElement(ErrorReport report)
        {
            SimpleErrorUI ui = (SimpleErrorUI) base.CreateVisualElement(report);
            if (_e.StackTrace != null) ui.AddStackTrace(_e + "\n" + _e.StackTrace);
            else ui.AddStackTrace(_e.ToString());
            return ui;
        }

        public override string ToMessage()
        {
            if (_e.StackTrace != null)
            {
                return base.ToMessage() + "\n\n" + _e + "\n" + _e.StackTrace;
            }
            else
            {
                return base.ToMessage() + "\n\n" + _e;
            }
        }
    }
}