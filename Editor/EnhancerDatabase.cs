#region

using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf
{
    internal static class EnhancerDatabase<T, Interface> where T : Attribute
    {
        internal delegate Interface Creator(Component c);

        private static readonly PropertyInfo forTypeProp = typeof(T).GetProperty("ForType");
        private static readonly Task<IImmutableDictionary<Type, Creator>> TaskDatabase = Task.Run(Init);

        private static IImmutableDictionary<Type, Creator> Init()
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

        public static IImmutableDictionary<Type, Creator> Mappings
        {
            get
            {
                TaskDatabase.Wait();
                return TaskDatabase.Result;
            }
        }

        public static bool Query(Component c, out Interface iface)
        {
            if (!Mappings.TryGetValue(c.GetType(), out var creator))
            {
                iface = default;
                return false;
            }

            iface = creator(c);
            return true;
        }

        public static void AsyncInit()
        {
            // static initializer does the work
        }
    }
}