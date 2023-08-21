using System;

namespace nadena.dev.build_framework
{
    public interface IExtensionContext
    {
        void OnActivate(BuildContext context);
        void OnDeactivate(BuildContext context);
    }
}