using System;
using System.Linq;
using nadena.dev.ndmf.localization;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf
{
    public class InlineError : SimpleError
    {
        private readonly string[] _subst;

        public InlineError(Localizer localizer, ErrorCategory errorCategory, string key, params object[] args)
        {
            Localizer = localizer;
            Category = errorCategory;
            TitleKey = key;

            _subst = Array.ConvertAll(args, o => o?.ToString());
            _references = args.Select(r =>
            {
                if (r is ObjectReference or)
                {
                    return or;
                }
                else if (r is Object uo)
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