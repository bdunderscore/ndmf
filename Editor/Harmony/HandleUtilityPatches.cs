#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable InconsistentNaming

#endregion

namespace nadena.dev.ndmf.preview
{
    internal static class HandleUtilityPatches
    {
        internal class ShadowArrayHelper
        {
            private readonly Type ty_elem;
            private Type ty_array;
            private Array array;
            private readonly object elem_default;

            private readonly Action<object, object> m_array_fill;

            public ShadowArrayHelper(Type ty_elem)
            {
                this.ty_elem = ty_elem;
                ty_array = ty_elem.MakeArrayType();
                array = Array.CreateInstance(ty_elem, 16);
                elem_default = Activator.CreateInstance(ty_elem);
                var array_fill_expr = Expression.Call(
                    typeof(Array),
                    "Fill",
                    new[] { ty_elem },
                    Expression.Constant(array),
                    Expression.Constant(elem_default)
                );
                var lambdaExpr = Expression.Lambda<Action<object, object>>(
                    array_fill_expr,
                    Expression.Parameter(typeof(object)),
                    Expression.Parameter(typeof(object))
                );
                m_array_fill = lambdaExpr.Compile();
            }

            public Array GetShadow(Array input, Func<object, object> filter)
            {
                if (input == null) return null;

                if (array.Length < input.Length)
                {
                    array = Array.CreateInstance(ty_elem, input.Length);
                }
                else
                {
                    m_array_fill(array, elem_default);
                }

                for (var i = 0; i < input.Length; i++)
                {
                    array.SetValue(filter(input.GetValue(i)), i);
                }

                return array;
            }
            public Array Distinct(Array input)
            {
                var objArray = new object[input.Length];

                for (var i = 0; i < input.Length; i++)
                    objArray[i] = input.GetValue(i);

                objArray = objArray.Distinct().ToArray();
                array = Array.CreateInstance(ty_elem, objArray.Length);

                for (var i = 0; i < objArray.Length; i++)
                    array.SetValue(objArray[i], i);

                return array;
            }
        }

        internal static void Patch_FilterInstanceIDs(Harmony h)
        {
            var t_HandleUtility = AccessTools.TypeByName("UnityEditor.HandleUtility");
            var m_orig = AccessTools.Method(t_HandleUtility,
#if UNITY_6000_3_OR_NEWER
            "FilterEntityIds"
#else
            "FilterInstanceIDs"
#endif
            );
            var m_prefix = AccessTools.Method(typeof(HandleUtilityPatches), "Prefix_FilterInstanceIDs");
            var m_postfix = AccessTools.Method(typeof(HandleUtilityPatches), "Postfix_FilterInstanceIDs");

            h.Patch(original: m_orig, prefix: new HarmonyMethod(m_prefix), postfix: new HarmonyMethod(m_postfix));
            var m_internal_getclosestpickingid = AccessTools.Method(t_HandleUtility, "Internal_GetClosestPickingID");
            var m_prefix_internal_getclosestpickingid = AccessTools.Method(typeof(HandleUtilityPatches),
                nameof(Prefix_Internal_GetClosestPickingID));
            var m_postfix_internal_getclosestpickingid = AccessTools.Method(typeof(HandleUtilityPatches),
                nameof(Postfix_Internal_GetClosestPickingID));

            h.Patch(
                m_internal_getclosestpickingid,
                new HarmonyMethod(m_prefix_internal_getclosestpickingid),
                new HarmonyMethod(m_postfix_internal_getclosestpickingid)
            );

            var m_internal_pickrectobjects = AccessTools.Method(t_HandleUtility, "Internal_PickRectObjects");
            var m_postfix_internal_pickrectobjects = AccessTools.Method(typeof(HandleUtilityPatches),
                nameof(Postfix_Internal_PickRectObjects));

            h.Patch(
                m_internal_pickrectobjects,
                postfix: new HarmonyMethod(m_postfix_internal_pickrectobjects)
            );
        }

        private static readonly Type ty_PickingObject = AccessTools.TypeByName("UnityEditor.PickingObject");
        private static readonly ShadowArrayHelper shadow_ignore = new(ty_PickingObject);
        private static readonly ShadowArrayHelper shadow_filter = new(ty_PickingObject);

        private static readonly ConstructorInfo m_PickingObject_ctor =
#if UNITY_6000_5_OR_NEWER
            AccessTools.Constructor(ty_PickingObject, new[] { typeof(EntityId), typeof(int) });
#else
            AccessTools.Constructor(ty_PickingObject, new[] { typeof(Object), typeof(int) });
#endif

        private static readonly MethodInfo m_PickingObject_Target =
            AccessTools.PropertyGetter(ty_PickingObject,
#if UNITY_6000_5_OR_NEWER
            "targetId"
#else
            "target"
#endif
            );

        private static readonly MethodInfo m_PickingObject_MaterialIndex =
            AccessTools.PropertyGetter(ty_PickingObject, "materialIndex");
        [UsedImplicitly]
        private static void Prefix_Internal_GetClosestPickingID(
            Camera cam,
            int layers,
            Vector2 position,
            ref object ignore, // PickingObject[]
            ref object filter, // PickingObject[]
            bool drawGizmos,
            ref int materialIndex,
            ref bool isEntity
        )
        {
            var sess = PreviewSession.Current;
            if (sess == null) return;

            Func<object, object> obj_filter = obj =>
            {
                if (obj == null) return null;
                var target = m_PickingObject_Target.Invoke(obj, null);
                if (target is not GameObject go) return obj;

                if (sess.OriginalToProxyObject.TryGetValue(go, out var proxy) && proxy != null)
                {
                    return m_PickingObject_ctor.Invoke(new[]
                        {
#if UNITY_6000_5_OR_NEWER
                            go.GetEntityId()
#else
                            proxy
#endif
                        , m_PickingObject_MaterialIndex.Invoke(obj, null) });
                }

                return obj;
            };

            ignore = shadow_ignore.GetShadow((Array)ignore, obj_filter);
            filter = shadow_filter.GetShadow((Array)filter, obj_filter);
        }


        [UsedImplicitly]
        private static void Postfix_Internal_GetClosestPickingID(
            Camera cam,
            int layers,
            Vector2 position,
            object ignore, // PickingObject[]
            object filter, // PickingObject[]
            bool drawGizmos,
            ref int materialIndex,
            ref bool isEntity,
#if UNITY_6000_3_OR_NEWER
            ref ulong __result
#else
            ref uint __result
#endif
        )
        {
            var sess = PreviewSession.Current;
            if (sess == null) return;

            var go =
#if UNITY_6000_4_OR_NEWER
            EditorUtility.EntityIdToObject(EntityId.FromULong(__result))
#elif UNITY_6000_3_OR_NEWER
            EditorUtility.EntityIdToObject((EntityId)(int)__result)
#else
            EditorUtility.InstanceIDToObject((int)__result)
#endif
             as GameObject;
            if (go == null) return;

#if UNITY_6000_5_OR_NEWER
            // ここで ProxyToOriginalObject をしてしまうと GetAllOverlapping のコードから`GetAllOverlapping failed, could not ignore game object ' ... ' when picking` が発生する。
            // これをしないほうが正常に動くため ... 何故なのかよくわからず謎。まぁ こんな強引なパッチはそんな程度でもいいでしょう ()
            // By Reina_Sakiria
#else
            if (sess.ProxyToOriginalObject.TryGetValue(go, out var original) && original != null)
            {
                __result =
#if UNITY_6000_4_OR_NEWER
               EntityId.ToULong(original.GetEntityId())
#else
                (uint)original.GetInstanceID()
#endif
                ;
            }
#endif
        }

        [UsedImplicitly]
        private static void Postfix_Internal_PickRectObjects(
            Camera cam,
            Rect rect,
            bool selectPrefabRoots,
            bool drawGizmos,
            ref GameObject[] __result
        )
        {
            if (__result == null) return;

            var sess = PreviewSession.Current;
            if (sess == null) return;

            for (var i = 0; i < __result.Length; i++)
            {
                if (sess.ProxyToOriginalObject.TryGetValue(__result[i], out var original) && original != null)
                {
                    __result[i] = original;
                }
            }
        }

#if UNITY_6000_3_OR_NEWER
        [UsedImplicitly]
        private static bool Prefix_FilterInstanceIDs(
            ref IEnumerable<GameObject> gameObjects,
            out EntityId[] parentEntityIds,
            out EntityId[] childEntityIds,
            out HashSet<EntityId> childEntityIdsHashSet
        )
        {
            gameObjects = RemapObjects(gameObjects);
            parentEntityIds = childEntityIds = null;
            childEntityIdsHashSet = null;
            return true;
        }
#else
        [UsedImplicitly]
        private static bool Prefix_FilterInstanceIDs(
            ref IEnumerable<GameObject> gameObjects,
            out int[] parentInstanceIDs,
            out int[] childInstanceIDs
        )
        {
            gameObjects = RemapObjects(gameObjects);
            parentInstanceIDs = childInstanceIDs = null;
            return true;
        }
#endif

#if UNITY_6000_3_OR_NEWER

        [UsedImplicitly]
        private static void Postfix_FilterInstanceIDs(
            ref IEnumerable<GameObject> gameObjects,
            ref EntityId[] parentEntityIds,
            ref EntityId[] childEntityIds,
            ref HashSet<EntityId> childEntityIdsHashSet
        )
        {
            ref var parentInstanceIDs = ref parentEntityIds;
            ref var childInstanceIDs = ref childEntityIds;
            childEntityIdsHashSet = null;
#else
        [UsedImplicitly]
        private static void Postfix_FilterInstanceIDs(
            ref IEnumerable<GameObject> gameObjects,
            ref int[] parentInstanceIDs,
            ref int[] childInstanceIDs
        )
        {
            HashSet<int> childEntityIdsHashSet = null;
#endif
            var sess = PreviewSession.Current;
            if (sess == null) return;



            foreach (var parent in gameObjects)
            {
                foreach (var renderer in parent.GetComponentsInChildren<Renderer>())
                {
                    if (sess.OriginalToProxyRenderer.TryGetValue(renderer, out var proxy) && proxy != null)
                    {
                        if (childEntityIdsHashSet == null) childEntityIdsHashSet = new(childInstanceIDs);
                        childEntityIdsHashSet.Add(
#if UNITY_6000_3_OR_NEWER
                            proxy.GetEntityId()
#else
                            proxy.GetInstanceID()
#endif
                        );
                    }
                }
            }

            if (childEntityIdsHashSet != null)
            {
                childInstanceIDs = childEntityIdsHashSet.ToArray();
            }
        }

        private static IEnumerable<GameObject> RemapObjects(IEnumerable<GameObject> objs)
        {
            var sess = PreviewSession.Current;
            if (sess == null) return objs;

            return objs.Select(
                obj =>
                {
                    if (obj == null) return obj;
                    if (sess.OriginalToProxyObject.TryGetValue(obj, out var proxy) && proxy != null)
                    {
                        return proxy.gameObject;
                    }
                    else
                    {
                        return obj;
                    }
                }
            ).ToArray();
        }
    }
}
