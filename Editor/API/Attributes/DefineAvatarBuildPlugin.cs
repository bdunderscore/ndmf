using System;

namespace nadena.dev.Av3BuildFramework
{
    [System.AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public class DefineAvatarBuildPlugin : System.Attribute
    {
        private readonly string _qualifiedName;
        private readonly Type _type;

        public DefineAvatarBuildPlugin(string _qualifiedName, Type type)
        {
            this._qualifiedName = _qualifiedName;
            this._type = type;
        }

        public string QualifiedName => _qualifiedName;
        public Type Type => _type;
    }
}