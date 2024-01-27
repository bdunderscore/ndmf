using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.localization;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf
{
    internal class InlineError : SimpleError
    {
        private readonly string[] _subst;

        public InlineError(Localizer localizer, ErrorSeverity errorSeverity, string key, params object[] args)
        {
            Localizer = localizer;
            Severity = errorSeverity;
            TitleKey = key;

            List<string> substitutions = new List<string>();
            AddContext(args, substitutions);
            _subst = substitutions.ToArray();
        }

        private void AddContext(IEnumerable args, List<string> substitutions)
        {
            foreach (var arg in args)
            {
                if (arg == null)
                {
                    substitutions.Add("<missing>");
                }
                else if (arg is string s)
                {
                    // string is IEnumerable, so we have to special case this
                    substitutions.Add(s);
                }
                else if (arg is ObjectReference or)
                {
                    AddReference(or);
                    substitutions.Add(or.ToString());
                }
                else if (arg is Object uo)
                {
                    var objectReference = ObjectRegistry.GetReference(uo);
                    AddReference(objectReference);
                    substitutions.Add(objectReference.ToString());
                }
                else if (arg is IEnumerable e)
                {
                    AddContext(e, substitutions);
                }
                else if (arg is IErrorContext ec)
                {
                    AddContext(ec.ContextReferences, substitutions);
                }
                else
                {
                    substitutions.Add(arg.ToString());
                }
            }
        }

        public override Localizer Localizer { get; }
        public override ErrorSeverity Severity { get; }
        public override string TitleKey { get; }

        public override string[] TitleSubst => _subst;
        public override string[] DetailsSubst => _subst;
        public override string[] HintSubst => _subst;
    }
}