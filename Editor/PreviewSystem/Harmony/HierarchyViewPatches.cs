#region

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.preview
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class HierarchyViewPatches
    {
        private static readonly Type t_HierarchyProperty = AccessTools.TypeByName("UnityEditor.HierarchyProperty");
        private static readonly PropertyInfo p_pptrValue = AccessTools.Property(t_HierarchyProperty, "pptrValue");
        private static readonly PropertyInfo p_rowIndex = AccessTools.Property(t_HierarchyProperty, "row");

        private static FieldInfo f_m_Rows; // List<TreeViewItem>
        private static FieldInfo f_m_RowCount; // int
        private static FieldInfo f_m_SearchString; // string
        private static PropertyInfo p_objectPPTR;

        /// <summary>
        ///     For each GameObjectTreeViewDataSource, we maintain a virtual mapping of HierarchyProperties to
        ///     row indexes. This is then used in GameObjectTreeViewDataSource.GetRow to return the row index,
        ///     ignoring skipped rows.
        /// </summary>
        private static readonly ConditionalWeakTable<object, Dictionary<int, int>> _rowIndexRemap = new();

        internal static void Patch(Harmony h)
        {
#if MODULAR_AVATAR_DEBUG_HIDDEN
            return;
#endif
            var t_GameObjectTreeViewDataSource = AccessTools.TypeByName("UnityEditor.GameObjectTreeViewDataSource");
            var t_GameObjectTreeViewItem = AccessTools.TypeByName("UnityEditor.GameObjectTreeViewItem");

            f_m_Rows = t_GameObjectTreeViewDataSource.GetField("m_Rows",
                BindingFlags.NonPublic | BindingFlags.Instance);
            f_m_RowCount =
                t_GameObjectTreeViewDataSource.GetField("m_RowCount", BindingFlags.NonPublic | BindingFlags.Instance);
            f_m_SearchString =
                t_GameObjectTreeViewDataSource.GetField("m_SearchString",
                    BindingFlags.NonPublic | BindingFlags.Instance);

            p_objectPPTR = t_GameObjectTreeViewItem.GetProperty("objectPPTR");

            var m_orig = AccessTools.Method(t_GameObjectTreeViewDataSource, "InitTreeViewItem",
                new[]
                {
                    t_GameObjectTreeViewItem,
                    typeof(int),
                    typeof(Scene),
                    typeof(bool),
                    typeof(int),
                    typeof(Object),
                    typeof(bool),
                    typeof(int)
                });
            var m_patch = AccessTools.Method(typeof(HierarchyViewPatches), nameof(Prefix_InitTreeViewItem));

            h.Patch(original: m_orig, prefix: new HarmonyMethod(m_patch));

            var m_InitRows = AccessTools.Method(t_GameObjectTreeViewDataSource, "InitializeRows");
            var m_transpiler = AccessTools.Method(typeof(HierarchyViewPatches), "Transpile_InitializeRows");

            h.Patch(original: m_InitRows,
                transpiler: new HarmonyMethod(m_transpiler),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(HierarchyViewPatches), "Postfix_InitializeRows"))
            );

            var m_GetRow = AccessTools.Method(t_GameObjectTreeViewDataSource, "GetRow");
            h.Patch(m_GetRow,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(HierarchyViewPatches), nameof(Postfix_GetRow))));
        }

        private static int skipped = 0;

        [UsedImplicitly]
        private static void Postfix_GetRow(object __instance, int id, ref int __result)
        {
            var cache = _rowIndexRemap.GetOrCreateValue(__instance);
            var searchString = (string)f_m_SearchString.GetValue(__instance);

            if (!string.IsNullOrEmpty(searchString))
                // GetRow calls the underlying TreeValueDataSource routine which just loops through all rows (safely)
                return;

            if (cache.TryGetValue(__result, out var newResult)) __result = newResult;
        }
        
        [UsedImplicitly]
        private static void Postfix_InitializeRows(object __instance)
        {
            var rows = (IList<TreeViewItem>)f_m_Rows.GetValue(__instance);

            var rowCount = (int)f_m_RowCount.GetValue(__instance);

            f_m_RowCount.SetValue(__instance, rowCount - skipped);

            for (int i = 0; i < skipped; i++)
            {
                rows.RemoveAt(rows.Count - 1);
            }
        }

        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpile_InitializeRows(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            foreach (var c in Transpile_InitializeRows0(instructions, generator))
            {
                //Debug.Log(c);
                yield return c;
            }
        }

        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpile_InitializeRows0(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            var m_shouldLoop = AccessTools.Method(typeof(HierarchyViewPatches), "ShouldLoop");
            var m_Next = AccessTools.Method(t_HierarchyProperty, "Next", new[] { typeof(int[]) });

            var cache_arg = generator.DeclareLocal(typeof(Dictionary<int, int>));
            yield return new CodeInstruction(OpCodes.Ldarg_0); // this
            yield return new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(HierarchyViewPatches), nameof(InitializeRows_Entry)));
            yield return new CodeInstruction(OpCodes.Stloc, cache_arg);
            
            foreach (var c in instructions)
            {
                if (c.Is(OpCodes.Callvirt, m_Next))
                {
                    var loopLabel = generator.DefineLabel();
                    var stash_arg = generator.DeclareLocal(typeof(int[]));
                    var stash_obj = generator.DeclareLocal(t_HierarchyProperty);

                    yield return new CodeInstruction(OpCodes.Stloc, stash_arg);
                    yield return new CodeInstruction(OpCodes.Stloc, stash_obj);

                    var tmp = new CodeInstruction(OpCodes.Ldloc, stash_obj);
                    tmp.labels.Add(loopLabel);
                    yield return tmp;
                    
                    yield return new CodeInstruction(OpCodes.Ldloc, stash_arg);
                    yield return new CodeInstruction(OpCodes.Call, m_Next);

                    // Check if this item should be ignored.
                    yield return new CodeInstruction(OpCodes.Ldloc, cache_arg);
                    yield return new CodeInstruction(OpCodes.Ldloc, stash_obj);
                    yield return new CodeInstruction(OpCodes.Call, m_shouldLoop);
                    yield return new CodeInstruction(OpCodes.Brtrue_S, loopLabel);
                }
                else
                {
                    yield return c;
                }
            }
        }

        [UsedImplicitly]
        private static Dictionary<int, int> InitializeRows_Entry(object self)
        {
            skipped = 0;
            var rowCache = _rowIndexRemap.GetOrCreateValue(self);
            rowCache.Clear();

            return rowCache;
        }

        [UsedImplicitly]
        private static bool ShouldLoop(Dictionary<int, int> rowIndexCache, HierarchyProperty hierarchyProperty)
        {
            var sess = PreviewSession.Current;
            if (sess == null) return false;

            if (hierarchyProperty == null) return false;

            var rowIndex = (int)p_rowIndex.GetValue(hierarchyProperty);
            rowIndexCache[rowIndex] = rowIndex - skipped;
            
            var pptrValue = p_pptrValue.GetValue(hierarchyProperty);
            if (pptrValue == null) return false;

            var skip = ProxyObjectController.IsProxyObject(pptrValue as GameObject);
            if (skip) skipped++;

            return skip;
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private static bool Prefix_InitTreeViewItem(
            object __instance,
            ref object item,
            int itemID,
            Scene scene,
            bool isSceneHeader,
            int colorCode,
            Object pptrObject,
            ref bool hasChildren,
            int depth
        )
        {
            var sess = PreviewSession.Current;
            if (sess == null) return true;

            if (pptrObject == null || isSceneHeader) return true;

            if (hasChildren && sess.ProxyToOriginalObject.ContainsKey((GameObject)pptrObject))
            {
                // See if there are any other children...
                hasChildren = ((GameObject)pptrObject).transform.childCount > 1;
            }

            return true;
        }
    }
}