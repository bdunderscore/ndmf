using System;

namespace nadena.dev.build_framework
{
    public interface ExtensionContext
    {
        void OnActivate(BuildContext context);
        void OnDeactivate(BuildContext context);
    }
}