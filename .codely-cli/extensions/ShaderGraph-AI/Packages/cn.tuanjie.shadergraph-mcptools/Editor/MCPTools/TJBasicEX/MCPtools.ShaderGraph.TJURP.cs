using Codely.Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityTcp.Editor.Tools;

namespace UnityTcp.ShaderTools
{
#if URP_INSTALLED
    /// <summary>
    /// URP ShaderGraph 扩展工具集 —— 操作 URP SubTarget 的 Keywords、Passes、Variants。
    /// 通过反射访问 UniversalLitSubTarget / UniversalUnlitSubTarget 的内部数据。
    /// 所有方法必须标记 public static 以便 ExecuteCustomTool 发现。
    /// </summary>
    public static class MCPtoolsShaderGraphURP
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // ================================================================
        //  Helpers
        // ================================================================

        private static object RequireGraph()
        {
            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return null;
            return SGReflection.GetGraphFromWindow(window);
        }

        private static object GetURPSubTarget(object graph)
        {
            var ut = SGReflection.GetUniversalTarget(graph);
            if (ut == null) return null;
            return SGReflection.Prop(ut, "activeSubTarget");
        }

        private static List<string> ReadStringList(object target, string fieldName)
        {
            var list = target.GetType().GetField(fieldName, All)?.GetValue(target) as IList;
            var result = new List<string>();
            if (list != null) foreach (var item in list) result.Add(item?.ToString() ?? "");
            return result;
        }

        private static List<bool> ReadBoolList(object target, string fieldName)
        {
            var list = target.GetType().GetField(fieldName, All)?.GetValue(target) as IList;
            var result = new List<bool>();
            if (list != null) foreach (var item in list) result.Add((bool)item);
            return result;
        }

        private static List<string> ReadPotentialNames(Type subTargetType, string fieldName)
        {
            var list = subTargetType.GetField(fieldName, All)?.GetValue(null) as IList;
            var result = new List<string>();
            if (list != null) foreach (var item in list) result.Add(item?.ToString() ?? "");
            return result;
        }

        private static void RegisterUndo(object graph, string message)
        {
            var owner = SGReflection.Prop(graph, "owner");
            SGReflection.CacheMethod(owner.GetType(), "RegisterCompleteObjectUndo")?.Invoke(owner, new object[] { message });
        }

        private static void RefreshAfterChange(object graph)
        {
            SGReflection.CacheMethod(SGReflection.T_GraphData, "ValidateGraph")?.Invoke(graph, null);
            MCPtoolsGraphSettings.RefreshInspector(new JObject());
        }

        // ================================================================
        //  shader_graph_urp_get_keywords
        // ================================================================

        /// <summary>
        /// 获取 URP SubTarget 的 Keywords 列表及启用状态。
        /// 返回每个 keyword 的名称、启用状态、是否被移除。
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_urp_get_keywords", "获取 URP SubTarget 的 Keywords 列表及状态")]
        public static object GetKeywords(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var sub = GetURPSubTarget(graph);
            if (sub == null) return new { success = false, message = "No URP SubTarget active" };

            var subType = sub.GetType();
            var keywordsStatus = ReadBoolList(sub, "keywordsStatus");
            var potentialKeywords = ReadPotentialNames(subType, "PotentialKeywords");
            var removedVariants = ReadStringList(sub, "m_RemovedVariants");

            var keywords = new List<object>();
            for (int i = 0; i < potentialKeywords.Count; i++)
            {
                var name = potentialKeywords[i];
                bool enabled = i < keywordsStatus.Count && keywordsStatus[i];
                bool removed = removedVariants.Contains(name);
                keywords.Add(new { name, enabled, removed });
            }

            return new
            {
                success = true,
                subTarget = subType.Name,
                keywordCount = keywords.Count,
                keywords
            };
        }

        // ================================================================
        //  shader_graph_urp_set_keywords
        // ================================================================

        /// <summary>
        /// 设置 URP SubTarget 的 Keywords 启用/禁用状态。
        /// 参数:
        ///   enableKeywords - string[] 要启用的 keyword 名称列表
        ///   disableKeywords - string[] 要禁用的 keyword 名称列表
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_urp_set_keywords", "设置 URP SubTarget 的 Keywords 启用/禁用")]
        public static object SetKeywords(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var sub = GetURPSubTarget(graph);
            if (sub == null) return new { success = false, message = "No URP SubTarget active" };

            var subType = sub.GetType();
            var potentialKeywords = ReadPotentialNames(subType, "PotentialKeywords");
            var keywordsStatus = ReadBoolList(sub, "keywordsStatus");

            var enableNames = parameters["enableKeywords"]?.Values<string>().ToList() ?? new List<string>();
            var disableNames = parameters["disableKeywords"]?.Values<string>().ToList() ?? new List<string>();

            RegisterUndo(graph, "Change Keywords");
            var changes = new List<string>();

            for (int i = 0; i < potentialKeywords.Count && i < keywordsStatus.Count; i++)
            {
                var name = potentialKeywords[i];
                if (enableNames.Contains(name) && !keywordsStatus[i])
                {
                    keywordsStatus[i] = true;
                    changes.Add($"enabled: {name}");
                }
                else if (disableNames.Contains(name) && keywordsStatus[i])
                {
                    keywordsStatus[i] = false;
                    changes.Add($"disabled: {name}");
                }
            }

            // Write back
            var field = subType.GetField("keywordsStatus", All);
            if (field != null)
            {
                var newList = (IList)Activator.CreateInstance(field.FieldType);
                foreach (var b in keywordsStatus) newList.Add(b);
                field.SetValue(sub, newList);
            }

            RefreshAfterChange(graph);

            return new { success = true, message = $"Changed {changes.Count} keywords", changes };
        }

        // ================================================================
        //  shader_graph_urp_get_passes
        // ================================================================

        /// <summary>
        /// 获取 URP SubTarget 的 Passes 列表及启用状态。
        /// 返回每个 pass 的名称、启用状态、是否被移除。
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_urp_get_passes", "获取 URP SubTarget 的 Passes 列表及状态")]
        public static object GetPasses(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var sub = GetURPSubTarget(graph);
            if (sub == null) return new { success = false, message = "No URP SubTarget active" };

            var subType = sub.GetType();
            var passStatus = ReadBoolList(sub, "passStatus");
            var potentialPasses = ReadPotentialNames(subType, "PotentialPassList");
            var removedPasses = ReadStringList(sub, "m_RemovedPasses");

            var passes = new List<object>();
            for (int i = 0; i < potentialPasses.Count; i++)
            {
                var name = potentialPasses[i];
                bool enabled = i < passStatus.Count && passStatus[i];
                bool removed = removedPasses.Contains(name);
                passes.Add(new { name, enabled, removed });
            }

            return new
            {
                success = true,
                subTarget = subType.Name,
                passCount = passes.Count,
                passes
            };
        }

        // ================================================================
        //  shader_graph_urp_set_passes
        // ================================================================

        /// <summary>
        /// 设置 URP SubTarget 的 Passes 启用/禁用状态。
        /// 参数:
        ///   enablePasses - string[] 要启用的 pass 名称列表
        ///   disablePasses - string[] 要禁用的 pass 名称列表
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_urp_set_passes", "设置 URP SubTarget 的 Passes 启用/禁用")]
        public static object SetPasses(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var sub = GetURPSubTarget(graph);
            if (sub == null) return new { success = false, message = "No URP SubTarget active" };

            var subType = sub.GetType();
            var potentialPasses = ReadPotentialNames(subType, "PotentialPassList");
            var passStatus = ReadBoolList(sub, "passStatus");

            var enableNames = parameters["enablePasses"]?.Values<string>().ToList() ?? new List<string>();
            var disableNames = parameters["disablePasses"]?.Values<string>().ToList() ?? new List<string>();

            RegisterUndo(graph, "Change Passes");
            var changes = new List<string>();

            for (int i = 0; i < potentialPasses.Count && i < passStatus.Count; i++)
            {
                var name = potentialPasses[i];
                if (enableNames.Contains(name) && !passStatus[i])
                {
                    passStatus[i] = true;
                    changes.Add($"enabled: {name}");
                }
                else if (disableNames.Contains(name) && passStatus[i])
                {
                    passStatus[i] = false;
                    changes.Add($"disabled: {name}");
                }
            }

            // Write back
            var field = subType.GetField("passStatus", All);
            if (field != null)
            {
                var newList = (IList)Activator.CreateInstance(field.FieldType);
                foreach (var b in passStatus) newList.Add(b);
                field.SetValue(sub, newList);
            }

            RefreshAfterChange(graph);

            return new { success = true, message = $"Changed {changes.Count} passes", changes };
        }

        // ================================================================
        //  shader_graph_urp_get_removed_variants
        // ================================================================

        /// <summary>
        /// 获取 URP SubTarget 被移除的 Variants（Keywords）和 Passes 列表。
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_urp_get_removed_variants", "获取 URP SubTarget 被移除的 Variants 和 Passes")]
        public static object GetRemovedVariants(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var sub = GetURPSubTarget(graph);
            if (sub == null) return new { success = false, message = "No URP SubTarget active" };

            var removedVariants = ReadStringList(sub, "m_RemovedVariants");
            var removedPasses = ReadStringList(sub, "m_RemovedPasses");

            return new
            {
                success = true,
                subTarget = sub.GetType().Name,
                removedVariants,
                removedVariantCount = removedVariants.Count,
                removedPasses,
                removedPassCount = removedPasses.Count
            };
        }

        // ================================================================
        //  shader_graph_urp_set_removed_variants
        // ================================================================

        /// <summary>
        /// 设置 URP SubTarget 被移除的 Variants 和 Passes。
        /// 参数:
        ///   addRemovedVariants - string[] 添加到移除列表的 variant 名称
        ///   removeRemovedVariants - string[] 从移除列表中恢复的 variant 名称
        ///   addRemovedPasses - string[] 添加到移除列表的 pass 名称
        ///   removeRemovedPasses - string[] 从移除列表中恢复的 pass 名称
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_urp_set_removed_variants", "设置 URP SubTarget 的 Removed Variants 和 Passes")]
        public static object SetRemovedVariants(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var sub = GetURPSubTarget(graph);
            if (sub == null) return new { success = false, message = "No URP SubTarget active" };

            RegisterUndo(graph, "Change Removed Variants/Passes");
            var changes = new List<string>();

            // --- Variants ---
            var addVariants = parameters["addRemovedVariants"]?.Values<string>().ToList() ?? new List<string>();
            var removeVariants = parameters["removeRemovedVariants"]?.Values<string>().ToList() ?? new List<string>();

            if (addVariants.Count > 0 || removeVariants.Count > 0)
            {
                var currentVariants = ReadStringList(sub, "m_RemovedVariants");
                foreach (var v in addVariants)
                {
                    if (!currentVariants.Contains(v))
                    {
                        currentVariants.Add(v);
                        changes.Add($"removed variant: {v}");
                    }
                }
                foreach (var v in removeVariants)
                {
                    if (currentVariants.Remove(v))
                        changes.Add($"restored variant: {v}");
                }

                var rvField = sub.GetType().GetField("m_RemovedVariants", All);
                if (rvField != null)
                {
                    var newList = (IList)Activator.CreateInstance(rvField.FieldType);
                    foreach (var s in currentVariants) newList.Add(s);
                    rvField.SetValue(sub, newList);
                }
            }

            // --- Passes ---
            var addPasses = parameters["addRemovedPasses"]?.Values<string>().ToList() ?? new List<string>();
            var removePasses = parameters["removeRemovedPasses"]?.Values<string>().ToList() ?? new List<string>();

            if (addPasses.Count > 0 || removePasses.Count > 0)
            {
                var currentPasses = ReadStringList(sub, "m_RemovedPasses");
                foreach (var p in addPasses)
                {
                    if (!currentPasses.Contains(p))
                    {
                        currentPasses.Add(p);
                        changes.Add($"removed pass: {p}");
                    }
                }
                foreach (var p in removePasses)
                {
                    if (currentPasses.Remove(p))
                        changes.Add($"restored pass: {p}");
                }

                var rpField = sub.GetType().GetField("m_RemovedPasses", All);
                if (rpField != null)
                {
                    var newList = (IList)Activator.CreateInstance(rpField.FieldType);
                    foreach (var s in currentPasses) newList.Add(s);
                    rpField.SetValue(sub, newList);
                }
            }

            RefreshAfterChange(graph);

            return new { success = true, message = $"Changed {changes.Count} items", changes };
        }
    }
#endif // URP_INSTALLED
}
