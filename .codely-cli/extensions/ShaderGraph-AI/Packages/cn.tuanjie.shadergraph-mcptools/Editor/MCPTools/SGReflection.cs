using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityTcp.ShaderTools
{
    /// <summary>
    /// 反射辅助类，用于从 Assembly-CSharp-Editor 访问 Unity.ShaderGraph.Editor 的 internal 类型。
    /// 所有 Type/PropertyInfo/MethodInfo/FieldInfo 均懒加载并缓存。
    /// </summary>
    public static class SGReflection
    {
        // ================================================================
        //  Assembly & Type Cache
        // ================================================================
        private static Assembly _sgAsm;

        public static Assembly SGAssembly
        {
            get
            {
                if (_sgAsm == null)
                {
                    try { _sgAsm = Assembly.Load("Unity.ShaderGraph.Editor"); }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SGReflection] Failed to load Unity.ShaderGraph.Editor: {e.Message}");
                    }
                }
                return _sgAsm;
            }
        }

        public static bool IsAvailable => SGAssembly != null;

        private static readonly Dictionary<string, Type> _typeCache = new();

        public static Type GetSGType(string fullName)
        {
            if (_typeCache.TryGetValue(fullName, out var t)) return t;
            t = SGAssembly?.GetType(fullName);
            _typeCache[fullName] = t;
            return t;
        }

        // ================================================================
        //  Member Cache
        // ================================================================
        private static readonly Dictionary<(Type, string), PropertyInfo> _propCache = new();
        private static readonly Dictionary<(Type, string), FieldInfo> _fieldCache = new();
        private static readonly Dictionary<(Type, string), MethodInfo> _methodCache = new();

        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags All = AllInstance | AllStatic;

        public static PropertyInfo CacheProp(Type type, string name)
        {
            if (type == null) return null;
            var key = (type, name);
            if (!_propCache.TryGetValue(key, out var pi))
            {
                pi = type.GetProperty(name, All);
                _propCache[key] = pi;
            }
            return pi;
        }

        public static FieldInfo CacheField(Type type, string name)
        {
            if (type == null) return null;
            var key = (type, name);
            if (!_fieldCache.TryGetValue(key, out var fi))
            {
                fi = type.GetField(name, All);
                _fieldCache[key] = fi;
            }
            return fi;
        }

        public static MethodInfo CacheMethod(Type type, string name)
        {
            if (type == null) return null;
            var key = (type, name);
            if (!_methodCache.TryGetValue(key, out var mi))
            {
                mi = type.GetMethod(name, All);
                _methodCache[key] = mi;
            }
            return mi;
        }

        // ================================================================
        //  Quick Access Helpers
        // ================================================================
        public static object Prop(object target, string name)
            => CacheProp(target.GetType(), name)?.GetValue(target);

        public static void SetProp(object target, string name, object value)
            => CacheProp(target.GetType(), name)?.SetValue(target, value);

        public static object Field(object target, string name)
            => CacheField(target.GetType(), name)?.GetValue(target);

        public static void SetField(object target, string name, object value)
            => CacheField(target.GetType(), name)?.SetValue(target, value);

        public static object Call(object target, string name, params object[] args)
            => CacheMethod(target.GetType(), name)?.Invoke(target, args);

        public static object CallStatic(Type type, string name, params object[] args)
            => CacheMethod(type, name)?.Invoke(null, args);

        // ================================================================
        //  Key ShaderGraph Types (lazy)
        // ================================================================
        // Window / Drawing
        public static Type T_MaterialGraphEditWindow => GetSGType("UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow");

        // Graph
        public static Type T_GraphData => GetSGType("UnityEditor.ShaderGraph.GraphData");

        // Nodes
        public static Type T_AbstractMaterialNode => GetSGType("UnityEditor.ShaderGraph.AbstractMaterialNode");
        public static Type T_CustomFunctionNode => GetSGType("UnityEditor.ShaderGraph.CustomFunctionNode");
        public static Type T_ColorNode => GetSGType("UnityEditor.ShaderGraph.ColorNode");
        public static Type T_Vector1Node => GetSGType("UnityEditor.ShaderGraph.Vector1Node");
        public static Type T_Vector2Node => GetSGType("UnityEditor.ShaderGraph.Vector2Node");
        public static Type T_Vector3Node => GetSGType("UnityEditor.ShaderGraph.Vector3Node");
        public static Type T_Vector4Node => GetSGType("UnityEditor.ShaderGraph.Vector4Node");
        public static Type T_SampleTexture2DNode => GetSGType("UnityEditor.ShaderGraph.SampleTexture2DNode");
        public static Type T_SamplerStateNode => GetSGType("UnityEditor.ShaderGraph.SamplerStateNode");
        public static Type T_PropertyNode => GetSGType("UnityEditor.ShaderGraph.PropertyNode");
        public static Type T_BlockNode => GetSGType("UnityEditor.ShaderGraph.BlockNode");

        // Slots
        public static Type T_MaterialSlot => GetSGType("UnityEditor.ShaderGraph.MaterialSlot");
        public static Type T_Vector1MaterialSlot => GetSGType("UnityEditor.ShaderGraph.Vector1MaterialSlot");
        public static Type T_Vector2MaterialSlot => GetSGType("UnityEditor.ShaderGraph.Vector2MaterialSlot");
        public static Type T_Vector3MaterialSlot => GetSGType("UnityEditor.ShaderGraph.Vector3MaterialSlot");
        public static Type T_Vector4MaterialSlot => GetSGType("UnityEditor.ShaderGraph.Vector4MaterialSlot");
        public static Type T_ColorRGBMaterialSlot => GetSGType("UnityEditor.ShaderGraph.ColorRGBMaterialSlot");
        public static Type T_Texture2DMaterialSlot => GetSGType("UnityEditor.ShaderGraph.Texture2DMaterialSlot");
        public static Type T_Texture3DMaterialSlot => GetSGType("UnityEditor.ShaderGraph.Texture3DMaterialSlot");
        public static Type T_CubemapMaterialSlot => GetSGType("UnityEditor.ShaderGraph.CubemapMaterialSlot");
        public static Type T_SamplerStateMaterialSlot => GetSGType("UnityEditor.ShaderGraph.SamplerStateMaterialSlot");
        public static Type T_BooleanMaterialSlot => GetSGType("UnityEditor.ShaderGraph.BooleanMaterialSlot");
        public static Type T_Matrix2MaterialSlot => GetSGType("UnityEditor.ShaderGraph.Matrix2MaterialSlot");
        public static Type T_Matrix3MaterialSlot => GetSGType("UnityEditor.ShaderGraph.Matrix3MaterialSlot");
        public static Type T_Matrix4MaterialSlot => GetSGType("UnityEditor.ShaderGraph.Matrix4MaterialSlot");
        public static Type T_DynamicVectorMaterialSlot => GetSGType("UnityEditor.ShaderGraph.DynamicVectorMaterialSlot");

        // Properties
        public static Type T_AbstractShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.AbstractShaderProperty");
        public static Type T_Vector1ShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty");
        public static Type T_Vector2ShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty");
        public static Type T_Vector3ShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty");
        public static Type T_Vector4ShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty");
        public static Type T_ColorShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.ColorShaderProperty");
        public static Type T_Texture2DShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty");
        public static Type T_Texture3DShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.Texture3DShaderProperty");
        public static Type T_CubemapShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.CubemapShaderProperty");
        public static Type T_BooleanShaderProperty => GetSGType("UnityEditor.ShaderGraph.Internal.BooleanShaderProperty");
        public static Type T_GradientShaderProperty => GetSGType("UnityEditor.ShaderGraph.GradientShaderProperty");
        public static Type T_SamplerStateShaderProperty => GetSGType("UnityEditor.ShaderGraph.SamplerStateShaderProperty");
        public static Type T_Matrix2ShaderProperty => GetSGType("UnityEditor.ShaderGraph.Matrix2ShaderProperty");
        public static Type T_Matrix3ShaderProperty => GetSGType("UnityEditor.ShaderGraph.Matrix3ShaderProperty");
        public static Type T_Matrix4ShaderProperty => GetSGType("UnityEditor.ShaderGraph.Matrix4ShaderProperty");

        // Cache / Utility
        public static Type T_NodeClassCache => GetSGType("UnityEditor.ShaderGraph.NodeClassCache");
        public static Type T_TitleAttribute => GetSGType("UnityEditor.ShaderGraph.TitleAttribute");
        public static Type T_FileUtilities => GetSGType("UnityEditor.ShaderGraph.FileUtilities");

        // Internal
        public static Type T_SerializableTexture => GetSGType("UnityEditor.ShaderGraph.Internal.SerializableTexture");
        public static Type T_SerializableCubemap => GetSGType("UnityEditor.ShaderGraph.Internal.SerializableCubemap");
        public static Type T_TextureSamplerState => GetSGType("UnityEditor.ShaderGraph.Internal.TextureSamplerState");

        // Graphing
        public static Type T_SlotReference => GetSGType("UnityEditor.Graphing.SlotReference");
        public static Type T_BlackboardInputInfo => GetSGType("UnityEditor.Graphing.BlackboardInputInfo");
        public static Type T_AddItemToCategoryAction => GetSGType("UnityEditor.ShaderGraph.Drawing.AddItemToCategoryAction");

        // Enums
        public static Type T_SlotType => GetSGType("UnityEditor.Graphing.SlotType");
        public static Type T_FloatType => GetSGType("UnityEditor.ShaderGraph.Internal.FloatType");
        public static Type T_TextureType => GetSGType("UnityEditor.ShaderGraph.TextureType");
        public static Type T_NormalMapSpace => GetSGType("UnityEditor.ShaderGraph.NormalMapSpace");
        public static Type T_ColorMode => GetSGType("UnityEditor.ShaderGraph.Internal.ColorMode");
        public static Type T_HlslSourceType => GetSGType("UnityEditor.ShaderGraph.Drawing.HlslSourceType");
        public static Type T_ModificationScope => GetSGType("UnityEditor.Graphing.ModificationScope");
        public static Type T_ConcreteSlotValueType => GetSGType("UnityEditor.ShaderGraph.ConcreteSlotValueType");

        // Nested enums: TextureSamplerState.FilterMode, WrapMode, Anisotropic
        public static Type T_FilterMode => GetSGType("UnityEditor.ShaderGraph.Internal.TextureSamplerState+FilterMode");
        public static Type T_WrapMode => GetSGType("UnityEditor.ShaderGraph.Internal.TextureSamplerState+WrapMode");
        public static Type T_Anisotropic => GetSGType("UnityEditor.ShaderGraph.Internal.TextureSamplerState+Anisotropic");

        // Texture2DShaderProperty.DefaultType
        public static Type T_Texture2DDefaultType => GetSGType("UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty+DefaultType");

        // ColorNode.Color (nested struct)
        public static Type T_ColorNodeColor => GetSGType("UnityEditor.ShaderGraph.ColorNode+Color");

        // CategoryData
        public static Type T_CategoryData => GetSGType("UnityEditor.ShaderGraph.CategoryData");

        // Target & BlockFieldDescriptor (for asset creation)
        public static Type T_Target => GetSGType("UnityEditor.ShaderGraph.Target");
        public static Type T_BlockFieldDescriptor => GetSGType("UnityEditor.ShaderGraph.BlockFieldDescriptor");

        // GraphPrecision enum
        public static Type T_GraphPrecision => GetSGType("UnityEditor.ShaderGraph.Internal.GraphPrecision");

        // Always available (from ShaderGraph / UnityEngine assemblies)
        public static Type T_NormalDropOffSpace => GetSGType("UnityEditor.ShaderGraph.NormalDropOffSpace");
        public static Type T_CompareFunction => typeof(UnityEngine.Rendering.CompareFunction);

        // Built-in Target & Enum Types (from Unity.ShaderGraph.Editor assembly, always available)
        public static Type T_BuiltInTarget => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInTarget");
        public static Type T_BuiltInSubTarget => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInSubTarget");
        public static Type T_BuiltInLitSubTarget => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInLitSubTarget");
        public static Type T_BuiltInUnlitSubTarget => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInUnlitSubTarget");
        public static Type T_BuiltInSurfaceType => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.SurfaceType");
        public static Type T_BuiltInAlphaMode => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.AlphaMode");
        public static Type T_BuiltInRenderFace => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.RenderFace");
        public static Type T_BuiltInZWriteControl => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.ZWriteControl");
        public static Type T_BuiltInZTestMode => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.ZTestMode");
        public static Type T_BuiltInWorkflowMode => GetSGType("UnityEditor.Rendering.BuiltIn.ShaderGraph.WorkflowMode");

#if HDRP_INSTALLED
        // HDRP Target & Data types (loaded from HDRP assembly)
        private static Assembly _hdAsm;
        public static Assembly HDAssembly
        {
            get
            {
                if (_hdAsm == null)
                    _hdAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Editor");
                return _hdAsm;
            }
        }

        public static Type T_HDTarget => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDTarget");
        public static Type T_HDSubTarget => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDSubTarget");
        public static Type T_HDLitSubTarget => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDLitSubTarget");
        public static Type T_HDUnlitSubTarget => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDUnlitSubTarget");

        // HDRP Data types
        public static Type T_SystemData => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.SystemData");
        public static Type T_BuiltinData => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.BuiltinData");
        public static Type T_LightingData => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.LightingData");
        public static Type T_HDLitData => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDLitData");

        // HDRP Enums
        public static Type T_SurfaceType
        {
            get
            {
                var hdRuntimeAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Runtime");
                return hdRuntimeAsm?.GetType("UnityEditor.Rendering.HighDefinition.SurfaceType");
            }
        }

        public static Type T_BlendMode
        {
            get
            {
                var hdRuntimeAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Runtime");
                return hdRuntimeAsm?.GetType("UnityEditor.Rendering.HighDefinition.BlendMode");
            }
        }
        public static Type T_DoubleSidedMode => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.DoubleSidedMode");
        public static Type T_SpecularOcclusionMode => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.SpecularOcclusionMode");
        public static Type T_TessellationMode
        {
            get
            {
                var hdRuntimeAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Runtime");
                return hdRuntimeAsm?.GetType("UnityEditor.Rendering.HighDefinition.TessellationMode");
            }
        }

        public static Type T_RenderQueueType
        {
            get
            {
                var hdRuntimeAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Runtime");
                return hdRuntimeAsm?.GetType("UnityEngine.Rendering.HighDefinition.HDRenderQueue+RenderQueueType");
            }
        }
        public static Type T_OpaqueCullMode
        {
            get
            {
                var hdRuntimeAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Runtime");
                return hdRuntimeAsm?.GetType("UnityEditor.Rendering.HighDefinition.OpaqueCullMode");
            }
        }
        public static Type T_TransparentCullMode
        {
            get
            {
                var hdRuntimeAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Runtime");
                return hdRuntimeAsm?.GetType("UnityEditor.Rendering.HighDefinition.TransparentCullMode");
            }
        }
        public static Type T_HDLitMaterialTypeFlag => HDAssembly?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDLitData+MaterialTypeFlag");
        public static Type T_RefractionModel
        {
            get
            {
                var hdRuntimeAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Runtime");
                return hdRuntimeAsm?.GetType("UnityEngine.Rendering.HighDefinition.ScreenSpaceRefraction+RefractionModel");
            }
        }
#endif

        // ================================================================
        //  Enum Helpers
        // ================================================================
        public static object ParseEnum(Type enumType, string value)
        {
            if (enumType == null || string.IsNullOrEmpty(value)) return null;
            try { return Enum.Parse(enumType, value, true); }
            catch { return null; }
        }

        public static object ParseEnum(string typeFullName, string value)
            => ParseEnum(GetSGType(typeFullName), value);

        public static string EnumToString(object enumValue)
            => enumValue?.ToString();

        // ================================================================
        //  Window Helpers
        // ================================================================
        public static object GetFocusedShaderGraphWindow()
        {
            if (!IsAvailable) return null;
            var windowType = T_MaterialGraphEditWindow;
            if (windowType == null) return null;
            var windows = Resources.FindObjectsOfTypeAll(windowType);
            if (windows == null || windows.Length == 0) return null;

            foreach (var w in windows)
            {
                if (w is EditorWindow ew && ew.hasFocus) return w;
            }
            return windows[0];
        }

        // ================================================================
        //  Graph Access Chain: window -> graphEditorView -> graphView -> graph
        // ================================================================
        public static object GetGraphFromWindow(object window)
        {
            if (window == null) return null;
            var gev = Prop(window, "graphEditorView");
            if (gev == null) return null;
            var gv = Prop(gev, "graphView");
            if (gv == null) return null;
            return Prop(gv, "graph");
        }

        public static object GetGraphObjectFromWindow(object window)
            => window == null ? null : Prop(window, "graphObject");

        // ================================================================
        //  Node Enumeration
        // ================================================================
        public static IEnumerable<object> GetNodesFromGraph(object graph)
        {
            if (graph == null) yield break;
            var method = CacheMethod(T_GraphData, "GetNodes");
            if (method == null) yield break;
            var generic = method.MakeGenericMethod(T_AbstractMaterialNode);
            var result = generic.Invoke(graph, null);
            if (result is IEnumerable ie)
                foreach (var item in ie)
                    yield return item;
        }

        public static object FindNodeById(object graph, string nodeId)
            => GetNodesFromGraph(graph).FirstOrDefault(n => (string)Prop(n, "objectId") == nodeId);

        public static object FindNodeByName(object graph, string nodeName)
            => GetNodesFromGraph(graph).FirstOrDefault(n =>
            {
                var name = n is UnityEngine.Object uo ? uo.name : (string)Prop(n, "name");
                var displayName = Prop(n, "displayName") as string;
                return name == nodeName || displayName == nodeName;
            });

        public static object FindNode(object graph, string nodeId, string nodeName)
        {
            if (!string.IsNullOrEmpty(nodeId)) return FindNodeById(graph, nodeId);
            if (!string.IsNullOrEmpty(nodeName)) return FindNodeByName(graph, nodeName);
            return null;
        }

        // ================================================================
        //  Slot Access
        // ================================================================
        public static List<object> GetInputSlots(object node)
        {
            var list = CreateMaterialSlotList();
            // GetInputSlots<T>(List<T>) is a generic method definition — find it then MakeGeneric
            var genericDef = T_AbstractMaterialNode.GetMethods(All)
                .FirstOrDefault(m => m.Name == "GetInputSlots" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (genericDef != null)
            {
                var concrete = genericDef.MakeGenericMethod(T_MaterialSlot);
                concrete.Invoke(node, new object[] { list });
            }
            return ListToList(list);
        }

        public static List<object> GetOutputSlots(object node)
        {
            var list = CreateMaterialSlotList();
            var genericDef = T_AbstractMaterialNode.GetMethods(All)
                .FirstOrDefault(m => m.Name == "GetOutputSlots" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (genericDef != null)
            {
                var concrete = genericDef.MakeGenericMethod(T_MaterialSlot);
                concrete.Invoke(node, new object[] { list });
            }
            return ListToList(list);
        }

        public static object FindSlotOnNode(object node, int slotId)
        {
            var genericDef = T_AbstractMaterialNode.GetMethods(All)
                .FirstOrDefault(m => m.Name == "FindSlot" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (genericDef == null) return null;
            var concrete = genericDef.MakeGenericMethod(T_MaterialSlot);
            return concrete.Invoke(node, new object[] { slotId });
        }

        public static object FindInputSlotOnNode(object node, Type slotType, int slotId)
        {
            var genericDef = T_AbstractMaterialNode.GetMethods(All)
                .FirstOrDefault(m => m.Name == "FindInputSlot" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (genericDef == null) return null;
            var concrete = genericDef.MakeGenericMethod(slotType);
            return concrete.Invoke(node, new object[] { slotId });
        }

        private static object CreateMaterialSlotList()
        {
            var listType = typeof(List<>).MakeGenericType(T_MaterialSlot);
            return Activator.CreateInstance(listType);
        }

        private static List<object> ListToList(object list)
        {
            var result = new List<object>();
            if (list is IList il)
                foreach (var item in il) result.Add(item);
            return result;
        }

        // ================================================================
        //  Slot Add
        // ================================================================
        /// <summary>
        /// Add a MaterialSlot to a node via reflection.
        /// Calls AbstractMaterialNode.AddSlot(MaterialSlot, bool) and then Dirty().
        /// </summary>
        public static bool AddSlotToNode(object node, object slot)
        {
            if (node == null || slot == null) return false;
            var method = CacheMethod(T_AbstractMaterialNode, "AddSlot");
            if (method == null)
            {
                Debug.LogWarning("[SGReflection] AddSlot method not found on AbstractMaterialNode");
                return false;
            }
            // AddSlot(MaterialSlot slot, bool attemptToModifyExistingInstance)
            // If it's generic, make concrete; otherwise call directly
            if (method.IsGenericMethodDefinition)
            {
                var concrete = method.MakeGenericMethod(slot.GetType());
                concrete.Invoke(node, new object[] { slot, false });
            }
            else
            {
                method.Invoke(node, new object[] { slot, false });
            }
            DirtyNode(node);
            return true;
        }

        // ================================================================
        //  Slot Info Extraction
        // ================================================================
        public static int GetSlotId(object slot) => (int)Prop(slot, "id");
        public static string GetSlotDisplayName(object slot) => (string)Prop(slot, "displayName");
        public static string GetSlotRawDisplayName(object slot) => (string)CacheMethod(slot.GetType(), "RawDisplayName")?.Invoke(slot, null);
        public static string GetSlotTypeString(object slot) => Prop(slot, "slotType")?.ToString();

        public static object MakeSlotInfo(object slot)
            => new { id = GetSlotId(slot), displayName = GetSlotDisplayName(slot), slotType = GetSlotTypeString(slot) };

        /// <summary>
        /// Returns the value-type name string for CreateMaterialSlot (e.g. "Vector1", "Vector2",
        /// "Color", "Texture2D") based on the slot's C# type, NOT the SlotType enum (Input/Output).
        /// </summary>
        public static string GetSlotTypeName(object slot)
        {
            if (slot == null) return "Vector1";
            var t = slot.GetType();
            if (t == T_Vector1MaterialSlot || t.IsSubclassOf(T_Vector1MaterialSlot)) return "Vector1";
            if (t == T_Vector2MaterialSlot || t.IsSubclassOf(T_Vector2MaterialSlot)) return "Vector2";
            if (t == T_Vector3MaterialSlot || t.IsSubclassOf(T_Vector3MaterialSlot)) return "Vector3";
            if (t == T_Vector4MaterialSlot || t.IsSubclassOf(T_Vector4MaterialSlot)) return "Vector4";
            if (t == T_ColorRGBMaterialSlot || t.IsSubclassOf(T_ColorRGBMaterialSlot)) return "Color";
            if (t == T_Texture2DMaterialSlot || t.IsSubclassOf(T_Texture2DMaterialSlot)) return "Texture2D";
            if (t == T_Texture3DMaterialSlot || t.IsSubclassOf(T_Texture3DMaterialSlot)) return "Texture3D";
            if (t == T_CubemapMaterialSlot || t.IsSubclassOf(T_CubemapMaterialSlot)) return "Cubemap";
            if (t == T_SamplerStateMaterialSlot || t.IsSubclassOf(T_SamplerStateMaterialSlot)) return "SamplerState";
            if (t == T_BooleanMaterialSlot || t.IsSubclassOf(T_BooleanMaterialSlot)) return "Boolean";
            if (t == T_Matrix2MaterialSlot || t.IsSubclassOf(T_Matrix2MaterialSlot)) return "Matrix2";
            if (t == T_Matrix3MaterialSlot || t.IsSubclassOf(T_Matrix3MaterialSlot)) return "Matrix3";
            if (t == T_Matrix4MaterialSlot || t.IsSubclassOf(T_Matrix4MaterialSlot)) return "Matrix4";
            if (t == T_DynamicVectorMaterialSlot || t.IsSubclassOf(T_DynamicVectorMaterialSlot)) return "DynamicVector";
            return "Vector1";
        }

        /// <summary>
        /// Returns the shaderOutputName of a slot (the HLSL parameter name).
        /// </summary>
        public static string GetSlotShaderOutputName(object slot) => (string)Prop(slot, "shaderOutputName");

        // ================================================================
        //  Property Access
        // ================================================================
        public static IEnumerable<object> GetPropertiesFromGraph(object graph)
        {
            var props = Prop(graph, "properties") as IEnumerable;
            if (props == null) yield break;
            foreach (var p in props) yield return p;
        }

        public static object FindPropertyByRefName(object graph, string refName)
            => GetPropertiesFromGraph(graph).FirstOrDefault(p => (string)Prop(p, "referenceName") == refName);

        public static object FindPropertyByDisplayName(object graph, string displayName)
            => GetPropertiesFromGraph(graph).FirstOrDefault(p => (string)Prop(p, "displayName") == displayName);

        public static object FindProperty(object graph, string referenceName, string displayName)
        {
            if (!string.IsNullOrEmpty(referenceName))
            {
                var p = FindPropertyByRefName(graph, referenceName);
                if (p != null) return p;
            }
            if (!string.IsNullOrEmpty(displayName))
                return FindPropertyByDisplayName(graph, displayName);
            return null;
        }

        // ================================================================
        //  Node Creation
        // ================================================================
        public static Type FindNodeTypeByName(string nodeTypeName)
            => GetKnownNodeTypes().FirstOrDefault(t => t.Name.Equals(nodeTypeName, StringComparison.OrdinalIgnoreCase));

        public static Type FindShaderPropertyType(string propertyTypeName)
        {
            if (!IsAvailable) return null;
            // Search in the ShaderGraph assembly for types deriving from AbstractShaderProperty
            return SGAssembly.GetTypes()
                .FirstOrDefault(t => !t.IsAbstract
                    && t.Name.Equals(propertyTypeName, StringComparison.OrdinalIgnoreCase)
                    && T_AbstractShaderProperty.IsAssignableFrom(t));
        }

        public static IEnumerable<Type> GetKnownNodeTypes()
        {
            // knownNodeTypes is a property, not a field
            var prop = CacheProp(T_NodeClassCache, "knownNodeTypes");
            if (prop != null)
            {
                var value = prop.GetValue(null);
                if (value is IEnumerable<Type> types) return types;
                if (value is IEnumerable ie) return ie.Cast<Type>();
            }
            // Fallback: try field
            var field = CacheField(T_NodeClassCache, "knownNodeTypes");
            if (field != null)
            {
                var value = field.GetValue(null);
                if (value is IEnumerable<Type> types) return types;
                if (value is IEnumerable ie) return ie.Cast<Type>();
            }
            return Enumerable.Empty<Type>();
        }

        public static object CreateNode(Type nodeType, float x = 200, float y = 200)
        {
            var node = Activator.CreateInstance(nodeType);
            SetNodePosition(node, x, y);
            return node;
        }

        public static void SetNodePosition(object node, float x, float y)
        {
            var drawState = Prop(node, "drawState");
            var position = (Rect)Prop(drawState, "position");
            var newDrawState = drawState;
            CacheProp(drawState.GetType(), "position")?.SetValue(newDrawState, new Rect(new Vector2(x, y), position.size));
            SetProp(node, "drawState", newDrawState);
        }

        public static void AddNodeToGraph(object graph, object node, string undoMessage = "Add Node")
        {
            var owner = Prop(graph, "owner");
            CacheMethod(owner.GetType(), "RegisterCompleteObjectUndo")?.Invoke(owner, new object[] { undoMessage });
            CacheMethod(T_GraphData, "AddNode")?.Invoke(graph, new object[] { node });
        }

        public static void RemoveNodeFromGraph(object graph, object node, string undoMessage = "Delete Node")
        {
            var owner = Prop(graph, "owner");
            CacheMethod(owner.GetType(), "RegisterCompleteObjectUndo")?.Invoke(owner, new object[] { undoMessage });
            CacheMethod(T_GraphData, "RemoveNode")?.Invoke(graph, new object[] { node });
        }

        // ================================================================
        //  Graph Input (Property) Add/Remove
        // ================================================================
        public static void AddGraphInput(object graph, object property, string undoMessage = "Add Property")
        {
            var owner = Prop(graph, "owner");
            CacheMethod(owner.GetType(), "RegisterCompleteObjectUndo")?.Invoke(owner, new object[] { undoMessage });
            // AddGraphInput(ShaderInput input, int index) — index = -1 to append at end
            CacheMethod(T_GraphData, "AddGraphInput")?.Invoke(graph, new object[] { property, -1 });
        }

        public static void RemoveGraphInput(object graph, object property, string undoMessage = "Delete Property")
        {
            var owner = Prop(graph, "owner");
            CacheMethod(owner.GetType(), "RegisterCompleteObjectUndo")?.Invoke(owner, new object[] { undoMessage });
            CacheMethod(T_GraphData, "RemoveGraphInput")?.Invoke(graph, new object[] { property });
        }

        // ================================================================
        //  Connection / Edge
        // ================================================================
        public static object CreateSlotReference(object node, int slotId)
            => Activator.CreateInstance(T_SlotReference, node, slotId);

        public static object ConnectSlots(object graph, object fromNode, int fromSlotId, object toNode, int toSlotId)
        {
            var fromRef = CreateSlotReference(fromNode, fromSlotId);
            var toRef = CreateSlotReference(toNode, toSlotId);
            var owner = Prop(graph, "owner");
            CacheMethod(owner.GetType(), "RegisterCompleteObjectUndo")?.Invoke(owner, new object[] { "Connect Nodes" });
            return CacheMethod(T_GraphData, "Connect")?.Invoke(graph, new object[] { fromRef, toRef });
        }

        public static IEnumerable<object> GetEdges(object graph)
        {
            var edges = Prop(graph, "edges") as IEnumerable;
            if (edges == null) yield break;
            foreach (var e in edges) yield return e;
        }

        public static void RemoveEdge(object graph, object edge)
            => CacheMethod(T_GraphData, "RemoveEdge")?.Invoke(graph, new object[] { edge });

        /// <summary>
        /// Refresh the ShaderGraph UI after data changes (add/remove nodes, connect/disconnect edges, etc.)
        /// </summary>
        public static void RefreshGraphUI()
        {
            var window = GetFocusedShaderGraphWindow();
            if (window == null) return;

            // Force the InspectorView to rebuild its UI by setting doesInspectorNeedUpdate = true
            var gev = Prop(window, "graphEditorView");
            if (gev != null)
            {
                var inspectorViewField = CacheField(gev.GetType(), "m_InspectorView")
                    ?? gev.GetType().GetFields(All).FirstOrDefault(f => f.FieldType.Name == "InspectorView");
                if (inspectorViewField != null)
                {
                    var inspectorView = inspectorViewField.GetValue(gev);
                    if (inspectorView != null)
                    {
                        // Set doesInspectorNeedUpdate = true
                        var needUpdateProp = inspectorView.GetType().GetProperty("doesInspectorNeedUpdate",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        needUpdateProp?.SetValue(inspectorView, true);
                    }
                }
            }

            // Call the window's Update method which triggers graph change processing
            var updateMethod = window.GetType().GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            updateMethod?.Invoke(window, new object[0]);

            // Repaint the window
            (window as UnityEditor.EditorWindow)?.Repaint();
        }

        public static object GetEdgeOutputNode(object edge) => Prop(Prop(edge, "outputSlot"), "node");
        public static object GetEdgeInputNode(object edge) => Prop(Prop(edge, "inputSlot"), "node");
        public static int GetEdgeOutputSlotId(object edge) => (int)Prop(Prop(edge, "outputSlot"), "slotId");
        public static int GetEdgeInputSlotId(object edge) => (int)Prop(Prop(edge, "inputSlot"), "slotId");

        // ================================================================
        //  Create ShaderGraph Asset — Block Descriptors Helper
        // ================================================================
        /// <summary>
        /// Returns BlockFieldDescriptor instances matching HDRP source (CreateHDLitShaderGraph / CreateHDUnlitShaderGraph).
        /// Uses reflection to read static fields from BlockFields and HDBlockFields nested types.
        /// </summary>
#if HDRP_INSTALLED
        private static object[] GetBlockDescriptorsForTarget(string targetType)
        {
            var sgAsm = SGAssembly;
            var hdAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Editor");
            if (hdAsm == null) return null;

            // Helper: get BlockFieldDescriptor from BlockFields.VertexDescription.X or HDBlockFields.SurfaceDescription.X
            var bfVertType = sgAsm?.GetType("UnityEditor.ShaderGraph.BlockFields+VertexDescription");
            var bfSurfType = sgAsm?.GetType("UnityEditor.ShaderGraph.BlockFields+SurfaceDescription");
            var hdSurfType = hdAsm.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDBlockFields+SurfaceDescription");
            var AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            object GetBlock(Type nestedType, string fieldName)
            {
                if (nestedType == null) return null;
                return nestedType.GetField(fieldName, AllStatic)?.GetValue(null);
            }

            if (targetType == "HDRP_Lit")
            {
                return new object[]
                {
                    GetBlock(bfVertType, "Position"),
                    GetBlock(bfVertType, "Normal"),
                    GetBlock(bfVertType, "Tangent"),
                    GetBlock(bfSurfType, "BaseColor"),
                    GetBlock(bfSurfType, "NormalTS"),
                    GetBlock(hdSurfType, "BentNormal"),
                    GetBlock(bfSurfType, "Metallic"),
                    GetBlock(bfSurfType, "Emission"),
                    GetBlock(bfSurfType, "Smoothness"),
                    GetBlock(bfSurfType, "Occlusion"),
                    GetBlock(bfSurfType, "Alpha"),
                };
            }
            else if (targetType == "HDRP_Unlit")
            {
                return new object[]
                {
                    GetBlock(bfVertType, "Position"),
                    GetBlock(bfVertType, "Normal"),
                    GetBlock(bfVertType, "Tangent"),
                    GetBlock(bfSurfType, "BaseColor"),
                    GetBlock(bfSurfType, "Emission"),
                    GetBlock(bfSurfType, "Alpha"),
                };
            }

            return null;
        }
#endif // HDRP_INSTALLED

        /// <summary>
        /// Returns BlockFieldDescriptor instances for Fullscreen SubTarget.
        /// FullscreenBlocks lives in Unity.ShaderGraph.Editor assembly.
        /// </summary>
        public static object[] GetFullscreenBlockDescriptors()
        {
            var sgAsm = SGAssembly;
            var fbType = sgAsm?.GetType("UnityEditor.Rendering.Fullscreen.ShaderGraph.FullscreenBlocks");
            if (fbType == null) return null;

            var AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var colorBlock = fbType.GetField("color", AllStatic)?.GetValue(null);
            if (colorBlock == null) return null;

            return new object[] { colorBlock };
        }

        /// <summary>
        /// Returns BlockFieldDescriptor instances for URP SubTargets (Lit, Unlit, Decal).
        /// </summary>
#if URP_INSTALLED
        public static object[] GetURPBlockDescriptors(string subTargetTypeName)
        {
            var sgAsm = SGAssembly;
            var AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            var bfVertType = sgAsm?.GetType("UnityEditor.ShaderGraph.BlockFields+VertexDescription");
            var bfSurfType = sgAsm?.GetType("UnityEditor.ShaderGraph.BlockFields+SurfaceDescription");
            var urpSurfType = URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalBlockFields+SurfaceDescription");

            object GetBlock(Type nestedType, string fieldName)
            {
                if (nestedType == null) return null;
                return nestedType.GetField(fieldName, AllStatic)?.GetValue(null);
            }

            if (subTargetTypeName == "UniversalLitSubTarget")
            {
                return new object[]
                {
                    GetBlock(bfVertType, "Position"),
                    GetBlock(bfVertType, "Normal"),
                    GetBlock(bfVertType, "Tangent"),
                    GetBlock(bfSurfType, "BaseColor"),
                    GetBlock(bfSurfType, "NormalTS"),
                    GetBlock(bfSurfType, "Metallic"),
                    GetBlock(bfSurfType, "Smoothness"),
                    GetBlock(bfSurfType, "Emission"),
                    GetBlock(bfSurfType, "Occlusion"),
                    GetBlock(bfSurfType, "Alpha"),
                };
            }
            else if (subTargetTypeName == "UniversalUnlitSubTarget")
            {
                return new object[]
                {
                    GetBlock(bfVertType, "Position"),
                    GetBlock(bfVertType, "Normal"),
                    GetBlock(bfVertType, "Tangent"),
                    GetBlock(bfSurfType, "BaseColor"),
                    GetBlock(bfSurfType, "Emission"),
                    GetBlock(bfSurfType, "Alpha"),
                };
            }
            else if (subTargetTypeName == "UniversalDecalSubTarget")
            {
                // URP Decal blocks: Base Color, NormalTS, Alpha + decal-specific blocks
                var blocks = new List<object>();
                var baseColor = GetBlock(bfSurfType, "BaseColor");
                if (baseColor != null) blocks.Add(baseColor);
                var normalTS = GetBlock(bfSurfType, "NormalTS");
                if (normalTS != null) blocks.Add(normalTS);
                var alpha = GetBlock(bfSurfType, "Alpha");
                if (alpha != null) blocks.Add(alpha);
                // Decal-specific blocks from UniversalBlockFields
                if (urpSurfType != null)
                {
                    foreach (var f in urpSurfType.GetFields(AllStatic))
                    {
                        if (f.FieldType == T_BlockFieldDescriptor || (T_BlockFieldDescriptor != null && T_BlockFieldDescriptor.IsAssignableFrom(f.FieldType)))
                        {
                            var val = f.GetValue(null);
                            if (val != null && !blocks.Contains(val))
                                blocks.Add(val);
                        }
                    }
                }
                return blocks.ToArray();
            }

            return null;
        }
#endif // URP_INSTALLED

        /// <summary>
        /// Re-initialize output Block nodes for the current active targets.
        /// This should be called after switching SubTarget to ensure the correct
        /// Fragment/Vertex Context blocks are created.
        /// </summary>
        public static bool InitializeOutputBlocks(object graph, object target, string subTargetTypeName)
        {
            if (graph == null || T_GraphData == null) return false;

            // Build Target[] array
            var targetArray = Array.CreateInstance(T_Target, 1);
            targetArray.SetValue(target, 0);

            // Get block descriptors
            object[] blockDescs = null;

#if URP_INSTALLED
            if (target != null && T_UniversalTarget != null && T_UniversalTarget.IsAssignableFrom(target.GetType()))
            {
                blockDescs = GetURPBlockDescriptors(subTargetTypeName ?? "UniversalLitSubTarget");
            }
            else
#endif
#if HDRP_INSTALLED
            if (target != null && T_HDTarget != null && T_HDTarget.IsAssignableFrom(target.GetType()))
            {
                var activeSub = GetActiveSubTarget(target);
                var subName = activeSub?.GetType().Name;
                if (subName == "HDLitSubTarget") blockDescs = GetBlockDescriptorsForTarget("HDRP_Lit");
                else if (subName == "HDUnlitSubTarget") blockDescs = GetBlockDescriptorsForTarget("HDRP_Unlit");
                else if (subName == "HDFullscreenSubTarget") blockDescs = GetFullscreenBlockDescriptors();
            }
            else
#endif
            if (target != null && T_BuiltInTarget != null && T_BuiltInTarget.IsAssignableFrom(target.GetType()))
            {
                var activeSub = GetBuiltInActiveSubTarget(target);
                var subName = activeSub?.GetType().Name;
                if (string.IsNullOrEmpty(subName)) subName = subTargetTypeName;
                blockDescs = GetBuiltInBlockDescriptors(subName ?? "BuiltInLitSubTarget");
            }

            var blockArray = blockDescs != null ? Array.CreateInstance(T_BlockFieldDescriptor, blockDescs.Length) : null;
            if (blockArray != null)
            {
                for (int i = 0; i < blockDescs.Length; i++)
                    blockArray.SetValue(blockDescs[i], i);
            }

            var initMethod = T_GraphData.GetMethod("InitializeOutputs", All, null,
                new Type[] { T_Target.MakeArrayType(), T_BlockFieldDescriptor.MakeArrayType() }, null);
            if (initMethod == null) return false;

            initMethod.Invoke(graph, new object[] { targetArray, blockArray });
            return true;
        }

        // ================================================================
        //  Create ShaderGraph Asset
        // ================================================================
        /// <summary>
        /// Create a new .shadergraph file on disk and optionally open it.
        /// Mirrors NewGraphAction.Action() and CreateHDLitGraph() source.
        /// Supported target types: "Builtin_Lit", "Builtin_Unlit", "URP_Lit", "URP_Unlit", "URP_Decal", "HDRP_Lit", "HDRP_Unlit", "Blank", "Auto".
        /// </summary>
        public static object CreateShaderGraphAsset(string path, string targetType = null, bool openAfterCreate = true)
        {
            if (!path.StartsWith("Assets/")) path = "Assets/" + path;
            if (!path.EndsWith(".shadergraph")) path += ".shadergraph";

            if (File.Exists(path))
                return new { success = false, message = $"File already exists: {path}" };

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // 1. Create GraphData
            var graph = Activator.CreateInstance(T_GraphData);

            // 2. AddContexts
            CacheMethod(T_GraphData, "AddContexts")?.Invoke(graph, null);

            // 3. InitializeOutputs with target + block descriptors
            var targetNames = new List<string>();

            // Auto-detect pipeline
            if (string.IsNullOrEmpty(targetType) || targetType == "Auto")
            {
#if URP_INSTALLED
                targetType = "URP_Lit";
#elif HDRP_INSTALLED
                targetType = "HDRP_Lit";
#else
                targetType = "Builtin_Lit";
#endif
            }

            if (!string.IsNullOrEmpty(targetType) && targetType != "Blank")
            {
                var initMethod = T_GraphData.GetMethod("InitializeOutputs", All, null,
                    new Type[] { T_Target.MakeArrayType(), T_BlockFieldDescriptor.MakeArrayType() }, null);

#if URP_INSTALLED
                if (targetType.StartsWith("URP_") || targetType.StartsWith("Universal"))
                {
                    var urpTargetT = URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
                    if (urpTargetT != null)
                    {
                        var urpTarget = Activator.CreateInstance(urpTargetT);

                        Type subType = null;
                        string subTypeName = null;
                        if (targetType == "URP_Lit" || targetType == "UniversalLitSubTarget")
                        {
                            subTypeName = "UniversalLitSubTarget";
                            subType = URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget");
                        }
                        else if (targetType == "URP_Unlit" || targetType == "UniversalUnlitSubTarget")
                        {
                            subTypeName = "UniversalUnlitSubTarget";
                            subType = URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget");
                        }
                        else if (targetType == "URP_Decal" || targetType == "UniversalDecalSubTarget")
                        {
                            subTypeName = "UniversalDecalSubTarget";
                            subType = URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalDecalSubTarget");
                        }

                        if (subType != null)
                        {
                            var trySetActive = urpTargetT.GetMethod("TrySetActiveSubTarget",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            trySetActive?.Invoke(urpTarget, new object[] { subType });

                            if (subTypeName == "UniversalDecalSubTarget")
                            {
                                var surfaceTypeField = urpTargetT.GetField("m_SurfaceType",
                                    BindingFlags.NonPublic | BindingFlags.Instance);
                                if (surfaceTypeField != null) surfaceTypeField.SetValue(urpTarget, 1);
                            }

                            targetNames.Add(subTypeName);
                        }

                        var targetArray = Array.CreateInstance(T_Target, 1);
                        targetArray.SetValue(urpTarget, 0);

                        var blocks = GetURPBlockDescriptors(subTypeName ?? "UniversalLitSubTarget");
                        var blockArray = blocks != null ? Array.CreateInstance(T_BlockFieldDescriptor, blocks.Length) : null;
                        if (blockArray != null)
                        {
                            for (int i = 0; i < blocks.Length; i++)
                                blockArray.SetValue(blocks[i], i);
                        }

                        initMethod?.Invoke(graph, new object[] { targetArray, blockArray });
                    }
                }
                else
#endif
#if HDRP_INSTALLED
                if (targetType.StartsWith("HDRP_"))
                {
                    var hdAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.HighDefinition.Editor");

                    var targetT = hdAsm?.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDTarget");
                    if (targetT != null)
                    {
                        var hdTarget = Activator.CreateInstance(targetT);

                        Type subTargetType = null;
                        if (targetType == "HDRP_Unlit")
                            subTargetType = hdAsm.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDUnlitSubTarget");
                        else if (targetType == "HDRP_Lit")
                            subTargetType = hdAsm.GetType("UnityEditor.Rendering.HighDefinition.ShaderGraph.HDLitSubTarget");

                        if (subTargetType != null)
                        {
                            var trySetActive = targetT.GetMethod("TrySetActiveSubTarget",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            trySetActive?.Invoke(hdTarget, new object[] { subTargetType });
                            targetNames.Add(subTargetType.Name);
                        }

                        var targetArray = Array.CreateInstance(T_Target, 1);
                        targetArray.SetValue(hdTarget, 0);

                        var blocks = GetBlockDescriptorsForTarget(targetType);
                        var blockArray = blocks != null ? Array.CreateInstance(T_BlockFieldDescriptor, blocks.Length) : null;
                        if (blockArray != null)
                        {
                            for (int i = 0; i < blocks.Length; i++)
                                blockArray.SetValue(blocks[i], i);
                        }

                        initMethod?.Invoke(graph, new object[] { targetArray, blockArray });
                    }
                }
                else
#endif
                if (targetType.StartsWith("Builtin_") || targetType.StartsWith("BuiltIn"))
                {
                    if (T_BuiltInTarget != null)
                    {
                        var biTarget = Activator.CreateInstance(T_BuiltInTarget);

                        Type subType = null;
                        string subTypeName = null;
                        if (targetType == "Builtin_Lit" || targetType == "BuiltInLitSubTarget")
                        {
                            subTypeName = "BuiltInLitSubTarget";
                            subType = T_BuiltInLitSubTarget;
                        }
                        else if (targetType == "Builtin_Unlit" || targetType == "BuiltInUnlitSubTarget")
                        {
                            subTypeName = "BuiltInUnlitSubTarget";
                            subType = T_BuiltInUnlitSubTarget;
                        }

                        if (subType != null)
                        {
                            var trySetActive = T_BuiltInTarget.GetMethod("TrySetActiveSubTarget",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            trySetActive?.Invoke(biTarget, new object[] { subType });
                            targetNames.Add(subTypeName);
                        }

                        var targetArray = Array.CreateInstance(T_Target, 1);
                        targetArray.SetValue(biTarget, 0);

                        var blocks = GetBuiltInBlockDescriptors(subTypeName ?? "BuiltInLitSubTarget");
                        var blockArray = blocks != null ? Array.CreateInstance(T_BlockFieldDescriptor, blocks.Length) : null;
                        if (blockArray != null)
                        {
                            for (int i = 0; i < blocks.Length; i++)
                                blockArray.SetValue(blocks[i], i);
                        }

                        initMethod?.Invoke(graph, new object[] { targetArray, blockArray });
                    }
                }
            }
            else
            {
                var initMethod = T_GraphData.GetMethod("InitializeOutputs", All, null,
                    new Type[] { T_Target.MakeArrayType(), T_BlockFieldDescriptor.MakeArrayType() }, null);
                initMethod?.Invoke(graph, new object[] { null, null });
            }

            // 4. AddCategory(DefaultCategory)
            var defaultCat = CacheMethod(T_CategoryData, "DefaultCategory")?.Invoke(null, new object[] { null });
            if (defaultCat != null)
                CacheMethod(T_GraphData, "AddCategory")?.Invoke(graph, new object[] { defaultCat });

            // 5. Set path
            SetProp(graph, "path", "Shader Graphs");

            // 6. Write to disk
            SaveShaderGraphToDisk(path, graph);

            // 7. Refresh
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            // 8. Optionally open
            if (openAfterCreate)
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (obj != null) AssetDatabase.OpenAsset(obj);
            }

            var targetArr = targetNames.Count > 0 ? targetNames.ToArray() : new[] { "Blank" };
            return new { success = true, message = $"Created Shader Graph: {path}", path, targets = targetArr };
        }

        // ================================================================
        //  Save
        // ================================================================
        public static object SaveShaderGraphToDisk(string path, object graph)
            => CacheMethod(T_FileUtilities, "WriteShaderGraphToDisk")?.Invoke(null, new object[] { path, graph });

        public static bool GraphHasErrors(object graph)
        {
            var mm = Prop(graph, "messageManager");
            if (mm == null) return false;
            // AnyError(Func<INode, bool> nodeFilter) — pass null to get all errors
            var method = mm.GetType().GetMethod("AnyError", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) return false;
            var result = method.Invoke(mm, new object[] { null });
            return result is bool b && b;
        }

        // ================================================================
        //  Error Messages
        // ================================================================
        public static IEnumerable<(string nodeId, object message)> GetNodeMessages(object graph)
        {
            var mm = Prop(graph, "messageManager");
            if (mm == null) yield break;
            var dict = CacheMethod(mm.GetType(), "GetNodeMessages")?.Invoke(mm, null);
            if (dict is IDictionary id)
            {
                foreach (DictionaryEntry kvp in id)
                {
                    var nodeId = kvp.Key.ToString();
                    if (kvp.Value is IEnumerable msgs)
                        foreach (var msg in msgs)
                            yield return (nodeId, msg);
                }
            }
        }

        // ================================================================
        //  Category / AddItemToCategoryAction
        // ================================================================
        public static void AddPropertyToDefaultCategory(object graph, object property)
        {
            var categories = Prop(graph, "categories") as IEnumerable;
            if (categories == null) return;
            var defaultCategory = categories.Cast<object>().FirstOrDefault();
            if (defaultCategory == null) return;

            var action = Activator.CreateInstance(T_AddItemToCategoryAction);
            SetProp(action, "categoryGuid", Prop(defaultCategory, "categoryGuid"));
            SetProp(action, "itemToAdd", property);

            var owner = Prop(graph, "owner");
            var dataStore = Prop(owner, "graphDataStore");
            CacheMethod(dataStore.GetType(), "Dispatch")?.Invoke(dataStore, new object[] { action });
        }

        // ================================================================
        //  Node Dirty
        // ================================================================
        public static void DirtyNode(object node)
        {
            var scope = ParseEnum(T_ModificationScope, "Graph");
            CacheMethod(T_AbstractMaterialNode, "Dirty")?.Invoke(node, new object[] { scope });
        }

        /// <summary>
        /// Calls node.ValidateNode() — the official ShaderGraph validation step after slot changes.
        /// </summary>
        public static void ValidateNode(object node)
            => CacheMethod(T_AbstractMaterialNode, "ValidateNode")?.Invoke(node, null);

        /// <summary>
        /// Calls slot.CopyValuesFrom(sourceSlot) — the official way to copy values between slots.
        /// </summary>
        public static void CopySlotValues(object destSlot, object srcSlot)
            => CacheMethod(destSlot.GetType(), "CopyValuesFrom")?.Invoke(destSlot, new object[] { srcSlot });

        /// <summary>
        /// Calls node.SetSlotOrder(List&lt;int&gt; slotIds) — maintains slot display order.
        /// </summary>
        public static void SetSlotOrder(object node, List<int> slotIds)
        {
            var method = T_AbstractMaterialNode.GetMethod("SetSlotOrder",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(node, new object[] { slotIds });
        }

        /// <summary>
        /// Gets the bareResource flag on a slot.
        /// </summary>
        public static bool GetSlotBareResource(object slot)
        {
            var field = CacheField(slot.GetType(), "m_BareResource");
            if (field != null) return (bool)field.GetValue(slot);
            return false;
        }

        /// <summary>
        /// Sets the bareResource flag on a slot.
        /// </summary>
        public static void SetSlotBareResource(object slot, bool value)
        {
            var field = CacheField(slot.GetType(), "m_BareResource");
            if (field != null) field.SetValue(slot, value);
        }

        public static void DirtyGraphNodes(object graph, object property)
        {
            foreach (var node in GetNodesFromGraph(graph))
            {
                if (node.GetType() == T_PropertyNode || node.GetType().IsSubclassOf(T_PropertyNode))
                {
                    var nodeProp = Prop(node, "property");
                    if (nodeProp == property)
                        DirtyNode(node);
                }
            }
        }

        // ================================================================
        //  Slot Value Read/Write
        // ================================================================
        public static object GetSlotValue(object slot)
        {
            var t = slot.GetType();
            if (t == T_Vector1MaterialSlot || t.IsSubclassOf(T_Vector1MaterialSlot))
                return Prop(slot, "value");
            if (t == T_Vector2MaterialSlot || t.IsSubclassOf(T_Vector2MaterialSlot))
                return Prop(slot, "value");
            if (t == T_Vector3MaterialSlot || t.IsSubclassOf(T_Vector3MaterialSlot))
                return Prop(slot, "value");
            if (t == T_Vector4MaterialSlot || t.IsSubclassOf(T_Vector4MaterialSlot))
                return Prop(slot, "value");
            if (t == T_BooleanMaterialSlot || t.IsSubclassOf(T_BooleanMaterialSlot))
                return Prop(slot, "value");
            return Prop(slot, "value");
        }

        public static void SetSlotValue(object slot, object value)
            => SetProp(slot, "value", value);

        // ================================================================
        //  Create Material Slot by Type Name
        // ================================================================
        public static object CreateMaterialSlot(string typeName, int slotId, string displayName, bool isInput)
        {
            var slotType = isInput ? ParseEnum(T_SlotType, "Input") : ParseEnum(T_SlotType, "Output");
            var shaderOutputName = displayName; // Use displayName as shaderOutputName by convention
            var stageCapability = ParseEnum(SGAssembly?.GetType("UnityEditor.ShaderGraph.ShaderStageCapability"), "All");

            switch (typeName.ToLowerInvariant())
            {
                case "float":
                case "vector1":
                    return Activator.CreateInstance(T_Vector1MaterialSlot, slotId, displayName, shaderOutputName, slotType, 0f, stageCapability, "", false);
                case "vector2":
                    return Activator.CreateInstance(T_Vector2MaterialSlot, slotId, displayName, shaderOutputName, slotType, Vector2.zero, stageCapability, "", "", false, false);
                case "vector3":
                    return Activator.CreateInstance(T_Vector3MaterialSlot, slotId, displayName, shaderOutputName, slotType, Vector3.zero, stageCapability, "", "", "", false);
                case "vector4":
                    return Activator.CreateInstance(T_Vector4MaterialSlot, slotId, displayName, shaderOutputName, slotType, Vector4.zero, stageCapability, "", "", "", "", false);
                case "color":
                {
                    var colorMode = ParseEnum(T_ColorMode, "Default");
                    return Activator.CreateInstance(T_ColorRGBMaterialSlot, slotId, displayName, shaderOutputName, slotType, Color.black, colorMode, stageCapability, false);
                }
                case "texture2d":
                    return Activator.CreateInstance(T_Texture2DMaterialSlot, slotId, displayName, shaderOutputName, slotType, stageCapability, false);
                case "texture3d":
                    return Activator.CreateInstance(T_Texture3DMaterialSlot, slotId, displayName, shaderOutputName, slotType, stageCapability, false);
                case "cubemap":
                    return Activator.CreateInstance(T_CubemapMaterialSlot, slotId, displayName, shaderOutputName, slotType, stageCapability, false);
                case "samplerstate":
                    return Activator.CreateInstance(T_SamplerStateMaterialSlot, slotId, displayName, shaderOutputName, slotType, stageCapability, false);
                case "boolean":
                case "bool":
                    return Activator.CreateInstance(T_BooleanMaterialSlot, slotId, displayName, shaderOutputName, slotType, false, stageCapability, false);
                case "matrix2":
                    return Activator.CreateInstance(T_Matrix2MaterialSlot, slotId, displayName, shaderOutputName, slotType, stageCapability, false);
                case "matrix3":
                    return Activator.CreateInstance(T_Matrix3MaterialSlot, slotId, displayName, shaderOutputName, slotType, stageCapability, false);
                case "matrix4":
                case "matrix":
                    return Activator.CreateInstance(T_Matrix4MaterialSlot, slotId, displayName, shaderOutputName, slotType, stageCapability, false);
                case "dynamic":
                case "dynamicvector":
                    return Activator.CreateInstance(T_DynamicVectorMaterialSlot, slotId, displayName, shaderOutputName, slotType, Vector4.zero, stageCapability, false);
                default:
                    Debug.LogWarning($"[SGReflection] Unknown slot type: {typeName}, using Float");
                    return Activator.CreateInstance(T_Vector1MaterialSlot, slotId, displayName, shaderOutputName, slotType, 0f, stageCapability, "", false);
            }
        }

        // ================================================================
        //  TitleAttribute Helper
        // ================================================================
        public static (string displayName, string category) GetNodeTitleInfo(Type nodeType)
        {
            string displayName = nodeType.Name;
            string category = "";

            var attrs = nodeType.GetCustomAttributes(T_TitleAttribute, false);
            if (attrs != null && attrs.Length > 0)
            {
                var titleArr = Prop(attrs[0], "title") as string[];
                if (titleArr != null && titleArr.Length > 0)
                {
                    displayName = titleArr[titleArr.Length - 1];
                    category = string.Join("/", titleArr);
                }
            }
            return (displayName, category);
        }

        // ================================================================
        //  Built-in Target Access (always available)
        // ================================================================

        /// <summary>
        /// Get the first active BuiltInTarget from the graph, or null.
        /// </summary>
        public static object GetBuiltInTarget(object graph)
        {
            if (graph == null || T_BuiltInTarget == null) return null;
            foreach (var t in (IEnumerable)Prop(graph, "activeTargets"))
            {
                if (t != null && T_BuiltInTarget.IsAssignableFrom(t.GetType()))
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Get the active SubTarget from a BuiltInTarget.
        /// </summary>
        public static object GetBuiltInActiveSubTarget(object builtInTarget)
        {
            if (builtInTarget == null) return null;
            return Prop(builtInTarget, "activeSubTarget");
        }

        /// <summary>
        /// Try to set the active SubTarget on a BuiltInTarget by type name
        /// (e.g. "BuiltInLitSubTarget", "BuiltInUnlitSubTarget").
        /// </summary>
        public static bool TrySetBuiltInActiveSubTarget(object builtInTarget, string subTargetTypeName)
        {
            if (builtInTarget == null || T_BuiltInTarget == null) return false;
            var method = CacheMethod(T_BuiltInTarget, "TrySetActiveSubTarget");
            if (method == null) return false;

            Type subType = null;
            if (SGAssembly != null)
            {
                foreach (var t in SGAssembly.GetTypes())
                {
                    if (t.Name.Equals(subTargetTypeName, StringComparison.OrdinalIgnoreCase) &&
                        T_BuiltInSubTarget != null && T_BuiltInSubTarget.IsAssignableFrom(t))
                    {
                        subType = t;
                        break;
                    }
                }
            }
            if (subType == null) return false;

            var result = method.Invoke(builtInTarget, new object[] { subType });
            return result is bool b && b;
        }

        /// <summary>
        /// Returns BlockFieldDescriptor instances for Built-in SubTargets (Lit, Unlit).
        /// </summary>
        public static object[] GetBuiltInBlockDescriptors(string subTargetTypeName)
        {
            var sgAsm = SGAssembly;
            var AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            var bfVertType = sgAsm?.GetType("UnityEditor.ShaderGraph.BlockFields+VertexDescription");
            var bfSurfType = sgAsm?.GetType("UnityEditor.ShaderGraph.BlockFields+SurfaceDescription");

            object GetBlock(Type nestedType, string fieldName)
            {
                if (nestedType == null) return null;
                return nestedType.GetField(fieldName, AllStatic)?.GetValue(null);
            }

            if (subTargetTypeName == "BuiltInLitSubTarget")
            {
                return new object[]
                {
                    GetBlock(bfVertType, "Position"),
                    GetBlock(bfVertType, "Normal"),
                    GetBlock(bfVertType, "Tangent"),
                    GetBlock(bfSurfType, "BaseColor"),
                    GetBlock(bfSurfType, "NormalTS"),
                    GetBlock(bfSurfType, "Metallic"),
                    GetBlock(bfSurfType, "Smoothness"),
                    GetBlock(bfSurfType, "Emission"),
                    GetBlock(bfSurfType, "Occlusion"),
                    GetBlock(bfSurfType, "Alpha"),
                };
            }
            else if (subTargetTypeName == "BuiltInUnlitSubTarget")
            {
                return new object[]
                {
                    GetBlock(bfVertType, "Position"),
                    GetBlock(bfVertType, "Normal"),
                    GetBlock(bfVertType, "Tangent"),
                    GetBlock(bfSurfType, "BaseColor"),
                    GetBlock(bfSurfType, "Emission"),
                    GetBlock(bfSurfType, "Alpha"),
                };
            }

            return null;
        }

#if HDRP_INSTALLED
        // ================================================================
        //  HDRP Target & Data Access
        // ================================================================

        /// <summary>
        /// Get the first active HDTarget from the graph, or null.
        /// </summary>
        public static object GetHDTarget(object graph)
        {
            if (graph == null || T_HDTarget == null) return null;
            foreach (var t in (IEnumerable)Prop(graph, "activeTargets"))
            {
                if (t != null && T_HDTarget.IsAssignableFrom(t.GetType()))
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Get the active SubTarget from an HDTarget (e.g. HDLitSubTarget, HDUnlitSubTarget).
        /// </summary>
        public static object GetActiveSubTarget(object hdTarget)
        {
            if (hdTarget == null) return null;
            return Prop(hdTarget, "activeSubTarget");
        }

        /// <summary>
        /// Get SystemData from an HDTarget via its active SubTarget.
        /// The SubTarget implements IRequiresData&lt;SystemData&gt;.
        /// </summary>
        public static object GetSystemData(object hdTarget)
        {
            var sub = GetActiveSubTarget(hdTarget);
            if (sub == null) return null;
            // IRequiresData<SystemData> has a "data" property
            return Prop(sub, "systemData");
        }

        /// <summary>
        /// Get BuiltinData from a SurfaceSubTarget.
        /// </summary>
        public static object GetBuiltinData(object hdTarget)
        {
            var sub = GetActiveSubTarget(hdTarget);
            if (sub == null) return null;
            return Prop(sub, "builtinData");
        }

        /// <summary>
        /// Get LightingData from a LightingSubTarget. Returns null for Unlit.
        /// </summary>
        public static object GetLightingData(object hdTarget)
        {
            var sub = GetActiveSubTarget(hdTarget);
            if (sub == null) return null;
            return Prop(sub, "lightingData");
        }

        /// <summary>
        /// Get HDLitData from an HDLitSubTarget. Returns null for non-Lit targets.
        /// </summary>
        public static object GetHDLitData(object hdTarget)
        {
            var sub = GetActiveSubTarget(hdTarget);
            if (sub == null || T_HDLitSubTarget == null) return null;
            if (!T_HDLitSubTarget.IsAssignableFrom(sub.GetType())) return null;
            return Prop(sub, "litData");
        }

        /// <summary>
        /// Set the active SubTarget on an HDTarget by type name (e.g. "HDLitSubTarget", "HDUnlitSubTarget").
        /// </summary>
        public static bool TrySetActiveSubTarget(object hdTarget, string subTargetTypeName)
        {
            if (hdTarget == null || T_HDTarget == null) return false;
            var method = CacheMethod(T_HDTarget, "TrySetActiveSubTarget");
            if (method == null) return false;

            // Find the SubTarget type by name in HDRP assembly.
            // Note: Most SubTargets inherit from HDSubTarget, but HDFullscreenSubTarget
            // inherits from FullscreenSubTarget<HDTarget> (in Unity.ShaderGraph.Editor).
            // We match by name only and let TrySetActiveSubTarget validate the type.
            Type subType = null;
            if (HDAssembly != null)
            {
                foreach (var t in HDAssembly.GetTypes())
                {
                    if (t.Name.Equals(subTargetTypeName, StringComparison.OrdinalIgnoreCase))
                    {
                        subType = t;
                        break;
                    }
                }
            }
            if (subType == null) return false;

            var result = method.Invoke(hdTarget, new object[] { subType });
            return result is bool b && b;
        }

        /// <summary>
        /// Get the display name of the active SubTarget (e.g. "Lit", "Unlit").
        /// </summary>
        public static string GetActiveSubTargetDisplayName(object hdTarget)
        {
            var sub = GetActiveSubTarget(hdTarget);
            return sub != null ? (string)Prop(sub, "displayName") : null;
        }
#endif // HDRP_INSTALLED

#if URP_INSTALLED
        // ================================================================
        //  URP Target & Enum Types (lazy, from Unity.RenderPipelines.Universal.Editor)
        // ================================================================

        private static Assembly _urpAsm;
        public static Assembly URPAssembly
        {
            get
            {
                if (_urpAsm == null)
                    _urpAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Unity.RenderPipelines.Universal.Editor");
                return _urpAsm;
            }
        }

        public static Type T_UniversalTarget => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");

        // URP SubTargets
        public static Type T_UniversalLitSubTarget => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget");
        public static Type T_UniversalUnlitSubTarget => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget");
        public static Type T_UniversalSubTarget => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.UniversalSubTarget");

        // URP Enums (in UnityEditor.Rendering.Universal.ShaderGraph namespace)
        public static Type T_URPSurfaceType => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.SurfaceType");
        public static Type T_URPAlphaMode => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.AlphaMode");
        public static Type T_URPRenderFace => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.RenderFace");
        public static Type T_URPZWriteControl => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.ZWriteControl");
        public static Type T_URPZTestMode => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.ZTestMode");
        public static Type T_URPMaterialType => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.MaterialType");
        public static Type T_URPWorkflowMode => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.WorkflowMode");
        public static Type T_URPAdditionalMotionVectorMode => URPAssembly?.GetType("UnityEditor.Rendering.Universal.ShaderGraph.AdditionalMotionVectorMode");

        /// <summary>
        /// Get the first active UniversalTarget from the graph, or null.
        /// </summary>
        public static object GetUniversalTarget(object graph)
        {
            if (graph == null || T_UniversalTarget == null) return null;
            foreach (var t in (IEnumerable)Prop(graph, "activeTargets"))
            {
                if (t != null && T_UniversalTarget.IsAssignableFrom(t.GetType()))
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Get the active SubTarget from a UniversalTarget.
        /// </summary>
        public static object GetUniversalActiveSubTarget(object universalTarget)
        {
            if (universalTarget == null) return null;
            return Prop(universalTarget, "activeSubTarget");
        }

        /// <summary>
        /// Try to set the active SubTarget on a UniversalTarget by type name
        /// (e.g. "UniversalLitSubTarget", "UniversalUnlitSubTarget").
        /// </summary>
        public static bool TrySetUniversalActiveSubTarget(object universalTarget, string subTargetTypeName)
        {
            if (universalTarget == null || T_UniversalTarget == null) return false;
            var method = CacheMethod(T_UniversalTarget, "TrySetActiveSubTarget");
            if (method == null) return false;

            Type subType = null;
            if (URPAssembly != null)
            {
                foreach (var t in URPAssembly.GetTypes())
                {
                    if (t.Name.Equals(subTargetTypeName, StringComparison.OrdinalIgnoreCase) &&
                        T_UniversalSubTarget != null && T_UniversalSubTarget.IsAssignableFrom(t))
                    {
                        subType = t;
                        break;
                    }
                }
            }
            if (subType == null) return false;

            var result = method.Invoke(universalTarget, new object[] { subType });
            return result is bool b && b;
        }
#endif // URP_INSTALLED

        // ================================================================
        //  JToken Type Conversion (Codely.Newtonsoft.Json Value<T>() needs key)
        // ================================================================
        public static float ToFloat(object jtoken)
        {
            if (jtoken == null) return 0f;
            return Convert.ToSingle(jtoken.ToString());
        }

        public static bool ToBool(object jtoken)
        {
            if (jtoken == null) return false;
            return Convert.ToBoolean(jtoken.ToString());
        }

        public static int ToInt(object jtoken)
        {
            if (jtoken == null) return 0;
            return Convert.ToInt32(jtoken.ToString());
        }

        // ================================================================
        //  Property Value Set (for Shader Properties on Blackboard)
        // ================================================================
        public static void SetShaderPropertyValue(object property, Codely.Newtonsoft.Json.Linq.JObject parameters)
        {
            var propType = property.GetType();

            // Vector1
            if (propType == T_Vector1ShaderProperty || propType.IsSubclassOf(T_Vector1ShaderProperty))
            {
                if (parameters["value"] != null)
                    SetProp(property, "value", ToFloat(parameters["value"]));

                var floatTypeStr = parameters["floatType"]?.ToString();
                if (!string.IsNullOrEmpty(floatTypeStr))
                {
                    var ft = ParseEnum(T_FloatType, floatTypeStr);
                    if (ft != null) SetProp(property, "floatType", ft);
                }

                if (EnumToString(Prop(property, "floatType")) == "Slider")
                {
                    var rx = parameters["rangeMin"] != null ? ToFloat(parameters["rangeMin"]) : ((Vector2)Prop(property, "rangeValues")).x;
                    var ry = parameters["rangeMax"] != null ? ToFloat(parameters["rangeMax"]) : ((Vector2)Prop(property, "rangeValues")).y;
                    SetProp(property, "rangeValues", new Vector2(rx, ry));
                }
            }
            // Vector2
            else if (propType == T_Vector2ShaderProperty)
            {
                var cur = (Vector2)Prop(property, "value");
                var x = parameters["x"] != null ? ToFloat(parameters["x"]) : cur.x;
                var y = parameters["y"] != null ? ToFloat(parameters["y"]) : cur.y;
                SetProp(property, "value", new Vector2(x, y));
            }
            // Vector3
            else if (propType == T_Vector3ShaderProperty)
            {
                var cur = (Vector3)Prop(property, "value");
                var x = parameters["x"] != null ? ToFloat(parameters["x"]) : cur.x;
                var y = parameters["y"] != null ? ToFloat(parameters["y"]) : cur.y;
                var z = parameters["z"] != null ? ToFloat(parameters["z"]) : cur.z;
                SetProp(property, "value", new Vector3(x, y, z));
            }
            // Vector4
            else if (propType == T_Vector4ShaderProperty)
            {
                var cur = (Vector4)Prop(property, "value");
                var x = parameters["x"] != null ? ToFloat(parameters["x"]) : cur.x;
                var y = parameters["y"] != null ? ToFloat(parameters["y"]) : cur.y;
                var z = parameters["z"] != null ? ToFloat(parameters["z"]) : cur.z;
                var w = parameters["w"] != null ? ToFloat(parameters["w"]) : cur.w;
                SetProp(property, "value", new Vector4(x, y, z, w));
            }
            // Color
            else if (propType == T_ColorShaderProperty)
            {
                var cur = (Color)Prop(property, "value");
                var r = parameters["r"] != null ? ToFloat(parameters["r"]) : cur.r;
                var g = parameters["g"] != null ? ToFloat(parameters["g"]) : cur.g;
                var b = parameters["b"] != null ? ToFloat(parameters["b"]) : cur.b;
                var a = parameters["a"] != null ? ToFloat(parameters["a"]) : cur.a;
                SetProp(property, "value", new Color(r, g, b, a));
            }
            // Boolean
            else if (propType == T_BooleanShaderProperty)
            {
                if (parameters["value"] != null)
                    SetProp(property, "value", ToBool(parameters["value"]));
            }
            // Texture2D
            else if (propType == T_Texture2DShaderProperty)
            {
                var defaultTypeStr = parameters["defaultType"]?.ToString();
                if (!string.IsNullOrEmpty(defaultTypeStr))
                {
                    var dt = ParseEnum(T_Texture2DDefaultType, defaultTypeStr);
                    if (dt != null) SetProp(property, "defaultType", dt);
                }

                if (parameters["modifiable"] != null)
                    SetProp(property, "modifiable", ToBool(parameters["modifiable"]));
                if (parameters["useTilingAndOffset"] != null)
                    SetProp(property, "useTilingAndOffset", ToBool(parameters["useTilingAndOffset"]));
                if (parameters["isMainTexture"] != null)
                    SetProp(property, "isMainTexture", ToBool(parameters["isMainTexture"]));

                var texPath = parameters["texturePath"]?.ToString();
                if (!string.IsNullOrEmpty(texPath))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    if (tex != null)
                    {
                        var serTex = Activator.CreateInstance(T_SerializableTexture);
                        SetProp(serTex, "texture", tex);
                        SetProp(property, "value", serTex);
                    }
                }
            }
            // Cubemap
            else if (propType == T_CubemapShaderProperty)
            {
                var texPath = parameters["texturePath"]?.ToString();
                if (!string.IsNullOrEmpty(texPath))
                {
                    var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(texPath);
                    if (cube != null)
                    {
                        var serCube = Activator.CreateInstance(T_SerializableCubemap);
                        SetProp(serCube, "cubemap", cube);
                        SetProp(property, "value", serCube);
                    }
                }
            }
        }
    }
}
