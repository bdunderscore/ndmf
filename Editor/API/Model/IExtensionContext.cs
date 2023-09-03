using System;

namespace nadena.dev.ndmf
{
    public interface IExtensionContext
    {
        void OnActivate(BuildContext context);
        void OnDeactivate(BuildContext context);
    }
}