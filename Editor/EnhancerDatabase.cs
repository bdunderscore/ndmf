#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf
{
    internal class EnhancerDatabase<T, Interface> where T : Attribute
    {
        internal delegate Interface Creator(Component c);

        private static readonly PropertyInfo forTypeProp = typeof(T).GetProperty("ForType");
        private static readonly Task<EnhancerDatabase<T, Interface>> TaskDatabase = Task.Run(() => new EnhancerDatabase<T, Interface>());

        private readonly IImmutableDictionary<Type, Creator> _attributes = FindAttributes();
        private Dictionary<Type, Creator> _resolved;
        
        private EnhancerDatabase()
        {
            _resolved = new Dictionary<Type, Creator>();
        }
        
        
        private static IImmutableDictionary<Type, Creator> FindAttributes()
        {
            var builder = ImmutableDictionary.CreateBuilder<Type, Creator>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attribute = type.GetCustomAttribute<T>();
                    if (attribute != null)
                    {
                        TryConfigureAttribute(ref builder, type, attribute);
                    }
                }
            }

            return builder.ToImmutable();
        }

        private static void TryConfigureAttribute(ref ImmutableDictionary<Type, Creator>.Builder builder, Type type,
            T attribute)
        {
            var forType = forTypeProp.GetValue(attribute) as Type;
            if (forType == null)
            {
                Debug.LogError($"Attribute {attribute} on {type} does not have a valid ForType property");
                return;
            }

            if (builder.ContainsKey(forType))
            {
                Debug.LogError($"{forType} is referenced by multiple " + attribute.GetType() +
                               " annotations (one of which is " + type + ")");
                return;
            }

            var ctor = type.GetConstructor(new Type[] { forType });
            if (ctor == null)
            {
                Debug.LogError($"{type} does not have a valid constructor taking " + forType);
                return;
            }

            var delegateParam = Expression.Parameter(typeof(Component), "c");
            var cast = Expression.Convert(delegateParam, forType);
            var lambda = Expression.Lambda<Creator>(Expression.New(ctor, cast), delegateParam);
            builder.Add(forType, lambda.Compile());
        }

        public static bool Query(Component c, out Interface iface)
        {
            iface = default;
            if (!TaskDatabase.Result.DoQuery(c, out var creator)) return false;

            iface = creator(c);

            return true;
        }
        
        private bool DoQuery(Component c, out Creator creator)
        {
            creator = default;

            if (c == null) return false;
            
            if (_resolved.TryGetValue(c.GetType(), out creator))
            {
                return true;
            }

            var tmp = WalkTypeTree(c.GetType())
                .Where((kvp) => _attributes.ContainsKey(kvp.Item1)).ToList();
            
            // Perform breadth-first search on base classes and interfaces, prioritizing more specific declarations.
            using (var it = WalkTypeTree(c.GetType())
                       .Where((kvp) => _attributes.ContainsKey(kvp.Item1))
                       .OrderBy((kvp) => kvp.Item2)
                       .Take(2)
                       .GetEnumerator())
            {

                if (!it.MoveNext()) return false;
                var first = it.Current;
                if (it.MoveNext() && it.Current.Item2 == first.Item2)
                {
                    Debug.LogError("Multiple candidate " + typeof(T) +
                                        " attributes found for base types and interfaces of " + c.GetType());
                    return false;
                }

                creator = _attributes[first.Item1];
                _resolved[c.GetType()] = creator; // cache resolved type
            }
            

            return true;
        }
        
        private IEnumerable<(Type, int)> WalkTypeTree(Type type)
        {
            int depth = 0;
            while (type != null)
            {
                yield return (type, depth);
            
                foreach (var i in type.GetInterfaces())
                {
                    yield return (i, depth + 1);
                }

                if (type.BaseType == type) break;
                
                type = type.BaseType;
                depth++;
            }
        }
        

        public static void AsyncInit()
        {
            // static initializer does the work
        }
    }
}