#if NDMF_VRCSDK3_AVATARS
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.UnitTestSupport;

namespace UnitTests.Parameters
{
    [ParameterProviderFor(typeof(ParamTestComponent))]
    internal class ParamTestComponentProvider : IParameterProvider
    {
        public delegate ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> RemapParametersDelegate(
            ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap, BuildContext context = null);
        
        public delegate IEnumerable<ProvidedParameter> GetSuppliedParametersDelegate(BuildContext context = null);

        private static IDictionary<ParamTestComponent, RemapParametersDelegate> _remapper =
            new Dictionary<ParamTestComponent, RemapParametersDelegate>();
        
        private static IDictionary<ParamTestComponent, GetSuppliedParametersDelegate> _supplier =
            new Dictionary<ParamTestComponent, GetSuppliedParametersDelegate>();

        internal static void ClearAll()
        {
            _remapper.Clear();
            _supplier.Clear();
        } 
        
        public static void SetRemapper(ParamTestComponent component, RemapParametersDelegate remapper)
        {
            _remapper[component] = remapper;
        }
        
        public static void SetParameters(ParamTestComponent component, GetSuppliedParametersDelegate supplier)
        {
            _supplier[component] = supplier;
        }
        
        public static void SetParameters(ParamTestComponent component, params ProvidedParameter[] parameters)
        {
            _supplier[component] = _ => parameters.Select(p => p.Clone());
        }
        
        private readonly ParamTestComponent _component;
        
        public ParamTestComponentProvider(ParamTestComponent component)
        {
            _component = component;
        }
        
        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context = null)
        {
            return _supplier.TryGetValue(_component, out var supplier) ? supplier(context) : Array.Empty<ProvidedParameter>(); 
        }
        
        public void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap, BuildContext context = null)
        {
            if (_remapper.TryGetValue(_component, out var remapper))
            {
                nameMap = remapper(nameMap, context);
            }
        }
    }
}
#endif