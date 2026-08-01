using Codely.Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityTcp.Editor.Tools;

namespace UnityTcp.ShaderTools
{
    /// <summary>
    /// ShaderGraph Graph Inspector 设置工具集 —— 通过反射访问 GraphData / HDTarget / SubTarget 数据，
    /// 提供 Graph Settings、HDRP Target、Surface Options、Advanced Options、Lit 专属选项的读写。
    /// 所有方法必须标记 public static 以便 ExecuteCustomTool 发现。
    /// </summary>
    public static class MCPtoolsGraphSettings
    {
        // ================================================================
        //  Helpers
        // ================================================================

        private static (object window, object graph) GetWindowAndGraph()
        {
            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return (null, null);
            var graph = SGReflection.GetGraphFromWindow(window);
            return (window, graph);
        }

        private static object RequireGraph()
        {
            var (_, graph) = GetWindowAndGraph();
            return graph;
        }

        private static void RegisterUndo(object graph, string message)
        {
            var owner = SGReflection.Prop(graph, "owner");
            SGReflection.CacheMethod(owner.GetType(), "RegisterCompleteObjectUndo")?.Invoke(owner, new object[] { message });
        }

        private static void RefreshAfterChange(object graph)
        {
            SGReflection.CacheMethod(SGReflection.T_GraphData, "ValidateGraph")?.Invoke(graph, null);
            // Force inspector rebuild after every data change
            RefreshInspector(new JObject());
        }

        private static object ParseHDRPEnum(Type enumType, string value)
        {
            if (enumType == null || string.IsNullOrEmpty(value)) return null;
            try { return Enum.Parse(enumType, value, true); }
            catch { return null; }
        }

        private static object SafeGetEnumValue(Type enumType, object dataObj, string propName)
        {
            if (enumType == null || dataObj == null) return null;
            var val = SGReflection.Prop(dataObj, propName);
            return val?.ToString();
        }

        // ================================================================
        //  Inspector Refresh
        // ================================================================

        /// <summary>
        /// 强制刷新 ShaderGraph Inspector 面板，使数据修改在 UI 上可见。
        /// 在调用其他 set_ 工具后，如果 UI 没有立即更新，可手动调用此工具。
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_refresh_inspector", "强制刷新 Graph Inspector 面板，使修改在 UI 上可见")]
        public static object RefreshInspector(JObject parameters)
        {
            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };

            // 1. Set doesInspectorNeedUpdate = true on InspectorView
            var gev = SGReflection.Prop(window, "graphEditorView");
            if (gev != null)
            {
                var inspectorViewField = SGReflection.CacheField(gev.GetType(), "m_InspectorView")
                    ?? gev.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .FirstOrDefault(f => f.FieldType.Name == "InspectorView");
                if (inspectorViewField != null)
                {
                    var inspectorView = inspectorViewField.GetValue(gev);
                    if (inspectorView != null)
                    {
                        var needUpdateProp = inspectorView.GetType().GetProperty("doesInspectorNeedUpdate",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        needUpdateProp?.SetValue(inspectorView, true);
                    }
                }
            }

            // 2. Trigger window Update to process graph changes and rebuild inspector
            var updateMethod = window.GetType().GetMethod("Update",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            updateMethod?.Invoke(window, new object[0]);

            // 3. Repaint
            (window as UnityEditor.EditorWindow)?.Repaint();

            return new { success = true, message = "Inspector refreshed" };
        }

        // ================================================================
        //  Region 1: Graph Settings (Precision, Active Targets)
        // ================================================================

        #region Graph Settings

        /// <summary>
        /// 获取当前 Shader Graph 的 Graph Settings
        /// 包括 Precision、Active Targets、SubTarget 类型、isSubGraph
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_get_graph_settings", "获取当前 Shader Graph 的 Graph Settings（Precision、Targets 等）")]
        public static object GetGraphSettings(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var precision = SGReflection.Prop(graph, "graphDefaultPrecision")?.ToString();
            var isSubGraph = (bool)SGReflection.Prop(graph, "isSubGraph");

            var activeTargets = new List<object>();
            foreach (var t in (System.Collections.IEnumerable)SGReflection.Prop(graph, "activeTargets"))
            {
                if (t == null) continue;
                var targetName = t.GetType().Name;
                var displayName = (string)SGReflection.Prop(t, "displayName");

                // Check if it's HDTarget
#if HDRP_INSTALLED
                bool isHDTarget = SGReflection.T_HDTarget != null && SGReflection.T_HDTarget.IsAssignableFrom(t.GetType());
#else
                bool isHDTarget = false;
#endif
                string subTargetName = null;
                string materialType = null;

#if HDRP_INSTALLED
                if (isHDTarget)
                {
                    var sub = SGReflection.GetActiveSubTarget(t);
                    if (sub != null)
                    {
                        subTargetName = sub.GetType().Name;
                        materialType = (string)SGReflection.Prop(sub, "displayName");
                    }
                }
#endif

                activeTargets.Add(new
                {
                    typeName = targetName,
                    displayName,
                    isHDTarget,
                    subTarget = subTargetName,
                    materialType
                });
            }

            var potentialTargets = new List<object>();
            foreach (var t in (System.Collections.IEnumerable)SGReflection.Prop(graph, "allPotentialTargets"))
            {
                if (t == null) continue;
                var isActive = ((System.Collections.IEnumerable)SGReflection.Prop(graph, "activeTargets")).Cast<object>().Any(at => at.GetType() == t.GetType());
                potentialTargets.Add(new
                {
                    typeName = t.GetType().Name,
                    displayName = (string)SGReflection.Prop(t, "displayName"),
                    isActive
                });
            }

            return new
            {
                success = true,
                precision,
                isSubGraph,
                activeTargetCount = activeTargets.Count,
                activeTargets,
                potentialTargets
            };
        }

        /// <summary>
        /// 设置 Graph 的 Precision
        /// 参数:
        ///   precision - "Single" 或 "Half"
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_precision", "设置 Graph 的精度（Single/Half）")]
        public static object SetPrecision(JObject parameters)
        {
            string precision = parameters["precision"]?.ToString();

            if (string.IsNullOrEmpty(precision))
                return new { success = false, message = "precision parameter is required (Single or Half)" };

            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var parsedPrecision = SGReflection.ParseEnum(SGReflection.T_GraphPrecision, precision);
            if (parsedPrecision == null)
                return new { success = false, message = $"Invalid precision value: {precision}. Use 'Single' or 'Half'." };

            RegisterUndo(graph, "Change Precision");
            SGReflection.CacheMethod(SGReflection.T_GraphData, "SetGraphDefaultPrecision")?.Invoke(graph, new object[] { parsedPrecision });
            RefreshAfterChange(graph);

            return new { success = true, message = $"Precision set to {precision}" };
        }

        /// <summary>
        /// 激活一个 Target（添加到 Active Targets 列表）
        /// 参数:
        ///   targetTypeName - Target 类型名（如 "HDTarget"）
        ///   targetIndex - Target 在 Potential Targets 中的索引（与 targetTypeName 二选一）
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_target_active", "激活一个 Target（添加到 Active Targets）")]
        public static object SetTargetActive(JObject parameters)
        {
            string targetTypeName = parameters["targetTypeName"]?.ToString();
            int? targetIndex = parameters["targetIndex"]?.Value<int>();

            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            RegisterUndo(graph, "Add Target");

            if (targetIndex.HasValue)
            {
                // SetTargetActive(int, bool)
                var setTargetActiveIntMethod = SGReflection.T_GraphData.GetMethod("SetTargetActive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(int), typeof(bool) }, null);
                setTargetActiveIntMethod?.Invoke(graph, new object[] { targetIndex.Value, true });
            }
            else if (!string.IsNullOrEmpty(targetTypeName))
            {
                // Find target in allPotentialTargets by type name
                var potentialTargets = ((System.Collections.IEnumerable)SGReflection.Prop(graph, "allPotentialTargets")).Cast<object>().ToList();
                var target = potentialTargets.FirstOrDefault(t => t.GetType().Name.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                    return new { success = false, message = $"Target type not found in potential targets: {targetTypeName}" };

                // SetTargetActive(Target, bool)
                var setTargetActiveTargetMethod = SGReflection.T_GraphData.GetMethod("SetTargetActive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { SGReflection.T_Target, typeof(bool) }, null);
                setTargetActiveTargetMethod?.Invoke(graph, new object[] { target, true });
            }
            else
            {
                return new { success = false, message = "targetTypeName or targetIndex is required" };
            }

            RefreshAfterChange(graph);

            return new { success = true, message = $"Target activated: {targetTypeName ?? targetIndex.ToString()}" };
        }

        /// <summary>
        /// 停用一个 Target（从 Active Targets 列表移除）
        /// 参数:
        ///   targetTypeName - Target 类型名（如 "HDTarget"）
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_target_inactive", "停用一个 Target（从 Active Targets 移除）")]
        public static object SetTargetInactive(JObject parameters)
        {
            string targetTypeName = parameters["targetTypeName"]?.ToString();

            if (string.IsNullOrEmpty(targetTypeName))
                return new { success = false, message = "targetTypeName is required" };

            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var activeTargets = ((System.Collections.IEnumerable)SGReflection.Prop(graph, "activeTargets")).Cast<object>().ToList();
            var target = activeTargets.FirstOrDefault(t => t.GetType().Name.Equals(targetTypeName, StringComparison.OrdinalIgnoreCase));
            if (target == null)
                return new { success = false, message = $"Target not found in active targets: {targetTypeName}" };

            RegisterUndo(graph, "Remove Target");
            // SetTargetInactive(Target, bool)
            var setTargetInactiveMethod = SGReflection.T_GraphData.GetMethod("SetTargetInactive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { SGReflection.T_Target, typeof(bool) }, null);
            setTargetInactiveMethod?.Invoke(graph, new object[] { target, false });
            RefreshAfterChange(graph);

            return new { success = true, message = $"Target deactivated: {targetTypeName}" };
        }

        #endregion

        // ================================================================
        //  Region 2: HDRP Target Settings
        // ================================================================

#if HDRP_INSTALLED
        #region HDRP Target Settings

        /// <summary>
        /// 获取 HDRP Target 的设置（Material 类型、Custom Editor GUI、VFX 支持、Compute Vertex 支持）
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_get_hdrp_target_settings", "获取 HDRP Target 的设置")]
        public static object GetHDRPTargetSettings(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP target active in this graph" };

            var sub = SGReflection.GetActiveSubTarget(hdTarget);
            var customGUI = (string)SGReflection.Prop(hdTarget, "m_CustomEditorGUI") ?? (string)SGReflection.Field(hdTarget, "m_CustomEditorGUI") ?? "";
            var supportVFX = (bool?)SGReflection.Prop(hdTarget, "supportVFX") ?? false;
            var supportCompute = (bool?)SGReflection.Prop(hdTarget, "supportComputeForVertexSetup") ?? false;

            // Read m_SupportVFX and m_SupportComputeForVertexSetup from serialized fields
            var supportVFXField = SGReflection.CacheField(SGReflection.T_HDTarget, "m_SupportVFX")?.GetValue(hdTarget);
            var supportComputeField = SGReflection.CacheField(SGReflection.T_HDTarget, "m_SupportComputeForVertexSetup")?.GetValue(hdTarget);

            var availableSubTargets = new List<object>();
            if (SGReflection.T_HDTarget != null)
            {
                var subTargetsField = SGReflection.CacheField(SGReflection.T_HDTarget, "m_SubTargets");
                if (subTargetsField != null)
                {
                    var subTargetsList = subTargetsField.GetValue(hdTarget) as System.Collections.IList;
                    if (subTargetsList != null)
                    {
                        foreach (var st in subTargetsList)
                        {
                            availableSubTargets.Add(new
                            {
                                typeName = st.GetType().Name,
                                displayName = (string)SGReflection.Prop(st, "displayName")
                            });
                        }
                    }
                }
            }

            return new
            {
                success = true,
                activeSubTarget = sub != null ? new
                {
                    typeName = sub.GetType().Name,
                    displayName = (string)SGReflection.Prop(sub, "displayName")
                } : null,
                availableSubTargets,
                customEditorGUI = customGUI,
                supportVFX = supportVFXField is bool b1 && b1,
                supportComputeForVertexSetup = supportComputeField is bool b2 && b2
            };
        }

        /// <summary>
        /// 设置 HDRP Material 类型（切换 SubTarget，如 Lit / Unlit / Decal 等）
        /// 参数:
        ///   subTargetTypeName - SubTarget 类型名（如 "HDLitSubTarget", "HDUnlitSubTarget"）
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_hdrp_material", "设置 HDRP Material 类型（切换 SubTarget）")]
        public static object SetHDRPMaterial(JObject parameters)
        {
            string subTargetTypeName = parameters["subTargetTypeName"]?.ToString();

            if (string.IsNullOrEmpty(subTargetTypeName))
                return new { success = false, message = "subTargetTypeName is required (e.g. HDLitSubTarget, HDUnlitSubTarget)" };

            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP target active in this graph" };

            RegisterUndo(graph, "Change Material Type");

            bool success = SGReflection.TrySetActiveSubTarget(hdTarget, subTargetTypeName);
            if (!success)
                return new { success = false, message = $"Failed to set sub-target: {subTargetTypeName}" };

            // Initialize output blocks for the new SubTarget (same as URP flow)
            SGReflection.InitializeOutputBlocks(graph, hdTarget, subTargetTypeName);

            RefreshAfterChange(graph);

            return new { success = true, message = $"Material type changed to {subTargetTypeName}" };
        }

        /// <summary>
        /// 设置 HDRP Target 的 Custom Editor GUI
        /// 参数:
        ///   customEditorGUI - 自定义编辑器 GUI 类名
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_custom_editor_gui", "设置 Custom Editor GUI")]
        public static object SetCustomEditorGUI(JObject parameters)
        {
            string customEditorGUI = parameters["customEditorGUI"]?.ToString();

            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP target active" };

            RegisterUndo(graph, "Change Custom Editor GUI");
            SGReflection.SetProp(hdTarget, "m_CustomEditorGUI", customEditorGUI ?? "");
            RefreshAfterChange(graph);

            return new { success = true, message = $"Custom Editor GUI set to: {customEditorGUI}" };
        }

        /// <summary>
        /// 设置 HDRP Target 的 Support VFX Graph
        /// 参数:
        ///   enabled - true/false
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_support_vfx", "设置 Support VFX Graph")]
        public static object SetSupportVFX(JObject parameters)
        {
            bool enabled = parameters["enabled"]?.Value<bool>() ?? false;

            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP target active" };

            RegisterUndo(graph, "Change VFX Support");
            SGReflection.SetField(hdTarget, "m_SupportVFX", enabled);
            RefreshAfterChange(graph);

            return new { success = true, message = $"VFX Graph support: {enabled}" };
        }

        /// <summary>
        /// 设置 HDRP Target 的 Support Compute for Vertex Setup
        /// 参数:
        ///   enabled - true/false
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_support_compute_vertex", "设置 Support Compute for Vertex Setup")]
        public static object SetSupportComputeVertex(JObject parameters)
        {
            bool enabled = parameters["enabled"]?.Value<bool>() ?? false;

            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP target active" };

            RegisterUndo(graph, "Change Compute Vertex Support");
            SGReflection.SetField(hdTarget, "m_SupportComputeForVertexSetup", enabled);
            RefreshAfterChange(graph);

            return new { success = true, message = $"Compute for Vertex Setup: {enabled}" };
        }

        #endregion
#endif // HDRP_INSTALLED

        // ================================================================
        //  Region 3: Surface Options (SystemData + BuiltinData + LightingData)
        // ================================================================

#if URP_INSTALLED || HDRP_INSTALLED
        #region Surface Options

        /// <summary>
        /// 获取 Surface Options 的所有设置
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_get_surface_options", "获取 Surface Options 的所有设置")]
        public static object GetSurfaceOptions(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            // Try URP first (more common), then HDRP
#if URP_INSTALLED
            var universalTarget = SGReflection.GetUniversalTarget(graph);
            if (universalTarget != null)
                return GetURPSurfaceOptions(universalTarget);
#endif

#if HDRP_INSTALLED
            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP or URP target active" };

            var systemData = SGReflection.GetSystemData(hdTarget);
            var builtinData = SGReflection.GetBuiltinData(hdTarget);
            var lightingData = SGReflection.GetLightingData(hdTarget);

            if (systemData == null) return new { success = false, message = "SystemData not available" };

            var result = new Dictionary<string, object>
            {
                { "success", true },

                // SystemData
                { "surfaceType", SafeGetEnumValue(SGReflection.T_SurfaceType, systemData, "surfaceType") },
                { "renderQueueType", SafeGetEnumValue(SGReflection.T_RenderQueueType, systemData, "renderQueueType") },
                { "blendMode", SafeGetEnumValue(SGReflection.T_BlendMode, systemData, "blendMode") },
                { "sortPriority", SGReflection.Prop(systemData, "sortPriority") },
                { "alphaTest", SGReflection.Prop(systemData, "alphaTest") },
                { "doubleSidedMode", SafeGetEnumValue(SGReflection.T_DoubleSidedMode, systemData, "doubleSidedMode") },
                { "transparentZWrite", SGReflection.Prop(systemData, "transparentZWrite") },
                { "zTest", SafeGetEnumValue(SGReflection.T_CompareFunction, systemData, "zTest") },
                { "customVelocity", SGReflection.Prop(systemData, "customVelocity") },
                { "excludeFromTUAndAA", SGReflection.Prop(systemData, "excludeFromTUAndAA") },
                { "tessellation", SGReflection.Prop(systemData, "tessellation") },
            };

            // Tessellation sub-properties
            if ((bool?)SGReflection.Prop(systemData, "tessellation") == true)
            {
                result["tessellationMaxDisplacement"] = SGReflection.Prop(systemData, "tessellationMaxDisplacement");
                result["tessellationBackFaceCullEpsilon"] = SGReflection.Prop(systemData, "tessellationBackFaceCullEpsilon");
                result["tessellationFactorMinDistance"] = SGReflection.Prop(systemData, "tessellationFactorMinDistance");
                result["tessellationFactorMaxDistance"] = SGReflection.Prop(systemData, "tessellationFactorMaxDistance");
                result["tessellationFactorTriangleSize"] = SGReflection.Prop(systemData, "tessellationFactorTriangleSize");
                result["tessellationMode"] = SafeGetEnumValue(SGReflection.T_TessellationMode, systemData, "tessellationMode");
                result["tessellationShapeFactor"] = SGReflection.Prop(systemData, "tessellationShapeFactor");
            }

            // Cull modes
            var transparentCullMode = SGReflection.Prop(systemData, "transparentCullMode");
            var opaqueCullMode = SGReflection.Prop(systemData, "opaqueCullMode");
            result["transparentCullMode"] = transparentCullMode?.ToString();
            result["opaqueCullMode"] = opaqueCullMode?.ToString();

            // BuiltinData
            if (builtinData != null)
            {
                result["transparencyFog"] = SGReflection.Prop(builtinData, "transparencyFog");
                result["backThenFrontRendering"] = SGReflection.Prop(builtinData, "backThenFrontRendering");
                result["transparentDepthPrepass"] = SGReflection.Prop(builtinData, "transparentDepthPrepass");
                result["transparentDepthPostpass"] = SGReflection.Prop(builtinData, "transparentDepthPostpass");
                result["transparentWritesMotionVec"] = SGReflection.Prop(builtinData, "transparentWritesMotionVec");
                result["alphaTestShadow"] = SGReflection.Prop(builtinData, "alphaTestShadow");
                result["depthOffset"] = SGReflection.Prop(builtinData, "depthOffset");
                result["conservativeDepthOffset"] = SGReflection.Prop(builtinData, "conservativeDepthOffset");
                result["supportLodCrossFade"] = SGReflection.Prop(builtinData, "supportLodCrossFade");
                result["addPrecomputedVelocity"] = SGReflection.Prop(builtinData, "addPrecomputedVelocity");
                result["distortion"] = SGReflection.Prop(builtinData, "distortion");
                result["distortionMode"] = SafeGetEnumValue(SGReflection.HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.DistortionMode"), builtinData, "distortionMode");
                result["distortionDepthTest"] = SGReflection.Prop(builtinData, "distortionDepthTest");
            }

            // LightingData (null for Unlit)
            if (lightingData != null)
            {
                result["normalDropOffSpace"] = SafeGetEnumValue(SGReflection.T_NormalDropOffSpace, lightingData, "normalDropOffSpace");
                result["receiveDecals"] = SGReflection.Prop(lightingData, "receiveDecals");
                result["receiveSSR"] = SGReflection.Prop(lightingData, "receiveSSR");
                result["receiveSSRTransparent"] = SGReflection.Prop(lightingData, "receiveSSRTransparent");
                result["specularAA"] = SGReflection.Prop(lightingData, "specularAA");
                result["blendPreserveSpecular"] = SGReflection.Prop(lightingData, "blendPreserveSpecular");
            }

            return result;
#endif // HDRP_INSTALLED
        }

        /// <summary>
        /// 设置 Surface Options 的属性（批量设置）
        /// 支持的参数与 Surface Options 面板一一对应
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_surface_options", "设置 Surface Options 的属性")]
        public static object SetSurfaceOptions(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            // Try URP first, then HDRP
#if URP_INSTALLED
            var universalTarget = SGReflection.GetUniversalTarget(graph);
            if (universalTarget != null)
                return SetURPSurfaceOptions(graph, universalTarget, parameters);
#endif

#if HDRP_INSTALLED
            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP or URP target active" };

            var systemData = SGReflection.GetSystemData(hdTarget);
            var builtinData = SGReflection.GetBuiltinData(hdTarget);
            var lightingData = SGReflection.GetLightingData(hdTarget);

            if (systemData == null) return new { success = false, message = "SystemData not available" };

            RegisterUndo(graph, "Change Surface Options");
            var changes = new List<string>();

            // --- SystemData ---
            changes.AddRange(SetSystemDataProperties(systemData, hdTarget, parameters));

            // --- BuiltinData ---
            if (builtinData != null)
                changes.AddRange(SetBuiltinDataProperties(builtinData, parameters));

            // --- LightingData ---
            if (lightingData != null)
                changes.AddRange(SetLightingDataProperties(lightingData, parameters));

            // subTargetTypeName — switch HDRP SubTarget (e.g. "HDUnlitSubTarget", "HDLitSubTarget")
            string hdSubTargetTypeName = parameters["subTargetTypeName"]?.ToString();
            if (!string.IsNullOrEmpty(hdSubTargetTypeName) && hdTarget != null)
            {
                bool switched = SGReflection.TrySetActiveSubTarget(hdTarget, hdSubTargetTypeName);
                if (switched)
                    changes.Add($"subTargetTypeName: {hdSubTargetTypeName}");
                else
                    changes.Add($"subTargetTypeName: FAILED to switch to {hdSubTargetTypeName}");
            }

            RefreshAfterChange(graph);

            return new { success = true, pipeline = "HDRP", message = $"Updated {changes.Count} properties", changes };
#endif // HDRP_INSTALLED
        }

        #endregion
#endif // URP_INSTALLED || HDRP_INSTALLED

        // ================================================================
        //  Region 4: Advanced Options
        // ================================================================

#if HDRP_INSTALLED
        #region Advanced Options

        /// <summary>
        /// 获取 Advanced Options 的所有设置
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_get_advanced_options", "获取 Advanced Options 的所有设置")]
        public static object GetAdvancedOptions(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP target active" };

            var builtinData = SGReflection.GetBuiltinData(hdTarget);
            var lightingData = SGReflection.GetLightingData(hdTarget);

            var result = new Dictionary<string, object> { { "success", true } };

            if (lightingData != null)
            {
                result["specularOcclusionMode"] = SafeGetEnumValue(SGReflection.T_SpecularOcclusionMode, lightingData, "specularOcclusionMode");
                result["overrideBakedGI"] = SGReflection.Prop(lightingData, "overrideBakedGI");
            }

            if (builtinData != null)
            {
                result["supportLodCrossFade"] = SGReflection.Prop(builtinData, "supportLodCrossFade");
                result["addPrecomputedVelocity"] = SGReflection.Prop(builtinData, "addPrecomputedVelocity");
            }

            return result;
        }

        /// <summary>
        /// 设置 Advanced Options 的属性
        /// 参数:
        ///   specularOcclusionMode - "Off" / "FromAO" / "FromAOAndBentNormal" / "Custom"
        ///   overrideBakedGI - bool
        ///   supportLodCrossFade - bool
        ///   addPrecomputedVelocity - bool
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_advanced_options", "设置 Advanced Options 的属性")]
        public static object SetAdvancedOptions(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP target active" };

            var builtinData = SGReflection.GetBuiltinData(hdTarget);
            var lightingData = SGReflection.GetLightingData(hdTarget);

            RegisterUndo(graph, "Change Advanced Options");
            var changes = new List<string>();

            // LightingData
            if (lightingData != null)
            {
                string specOccStr = parameters["specularOcclusionMode"]?.ToString();
                if (!string.IsNullOrEmpty(specOccStr))
                {
                    var parsed = ParseHDRPEnum(SGReflection.T_SpecularOcclusionMode, specOccStr);
                    if (parsed != null)
                    {
                        SGReflection.SetProp(lightingData, "specularOcclusionMode", parsed);
                        changes.Add($"specularOcclusionMode: {specOccStr}");
                    }
                }

                bool? overrideGI = parameters["overrideBakedGI"]?.Value<bool>();
                if (overrideGI.HasValue)
                {
                    SGReflection.SetProp(lightingData, "overrideBakedGI", overrideGI.Value);
                    changes.Add($"overrideBakedGI: {overrideGI.Value}");
                }
            }

            // BuiltinData
            if (builtinData != null)
            {
                bool? lodCrossFade = parameters["supportLodCrossFade"]?.Value<bool>();
                if (lodCrossFade.HasValue)
                {
                    SGReflection.SetProp(builtinData, "supportLodCrossFade", lodCrossFade.Value);
                    changes.Add($"supportLodCrossFade: {lodCrossFade.Value}");
                }

                bool? addPrecompVel = parameters["addPrecomputedVelocity"]?.Value<bool>();
                if (addPrecompVel.HasValue)
                {
                    SGReflection.SetProp(builtinData, "addPrecomputedVelocity", addPrecompVel.Value);
                    changes.Add($"addPrecomputedVelocity: {addPrecompVel.Value}");
                }
            }

            RefreshAfterChange(graph);

            return new { success = true, message = $"Updated {changes.Count} properties", changes };
        }

        #endregion
#endif // HDRP_INSTALLED

        // ================================================================
        //  Region 5: Lit-Specific Options (HDLitData)
        // ================================================================

#if HDRP_INSTALLED
        #region Lit-Specific Options

        /// <summary>
        /// 获取 Lit 特有的选项（Material Type、Clear Coat、SSS、Refraction、Energy Conserving Specular）
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_get_lit_options", "获取 Lit 特有的选项（Material Type、Clear Coat 等）")]
        public static object GetLitOptions(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP target active" };

            var litData = SGReflection.GetHDLitData(hdTarget);
            if (litData == null) return new { success = false, message = "Active target is not Lit. Use shader_graph_get_surface_options for other target types." };

            var materialTypeFlag = SGReflection.Prop(litData, "materialTypeFlag")?.ToString();
            var clearCoat = SGReflection.Prop(litData, "clearCoat");
            var sssTransmission = SGReflection.Prop(litData, "sssTransmission");
            var refractionModel = SGReflection.Prop(litData, "refractionModel")?.ToString();
            var energyConservingSpecular = SGReflection.Prop(litData, "energyConservingSpecular");
            var rayTracing = SGReflection.Prop(litData, "rayTracing");

            return new
            {
                success = true,
                materialTypeFlag,
                clearCoat,
                sssTransmission,
                refractionModel,
                energyConservingSpecular,
                rayTracing
            };
        }

        /// <summary>
        /// 设置 Lit 特有的选项
        /// 参数:
        ///   materialTypeFlag - Material Type (e.g. "Standard", "SubsurfaceScattering", "SpecularColor")
        ///   clearCoat - bool
        ///   sssTransmission - bool
        ///   refractionModel - "None" / "Plane" / "Sphere" / "Thin"
        ///   energyConservingSpecular - bool
        ///   rayTracing - bool
        /// </summary>
        [ExecuteCustomTool.CustomTool("shader_graph_set_lit_options", "设置 Lit 特有的选项")]
        public static object SetLitOptions(JObject parameters)
        {
            var graph = RequireGraph();
            if (graph == null) return new { success = false, message = "No Shader Graph window open" };

            var hdTarget = SGReflection.GetHDTarget(graph);
            if (hdTarget == null) return new { success = false, message = "No HDRP target active" };

            var litData = SGReflection.GetHDLitData(hdTarget);
            if (litData == null) return new { success = false, message = "Active target is not Lit" };

            RegisterUndo(graph, "Change Lit Options");
            var changes = new List<string>();

            // materialTypeFlag (enum: HDLitData.MaterialTypeFlag — flags enum)
            string materialTypeFlagStr = parameters["materialTypeFlag"]?.ToString();
            if (!string.IsNullOrEmpty(materialTypeFlagStr) && SGReflection.T_HDLitMaterialTypeFlag != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_HDLitMaterialTypeFlag, materialTypeFlagStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(litData, "materialTypeFlag", parsed);
                    changes.Add($"materialTypeFlag: {materialTypeFlagStr}");
                }
            }

            // clearCoat
            bool? clearCoat = parameters["clearCoat"]?.Value<bool>();
            if (clearCoat.HasValue)
            {
                SGReflection.SetProp(litData, "clearCoat", clearCoat.Value);
                changes.Add($"clearCoat: {clearCoat.Value}");
            }

            // sssTransmission
            bool? sssTransmission = parameters["sssTransmission"]?.Value<bool>();
            if (sssTransmission.HasValue)
            {
                SGReflection.SetProp(litData, "sssTransmission", sssTransmission.Value);
                changes.Add($"sssTransmission: {sssTransmission.Value}");
            }

            // refractionModel
            string refractionModelStr = parameters["refractionModel"]?.ToString();
            if (!string.IsNullOrEmpty(refractionModelStr) && SGReflection.T_RefractionModel != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_RefractionModel, refractionModelStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(litData, "refractionModel", parsed);
                    changes.Add($"refractionModel: {refractionModelStr}");
                }
            }

            // energyConservingSpecular
            bool? energySpec = parameters["energyConservingSpecular"]?.Value<bool>();
            if (energySpec.HasValue)
            {
                SGReflection.SetProp(litData, "energyConservingSpecular", energySpec.Value);
                changes.Add($"energyConservingSpecular: {energySpec.Value}");
            }

            // rayTracing
            bool? rayTracing = parameters["rayTracing"]?.Value<bool>();
            if (rayTracing.HasValue)
            {
                SGReflection.SetProp(litData, "rayTracing", rayTracing.Value);
                changes.Add($"rayTracing: {rayTracing.Value}");
            }

            RefreshAfterChange(graph);

            return new { success = true, message = $"Updated {changes.Count} properties", changes };
        }

        #endregion
#endif // HDRP_INSTALLED

        // ================================================================
        //  Internal: Property Setter Helpers
        // ================================================================

#if HDRP_INSTALLED
        #region Property Setter Helpers

        private static List<string> SetSystemDataProperties(object systemData, object hdTarget, JObject parameters)
        {
            var changes = new List<string>();

            // Surface Type
            string surfaceTypeStr = parameters["surfaceType"]?.ToString();
            if (!string.IsNullOrEmpty(surfaceTypeStr) && SGReflection.T_SurfaceType != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_SurfaceType, surfaceTypeStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(systemData, "surfaceType", parsed);
                    // Mirror UI behavior: TryChangeRenderingPass after surface type change
                    var tryChangeMethod = SGReflection.CacheMethod(SGReflection.T_SystemData, "TryChangeRenderingPass");
                    if (tryChangeMethod != null)
                    {
                        var currentRQ = SGReflection.Prop(systemData, "renderQueueType");
                        tryChangeMethod.Invoke(systemData, new object[] { currentRQ });
                    }
                    changes.Add($"surfaceType: {surfaceTypeStr}");
                }
            }

            // Render Queue Type
            string renderQueueStr = parameters["renderQueueType"]?.ToString();
            if (!string.IsNullOrEmpty(renderQueueStr) && SGReflection.T_RenderQueueType != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_RenderQueueType, renderQueueStr);
                if (parsed != null)
                {
                    var tryChangeMethod = SGReflection.CacheMethod(SGReflection.T_SystemData, "TryChangeRenderingPass");
                    if (tryChangeMethod != null)
                    {
                        bool changed = (bool)tryChangeMethod.Invoke(systemData, new object[] { parsed });
                        if (changed) changes.Add($"renderQueueType: {renderQueueStr}");
                    }
                    else
                    {
                        SGReflection.SetProp(systemData, "renderQueueType", parsed);
                        changes.Add($"renderQueueType: {renderQueueStr}");
                    }
                }
            }

            // Blend Mode
            string blendModeStr = parameters["blendMode"]?.ToString();
            if (!string.IsNullOrEmpty(blendModeStr) && SGReflection.T_BlendMode != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_BlendMode, blendModeStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(systemData, "blendMode", parsed);
                    changes.Add($"blendMode: {blendModeStr}");
                }
            }

            // Sort Priority
            int? sortPriority = parameters["sortPriority"]?.Value<int>();
            if (sortPriority.HasValue)
            {
                // Mirror UI: HDRenderQueue.ClampsTransparentRangePriority — use reflection for HDRP type
                var hdRenderQueueType = SGReflection.HDAssembly?.GetType("UnityEngine.Rendering.HighDefinition.HDRenderQueue");
                int minSort = -100, maxSort = 100; // default fallback
                if (hdRenderQueueType != null)
                {
                    var minField = hdRenderQueueType.GetField("k_MinimumTransparentSortPriority", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var maxField = hdRenderQueueType.GetField("k_MaximumTransparentSortPriority", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (minField != null) minSort = (int)minField.GetValue(null);
                    if (maxField != null) maxSort = (int)maxField.GetValue(null);
                }
                var clamped = Mathf.Clamp(sortPriority.Value, minSort, maxSort);
                SGReflection.SetProp(systemData, "sortPriority", clamped);
                changes.Add($"sortPriority: {clamped}");
            }

            // Alpha Test
            bool? alphaTest = parameters["alphaTest"]?.Value<bool>();
            if (alphaTest.HasValue)
            {
                SGReflection.SetProp(systemData, "alphaTest", alphaTest.Value);
                changes.Add($"alphaTest: {alphaTest.Value}");
            }

            // Double Sided Mode
            string doubleSidedModeStr = parameters["doubleSidedMode"]?.ToString();
            if (!string.IsNullOrEmpty(doubleSidedModeStr) && SGReflection.T_DoubleSidedMode != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_DoubleSidedMode, doubleSidedModeStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(systemData, "doubleSidedMode", parsed);
                    changes.Add($"doubleSidedMode: {doubleSidedModeStr}");
                }
            }

            // Transparent Z Write
            bool? transparentZWrite = parameters["transparentZWrite"]?.Value<bool>();
            if (transparentZWrite.HasValue)
            {
                SGReflection.SetProp(systemData, "transparentZWrite", transparentZWrite.Value);
                changes.Add($"transparentZWrite: {transparentZWrite.Value}");
            }

            // Z Test
            string zTestStr = parameters["zTest"]?.ToString();
            if (!string.IsNullOrEmpty(zTestStr))
            {
                var parsed = ParseHDRPEnum(SGReflection.T_CompareFunction, zTestStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(systemData, "zTest", parsed);
                    changes.Add($"zTest: {zTestStr}");
                }
            }

            // Transparent Cull Mode
            string transparentCullModeStr = parameters["transparentCullMode"]?.ToString();
            if (!string.IsNullOrEmpty(transparentCullModeStr))
            {
                var parsed = ParseHDRPEnum(SGReflection.T_TransparentCullMode, transparentCullModeStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(systemData, "transparentCullMode", parsed);
                    changes.Add($"transparentCullMode: {transparentCullModeStr}");
                }
            }

            // Opaque Cull Mode
            string opaqueCullModeStr = parameters["opaqueCullMode"]?.ToString();
            if (!string.IsNullOrEmpty(opaqueCullModeStr))
            {
                var parsed = ParseHDRPEnum(SGReflection.T_OpaqueCullMode, opaqueCullModeStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(systemData, "opaqueCullMode", parsed);
                    changes.Add($"opaqueCullMode: {opaqueCullModeStr}");
                }
            }

            // Custom Velocity
            bool? customVelocity = parameters["customVelocity"]?.Value<bool>();
            if (customVelocity.HasValue)
            {
                SGReflection.SetProp(systemData, "customVelocity", customVelocity.Value);
                changes.Add($"customVelocity: {customVelocity.Value}");
            }

            // Exclude From TU And AA
            bool? excludeFromTUAndAA = parameters["excludeFromTUAndAA"]?.Value<bool>();
            if (excludeFromTUAndAA.HasValue)
            {
                SGReflection.SetProp(systemData, "excludeFromTUAndAA", excludeFromTUAndAA.Value);
                changes.Add($"excludeFromTUAndAA: {excludeFromTUAndAA.Value}");
            }

            // Tessellation
            bool? tessellation = parameters["tessellation"]?.Value<bool>();
            if (tessellation.HasValue)
            {
                SGReflection.SetProp(systemData, "tessellation", tessellation.Value);
                changes.Add($"tessellation: {tessellation.Value}");
            }

            // Tessellation sub-properties
            if ((bool?)SGReflection.Prop(systemData, "tessellation") == true || tessellation == true)
            {
                float? tessMaxDisp = parameters["tessellationMaxDisplacement"]?.Value<float>();
                if (tessMaxDisp.HasValue) { SGReflection.SetProp(systemData, "tessellationMaxDisplacement", Mathf.Abs(tessMaxDisp.Value)); changes.Add($"tessellationMaxDisplacement: {Mathf.Abs(tessMaxDisp.Value)}"); }

                float? tessBackFaceCull = parameters["tessellationBackFaceCullEpsilon"]?.Value<float>();
                if (tessBackFaceCull.HasValue) { SGReflection.SetProp(systemData, "tessellationBackFaceCullEpsilon", Mathf.Clamp(tessBackFaceCull.Value, -1.0f, 0.0f)); changes.Add($"tessellationBackFaceCullEpsilon: {Mathf.Clamp(tessBackFaceCull.Value, -1.0f, 0.0f)}"); }

                float? tessMinDist = parameters["tessellationFactorMinDistance"]?.Value<float>();
                if (tessMinDist.HasValue) { SGReflection.SetProp(systemData, "tessellationFactorMinDistance", tessMinDist.Value); changes.Add($"tessellationFactorMinDistance: {tessMinDist.Value}"); }

                float? tessMaxDist = parameters["tessellationFactorMaxDistance"]?.Value<float>();
                if (tessMaxDist.HasValue) { SGReflection.SetProp(systemData, "tessellationFactorMaxDistance", tessMaxDist.Value); changes.Add($"tessellationFactorMaxDistance: {tessMaxDist.Value}"); }

                float? tessTriSize = parameters["tessellationFactorTriangleSize"]?.Value<float>();
                if (tessTriSize.HasValue) { SGReflection.SetProp(systemData, "tessellationFactorTriangleSize", tessTriSize.Value); changes.Add($"tessellationFactorTriangleSize: {tessTriSize.Value}"); }

                string tessModeStr = parameters["tessellationMode"]?.ToString();
                if (!string.IsNullOrEmpty(tessModeStr) && SGReflection.T_TessellationMode != null)
                {
                    var parsed = ParseHDRPEnum(SGReflection.T_TessellationMode, tessModeStr);
                    if (parsed != null) { SGReflection.SetProp(systemData, "tessellationMode", parsed); changes.Add($"tessellationMode: {tessModeStr}"); }
                }

                float? tessShapeFactor = parameters["tessellationShapeFactor"]?.Value<float>();
                if (tessShapeFactor.HasValue) { SGReflection.SetProp(systemData, "tessellationShapeFactor", Mathf.Clamp01(tessShapeFactor.Value)); changes.Add($"tessellationShapeFactor: {Mathf.Clamp01(tessShapeFactor.Value)}"); }
            }

            return changes;
        }

        private static List<string> SetBuiltinDataProperties(object builtinData, JObject parameters)
        {
            var changes = new List<string>();

            SetBoolProp(builtinData, "transparencyFog", parameters, changes);
            SetBoolProp(builtinData, "backThenFrontRendering", parameters, changes);
            SetBoolProp(builtinData, "transparentDepthPrepass", parameters, changes);
            SetBoolProp(builtinData, "transparentDepthPostpass", parameters, changes);
            SetBoolProp(builtinData, "transparentWritesMotionVec", parameters, changes);
            SetBoolProp(builtinData, "alphaTestShadow", parameters, changes);
            SetBoolProp(builtinData, "depthOffset", parameters, changes);
            SetBoolProp(builtinData, "conservativeDepthOffset", parameters, changes);

            // Distortion
            bool? distortion = parameters["distortion"]?.Value<bool>();
            if (distortion.HasValue)
            {
                SGReflection.SetProp(builtinData, "distortion", distortion.Value);
                changes.Add($"distortion: {distortion.Value}");
            }

            string distortionModeStr = parameters["distortionMode"]?.ToString();
            if (!string.IsNullOrEmpty(distortionModeStr))
            {
                var distModeType = SGReflection.HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.DistortionMode");
                if (distModeType != null)
                {
                    var parsed = ParseHDRPEnum(distModeType, distortionModeStr);
                    if (parsed != null)
                    {
                        SGReflection.SetProp(builtinData, "distortionMode", parsed);
                        changes.Add($"distortionMode: {distortionModeStr}");
                    }
                }
            }

            SetBoolProp(builtinData, "distortionDepthTest", parameters, changes);

            return changes;
        }

        private static List<string> SetLightingDataProperties(object lightingData, JObject parameters)
        {
            var changes = new List<string>();

            // Normal Drop Off Space
            string normalDropOffStr = parameters["normalDropOffSpace"]?.ToString();
            if (!string.IsNullOrEmpty(normalDropOffStr) && SGReflection.T_NormalDropOffSpace != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_NormalDropOffSpace, normalDropOffStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(lightingData, "normalDropOffSpace", parsed);
                    changes.Add($"normalDropOffSpace: {normalDropOffStr}");
                }
            }

            SetBoolProp(lightingData, "receiveDecals", parameters, changes);
            SetBoolProp(lightingData, "receiveSSR", parameters, changes);
            SetBoolProp(lightingData, "receiveSSRTransparent", parameters, changes);
            SetBoolProp(lightingData, "specularAA", parameters, changes);
            SetBoolProp(lightingData, "blendPreserveSpecular", parameters, changes);

            return changes;
        }

        private static void SetBoolProp(object target, string propName, JObject parameters, List<string> changes)
        {
            bool? value = parameters[propName]?.Value<bool>();
            if (value.HasValue)
            {
                SGReflection.SetProp(target, propName, value.Value);
                changes.Add($"{propName}: {value.Value}");
            }
        }

        #endregion
#endif // HDRP_INSTALLED

        // ================================================================
        //  URP Surface Options
        // ================================================================

#if URP_INSTALLED
        #region URP Surface Options

        private static object GetURPSurfaceOptions(object universalTarget)
        {
            var result = new Dictionary<string, object> { { "success", true }, { "pipeline", "URP" } };

            result["surfaceType"] = SGReflection.Prop(universalTarget, "surfaceType")?.ToString();
            result["alphaMode"] = SGReflection.Prop(universalTarget, "alphaMode")?.ToString();
            result["renderFace"] = SGReflection.Prop(universalTarget, "renderFace")?.ToString();
            result["alphaClip"] = SGReflection.Prop(universalTarget, "alphaClip");
            result["castShadows"] = SGReflection.Prop(universalTarget, "castShadows");
            result["receiveShadows"] = SGReflection.Prop(universalTarget, "receiveShadows");
            result["zWriteControl"] = SGReflection.Prop(universalTarget, "zWriteControl")?.ToString();
            result["zTestMode"] = SGReflection.Prop(universalTarget, "zTestMode")?.ToString();
            result["allowMaterialOverride"] = SGReflection.Prop(universalTarget, "allowMaterialOverride");
            result["customEditorGUI"] = SGReflection.Prop(universalTarget, "customEditorGUI") ?? "";
            result["supportVFX"] = SGReflection.Prop(universalTarget, "supportVFX");
            result["supportsLodCrossFade"] = SGReflection.Prop(universalTarget, "supportsLodCrossFade");

            // Stencil
            result["overrideStencilState"] = SGReflection.Prop(universalTarget, "overrideStencilState");
            result["stencilReference"] = SGReflection.Prop(universalTarget, "stencilReference");
            result["stencilReadMask"] = SGReflection.Prop(universalTarget, "stencilReadMask");
            result["stencilWriteMask"] = SGReflection.Prop(universalTarget, "stencilWriteMask");
            result["stencilCompareFunction"] = SGReflection.Prop(universalTarget, "stencilCompareFunction")?.ToString();

            // Active SubTarget info
            var subTarget = SGReflection.GetUniversalActiveSubTarget(universalTarget);
            if (subTarget != null)
            {
                result["activeSubTarget"] = new
                {
                    typeName = subTarget.GetType().Name,
                    displayName = (string)SGReflection.Prop(subTarget, "displayName")
                };
            }

            return result;
        }

        private static object SetURPSurfaceOptions(object graph, object universalTarget, JObject parameters)
        {
            RegisterUndo(graph, "Change Surface Options (URP)");
            var changes = new List<string>();

            // surfaceType
            string surfaceTypeStr = parameters["surfaceType"]?.ToString();
            if (!string.IsNullOrEmpty(surfaceTypeStr) && SGReflection.T_URPSurfaceType != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_URPSurfaceType, surfaceTypeStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(universalTarget, "surfaceType", parsed);
                    changes.Add($"surfaceType: {surfaceTypeStr}");
                }
            }

            // alphaMode
            string alphaModeStr = parameters["alphaMode"]?.ToString();
            if (!string.IsNullOrEmpty(alphaModeStr) && SGReflection.T_URPAlphaMode != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_URPAlphaMode, alphaModeStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(universalTarget, "alphaMode", parsed);
                    changes.Add($"alphaMode: {alphaModeStr}");
                }
            }

            // renderFace
            string renderFaceStr = parameters["renderFace"]?.ToString();
            if (!string.IsNullOrEmpty(renderFaceStr) && SGReflection.T_URPRenderFace != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_URPRenderFace, renderFaceStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(universalTarget, "renderFace", parsed);
                    changes.Add($"renderFace: {renderFaceStr}");
                }
            }

            // zWriteControl
            string zWriteStr = parameters["zWriteControl"]?.ToString();
            if (!string.IsNullOrEmpty(zWriteStr) && SGReflection.T_URPZWriteControl != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_URPZWriteControl, zWriteStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(universalTarget, "zWriteControl", parsed);
                    changes.Add($"zWriteControl: {zWriteStr}");
                }
            }

            // zTestMode
            string zTestStr = parameters["zTestMode"]?.ToString();
            if (!string.IsNullOrEmpty(zTestStr) && SGReflection.T_URPZTestMode != null)
            {
                var parsed = ParseHDRPEnum(SGReflection.T_URPZTestMode, zTestStr);
                if (parsed != null)
                {
                    SGReflection.SetProp(universalTarget, "zTestMode", parsed);
                    changes.Add($"zTestMode: {zTestStr}");
                }
            }

            // Boolean fields
            SetURPBoolProp(universalTarget, "alphaClip", parameters, changes);
            SetURPBoolProp(universalTarget, "castShadows", parameters, changes);
            SetURPBoolProp(universalTarget, "receiveShadows", parameters, changes);
            SetURPBoolProp(universalTarget, "allowMaterialOverride", parameters, changes);
            SetURPBoolProp(universalTarget, "supportVFX", parameters, changes);
            SetURPBoolProp(universalTarget, "supportsLodCrossFade", parameters, changes);

            // customEditorGUI
            string customGUI = parameters["customEditorGUI"]?.ToString();
            if (customGUI != null)
            {
                SGReflection.SetProp(universalTarget, "customEditorGUI", customGUI);
                changes.Add($"customEditorGUI: {customGUI}");
            }

            // subTargetTypeName — switch URP SubTarget (e.g. "UniversalUnlitSubTarget", "UniversalLitSubTarget", "UniversalDecalSubTarget")
            string subTargetTypeName = parameters["subTargetTypeName"]?.ToString();
            if (!string.IsNullOrEmpty(subTargetTypeName))
            {
                bool switched = SGReflection.TrySetUniversalActiveSubTarget(universalTarget, subTargetTypeName);
                if (switched)
                {
                    changes.Add($"subTargetTypeName: {subTargetTypeName}");
                    // Re-initialize output Block nodes after SubTarget switch
                    bool initOk = SGReflection.InitializeOutputBlocks(graph, universalTarget, subTargetTypeName);
                    if (initOk)
                        changes.Add($"InitializeOutputBlocks: {subTargetTypeName}");
                    else
                        changes.Add($"InitializeOutputBlocks: FAILED for {subTargetTypeName}");
                }
                else
                    changes.Add($"subTargetTypeName: FAILED to switch to {subTargetTypeName}");
            }

            RefreshAfterChange(graph);

            return new { success = true, pipeline = "URP", message = $"Updated {changes.Count} properties", changes };
        }

        private static void SetURPBoolProp(object target, string propName, JObject parameters, List<string> changes)
        {
            bool? value = parameters[propName]?.Value<bool>();
            if (value.HasValue)
            {
                SGReflection.SetProp(target, propName, value.Value);
                changes.Add($"{propName}: {value.Value}");
            }
        }

        #endregion
#endif // URP_INSTALLED
    }
}
