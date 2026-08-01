using Codely.Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityTcp.Editor.Tools;

namespace UnityTcp.ShaderTools
{
    /// <summary>
    /// ShaderGraph MCP 工具集（反射版）—— 从 Assembly-CSharp-Editor 通过反射访问 internal API。
    /// 所有 ShaderGraph 内部类型通过 SGReflection 访问，与 Packages 原版零耦合。
    /// 方法必须标记 public static 以便 ExecuteCustomTool 发现。
    /// </summary>
    public static class MCPtoolsShaderGraph
    {
        // ================================================================
        //  shader_graph_get_info
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_get_info", "获取当前打开的 Shader Graph 信息")]
        public static object GetInfo(JObject parameters)
        {
            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };

            var graph = SGReflection.GetGraphFromWindow(window);
            var title = (string)SGReflection.Prop(window, "titleContent")?.GetType().GetProperty("text")?.GetValue(SGReflection.Prop(window, "titleContent"));
            if (title == null) title = (string)SGReflection.Prop(window, "titleContent.text");

            var path = (string)SGReflection.Prop(graph, "assetPath") ?? "";

            var properties = SGReflection.GetPropertiesFromGraph(graph).Select(p => new
            {
                displayName = (string)SGReflection.Prop(p, "displayName"),
                referenceName = (string)SGReflection.Prop(p, "referenceName"),
                type = p.GetType().Name
            }).ToList();

            return new
            {
                success = true,
                graphs = new[]
                {
                    new
                    {
                        title = title ?? "Shader Graph",
                        isFocused = true,
                        path,
                        propertyCount = properties.Count,
                        properties
                    }
                }
            };
        }

        // ================================================================
        //  shader_graph_get_node_slots
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_get_node_slots", "获取节点的所有 Slot 信息")]
        public static object GetNodeSlots(JObject parameters)
        {
            string nodeId = parameters["nodeId"]?.ToString();
            string nodeName = parameters["nodeName"]?.ToString();

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);
            var node = SGReflection.FindNode(graph, nodeId, nodeName);
            if (node == null) return new { success = false, message = "Node not found" };

            var inputSlots = SGReflection.GetInputSlots(node).Select(SGReflection.MakeSlotInfo).ToList();
            var outputSlots = SGReflection.GetOutputSlots(node).Select(SGReflection.MakeSlotInfo).ToList();

            return new
            {
                success = true,
                nodeName = GetNodeName(node),
                nodeId = (string)SGReflection.Prop(node, "objectId"),
                inputSlots,
                outputSlots
            };
        }

        // ================================================================
        //  shader_graph_get_node_details
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_get_node_details", "获取节点的详细信息")]
        public static object GetNodeDetails(JObject parameters)
        {
            string nodeId = parameters["nodeId"]?.ToString();
            string nodeName = parameters["nodeName"]?.ToString();

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);
            var node = SGReflection.FindNode(graph, nodeId, nodeName);
            if (node == null) return new { success = false, message = "Node not found" };

            var nodeType = node.GetType();
            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "nodeId", (string)SGReflection.Prop(node, "objectId") },
                { "name", GetNodeName(node) },
                { "displayName", (string)SGReflection.Prop(node, "displayName") },
                { "nodeType", nodeType.Name }
            };

            var inputSlots = SGReflection.GetInputSlots(node).Select(SGReflection.MakeSlotInfo).ToList();
            var outputSlots = SGReflection.GetOutputSlots(node).Select(SGReflection.MakeSlotInfo).ToList();
            result["inputSlots"] = inputSlots;
            result["outputSlots"] = outputSlots;

            // Type-specific properties
            object properties = null;

            if (nodeType == SGReflection.T_SamplerStateNode || nodeType.IsSubclassOf(SGReflection.T_SamplerStateNode))
            {
                properties = new
                {
                    filter = SGReflection.EnumToString(SGReflection.Prop(node, "filter")),
                    wrap = SGReflection.EnumToString(SGReflection.Prop(node, "wrap")),
                    anisotropic = SGReflection.EnumToString(SGReflection.Prop(node, "anisotropic"))
                };
            }
            else if (nodeType == SGReflection.T_SampleTexture2DNode || nodeType.IsSubclassOf(SGReflection.T_SampleTexture2DNode))
            {
                properties = new
                {
                    textureType = SGReflection.EnumToString(SGReflection.Prop(node, "textureType")),
                    normalMapSpace = SGReflection.EnumToString(SGReflection.Prop(node, "normalMapSpace"))
                };
            }
            else if (nodeType == SGReflection.T_PropertyNode || nodeType.IsSubclassOf(SGReflection.T_PropertyNode))
            {
                var prop = SGReflection.Prop(node, "property");
                if (prop != null)
                {
                    properties = new
                    {
                        propertyDisplayName = (string)SGReflection.Prop(prop, "displayName"),
                        propertyReferenceName = (string)SGReflection.Prop(prop, "referenceName"),
                        propertyType = prop.GetType().Name
                    };
                }
            }
            else if (nodeType == SGReflection.T_ColorNode || nodeType.IsSubclassOf(SGReflection.T_ColorNode))
            {
                var colorObj = SGReflection.Prop(node, "color");
                var color = (Color)SGReflection.Field(colorObj, "color");
                var mode = SGReflection.EnumToString(SGReflection.Field(colorObj, "mode"));
                properties = new { r = color.r, g = color.g, b = color.b, a = color.a, colorMode = mode };
            }
            else if (nodeType == SGReflection.T_Vector1Node || nodeType.IsSubclassOf(SGReflection.T_Vector1Node))
            {
                var slot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector1MaterialSlot, Vector1NodeInputSlotXId());
                properties = new { x = slot != null ? (float)SGReflection.Prop(slot, "value") : 0f };
            }
            else if (nodeType == SGReflection.T_Vector2Node || nodeType.IsSubclassOf(SGReflection.T_Vector2Node))
            {
                var xSlot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector2MaterialSlot, Vector2NodeInputSlotXId());
                var ySlot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector2MaterialSlot, Vector2NodeInputSlotYId());
                properties = new
                {
                    x = xSlot != null ? (float)SGReflection.Prop(xSlot, "value") : 0f,
                    y = ySlot != null ? (float)SGReflection.Prop(ySlot, "value") : 0f
                };
            }
            else if (nodeType == SGReflection.T_Vector3Node || nodeType.IsSubclassOf(SGReflection.T_Vector3Node))
            {
                var xSlot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector3MaterialSlot, Vector3NodeInputSlotXId());
                var ySlot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector3MaterialSlot, Vector3NodeInputSlotYId());
                var zSlot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector3MaterialSlot, Vector3NodeInputSlotZId());
                properties = new
                {
                    x = xSlot != null ? (float)SGReflection.Prop(xSlot, "value") : 0f,
                    y = ySlot != null ? (float)SGReflection.Prop(ySlot, "value") : 0f,
                    z = zSlot != null ? (float)SGReflection.Prop(zSlot, "value") : 0f
                };
            }
            else if (nodeType == SGReflection.T_Vector4Node || nodeType.IsSubclassOf(SGReflection.T_Vector4Node))
            {
                var xSlot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector4MaterialSlot, Vector4NodeInputSlotXId());
                var ySlot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector4MaterialSlot, Vector4NodeInputSlotYId());
                var zSlot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector4MaterialSlot, Vector4NodeInputSlotZId());
                var wSlot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector4MaterialSlot, Vector4NodeInputSlotWId());
                properties = new
                {
                    x = xSlot != null ? (float)SGReflection.Prop(xSlot, "value") : 0f,
                    y = ySlot != null ? (float)SGReflection.Prop(ySlot, "value") : 0f,
                    z = zSlot != null ? (float)SGReflection.Prop(zSlot, "value") : 0f,
                    w = wSlot != null ? (float)SGReflection.Prop(wSlot, "value") : 0f
                };
            }
            else if (nodeType == SGReflection.T_CustomFunctionNode || nodeType.IsSubclassOf(SGReflection.T_CustomFunctionNode))
            {
                var hf = SGReflection.Prop(node, "hlslFunctionName")?.ToString();
                var st = SGReflection.Prop(node, "hlslSourceType")?.ToString();
                var fn = SGReflection.Prop(node, "functionName")?.ToString();
                var hn = SGReflection.Prop(node, "hlslName")?.ToString();
                properties = new
                {
                    hlslFunctionName = hf,
                    hlslSourceType = st,
                    functionName = fn ?? hf,
                    hlslName = hn
                };
            }

            if (properties != null) result["properties"] = properties;
            return result;
        }

        // ================================================================
        //  shader_graph_get_nodes
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_get_nodes", "获取图中所有节点")]
        public static object GetNodes(JObject parameters)
        {
            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            var nodes = SGReflection.GetNodesFromGraph(graph).Select(n => new
            {
                id = (string)SGReflection.Prop(n, "objectId"),
                name = GetNodeName(n),
                displayName = (string)SGReflection.Prop(n, "displayName")
            }).ToList();

            return new { success = true, nodeCount = nodes.Count, nodes };
        }

        // ================================================================
        //  shader_graph_save
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_save", "保存当前 Shader Graph")]
        public static object Save(JObject parameters)
        {
            string savePath = parameters["savePath"]?.ToString();
            bool forceSave = parameters["forceSave"]?.Value<bool>() ?? false;

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };

            var graph = SGReflection.GetGraphFromWindow(window);
            var currentPath = (string)SGReflection.Prop(graph, "assetPath") ?? "";
            var targetPath = !string.IsNullOrEmpty(savePath) ? savePath : currentPath;

            try
            {
                // Use the window's SaveAsset method for proper serialization
                var saveMethod = window.GetType().GetMethod("SaveAsset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveMethod == null)
                    return new { success = false, message = "SaveAsset method not found on window" };
                saveMethod.Invoke(window, new object[0]);
                var hasErrors = SGReflection.GraphHasErrors(graph);
                return new { success = true, message = "Shader Graph saved", path = currentPath, hasErrors };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Save failed: {e.Message}" };
            }
        }

        // ================================================================
        //  shader_graph_get_errors
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_get_errors", "获取 Shader Graph 的编译错误和消息")]
        public static object GetErrors(JObject parameters)
        {
            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            var hasErrors = SGReflection.GraphHasErrors(graph);
            var messages = new List<object>();

            foreach (var (nodeId, msg) in SGReflection.GetNodeMessages(graph))
            {
                var severity = SGReflection.EnumToString(SGReflection.Prop(msg, "severity"));
                var messageText = (string)SGReflection.Prop(msg, "message");
                messages.Add(new { nodeId, severity, message = messageText });
            }

            return new
            {
                success = true,
                hasErrors,
                hasWarnings = messages.Any(m => ((dynamic)m).severity?.ToString() == "Warning"),
                messageCount = messages.Count,
                messages
            };
        }

        // ================================================================
        //  shader_graph_get_available_node_types
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_get_available_node_types", "获取所有可用的 ShaderGraph 节点类型")]
        public static object GetAvailableNodeTypes(JObject parameters)
        {
            string filter = parameters["filter"]?.ToString();
            int maxResults = parameters["maxResults"]?.Value<int>() ?? 200;

            var types = SGReflection.GetKnownNodeTypes()
                .Where(t => !t.IsAbstract && !t.ContainsGenericParameters)
                .Select(t =>
                {
                    var (displayName, category) = SGReflection.GetNodeTitleInfo(t);
                    return new { typeName = t.Name, fullName = t.FullName, displayName, category };
                });

            if (!string.IsNullOrEmpty(filter))
                types = types.Where(t => t.typeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || t.displayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            var result = types.Take(maxResults).ToList();
            return new { success = true, totalCount = SGReflection.GetKnownNodeTypes().Count(), filteredCount = result.Count, filter, nodeTypes = result };
        }

        // ================================================================
        //  shader_graph_create_asset
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_create_asset", "创建新的 ShaderGraph 文件")]
        public static object CreateAsset(JObject parameters)
        {
            string path = parameters["path"]?.ToString();
            string targetType = parameters["targetType"]?.ToString() ?? "HDRP_Lit";
            bool openAfterCreate = parameters["openAfterCreate"]?.Value<bool>() ?? true;

            if (string.IsNullOrEmpty(path))
                return new { success = false, message = "Missing path parameter" };

            return SGReflection.CreateShaderGraphAsset(path, targetType, openAfterCreate);
        }

        // ================================================================
        //  shader_graph_create_by_name
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_create_by_name", "通过节点名称创建节点")]
        public static object CreateByName(JObject parameters)
        {
            string nodeTypeName = parameters["nodeTypeName"]?.ToString();
            float x = parameters["x"]?.Value<float>() ?? 200;
            float y = parameters["y"]?.Value<float>() ?? 200;

            if (string.IsNullOrEmpty(nodeTypeName))
                return new { success = false, message = "Missing nodeTypeName parameter" };

            var nodeType = SGReflection.FindNodeTypeByName(nodeTypeName);
            if (nodeType == null)
                return new { success = false, message = $"Unknown node type: {nodeTypeName}" };

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            var node = SGReflection.CreateNode(nodeType, x, y);
            SGReflection.AddNodeToGraph(graph, node, $"Add {nodeTypeName}");
            SGReflection.RefreshGraphUI();

            return new
            {
                success = true,
                message = $"Created node: {nodeTypeName}",
                nodeId = (string)SGReflection.Prop(node, "objectId"),
                nodeName = GetNodeName(node)
            };
        }

        // ================================================================
        //  shader_graph_create_custom_function
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_create_custom_function", "创建 Custom Function 节点并设置 HLSL 代码")]
        public static object CreateCustomFunction(JObject parameters)
        {
            string functionName = parameters["functionName"]?.ToString();
            string hlslCode = parameters["hlslCode"]?.ToString();
            string hlslFileName = parameters["hlslFileName"]?.ToString();
            string sourceType = parameters["sourceType"]?.ToString() ?? "String";
            float x = parameters["x"]?.Value<float>() ?? 200;
            float y = parameters["y"]?.Value<float>() ?? 200;

            if (string.IsNullOrEmpty(functionName))
                return new { success = false, message = "Missing functionName parameter" };

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            var node = Activator.CreateInstance(SGReflection.T_CustomFunctionNode);
            SGReflection.SetNodePosition(node, x, y);

            // Set source type (must be set first, before functionBody/functionSource)
            var parsedSourceType = SGReflection.ParseEnum(SGReflection.T_HlslSourceType, sourceType);
            if (parsedSourceType != null) SGReflection.SetProp(node, "sourceType", parsedSourceType);

            // Set function name (hlslFunctionName is read-only, derived from functionName)
            SGReflection.SetProp(node, "functionName", functionName);

            if (parsedSourceType?.ToString() == "String" && !string.IsNullOrEmpty(hlslCode))
            {
                SGReflection.SetProp(node, "functionBody", hlslCode);
            }
            else if (!string.IsNullOrEmpty(hlslFileName))
            {
                // functionSource must be a GUID string, not a file path
                var guid = UnityEditor.AssetDatabase.AssetPathToGUID(hlslFileName);
                SGReflection.SetProp(node, "functionSource", !string.IsNullOrEmpty(guid) ? guid : hlslFileName);
            }

            SGReflection.AddNodeToGraph(graph, node, $"Add Custom Function {functionName}");
            SGReflection.ValidateNode(node);
            SGReflection.DirtyNode(node);
            SGReflection.RefreshGraphUI();

            return new
            {
                success = true,
                message = $"Created Custom Function node: {functionName}",
                nodeId = (string)SGReflection.Prop(node, "objectId"),
                nodeName = GetNodeName(node),
                sourceType = parsedSourceType?.ToString() ?? "String"
            };
        }

        // ================================================================
        //  shader_graph_create_property
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_create_property", "通过属性类型名称创建ShaderGraph属性（左侧Blackboard属性）")]
        public static object CreatePropertyByName(JObject parameters)
        {
            string propertyTypeName = parameters["propertyType"]?.ToString();
            string displayName = parameters["displayName"]?.ToString();
            string referenceName = parameters["referenceName"]?.ToString();

            if (string.IsNullOrEmpty(propertyTypeName))
                return new { success = false, message = "Missing propertyType parameter" };
            if (string.IsNullOrEmpty(displayName))
                displayName = propertyTypeName.Replace("ShaderProperty", "");

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            // Find property type via reflection on ShaderGraph assembly
            var propertyType = SGReflection.FindShaderPropertyType(propertyTypeName);

            if (propertyType == null)
                return new { success = false, message = $"Unknown property type: {propertyTypeName}" };

            // Create instance (nonPublic: true for internal constructors)
            var property = Activator.CreateInstance(propertyType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[0], null);
            if (property == null)
                return new { success = false, message = $"Failed to create property instance: {propertyTypeName}" };

            SGReflection.SetProp(property, "displayName", displayName);
            if (!string.IsNullOrEmpty(referenceName))
                SGReflection.SetProp(property, "overrideReferenceName", referenceName);

            SGReflection.SetShaderPropertyValue(property, parameters);

            SGReflection.SetProp(property, "generatePropertyBlock", (bool)SGReflection.Prop(property, "isExposable"));
            SGReflection.AddGraphInput(graph, property, $"Add Property {displayName}");
            SGReflection.AddPropertyToDefaultCategory(graph, property);
            SGReflection.RefreshGraphUI();

            return new { success = true, message = $"Created property: {displayName} ({propertyTypeName})", referenceName = (string)SGReflection.Prop(property, "referenceName") };
        }

        // ================================================================
        //  shader_graph_create_property_node
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_create_property_node", "创建属性节点，引用Blackboard中的属性")]
        public static object CreatePropertyNode(JObject parameters)
        {
            string propertyReferenceName = parameters["propertyReferenceName"]?.ToString();
            float x = parameters["x"]?.Value<float>() ?? 200;
            float y = parameters["y"]?.Value<float>() ?? 200;

            if (string.IsNullOrEmpty(propertyReferenceName))
                return new { success = false, message = "Missing propertyReferenceName parameter" };

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            var property = SGReflection.FindPropertyByRefName(graph, propertyReferenceName);
            if (property == null)
                return new { success = false, message = $"Property not found: {propertyReferenceName}" };

            var node = Activator.CreateInstance(SGReflection.T_PropertyNode);
            SGReflection.SetNodePosition(node, x, y);
            SGReflection.SetProp(node, "property", property);

            SGReflection.AddNodeToGraph(graph, node, "Add Property Node");
            SGReflection.RefreshGraphUI();

            return new
            {
                success = true,
                message = $"Created PropertyNode for: {(string)SGReflection.Prop(property, "displayName")}",
                nodeId = (string)SGReflection.Prop(node, "objectId"),
                propertyName = (string)SGReflection.Prop(property, "displayName"),
                propertyReferenceName
            };
        }

        // ================================================================
        //  shader_graph_modify_property
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_modify_property", "修改 ShaderGraph 属性的参数")]
        public static object ModifyProperty(JObject parameters)
        {
            string referenceName = parameters["referenceName"]?.ToString();
            string displayName = parameters["displayName"]?.ToString();

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            var property = SGReflection.FindProperty(graph, referenceName, displayName);
            if (property == null)
                return new { success = false, message = $"Property not found: {referenceName ?? displayName}" };

            var owner = SGReflection.Prop(graph, "owner");
            SGReflection.CacheMethod(owner.GetType(), "RegisterCompleteObjectUndo")?.Invoke(owner, new object[] { "Modify Property" });

            if (!string.IsNullOrEmpty(displayName))
                SGReflection.SetProp(property, "displayName", displayName);
            if (!string.IsNullOrEmpty(referenceName))
                SGReflection.SetProp(property, "overrideReferenceName", referenceName);

            SGReflection.SetShaderPropertyValue(property, parameters);

            // Refresh dependent nodes
            SGReflection.DirtyGraphNodes(graph, property);
            SGReflection.RefreshGraphUI();

            return new
            {
                success = true,
                message = $"Modified property: {(string)SGReflection.Prop(property, "displayName")}",
                referenceName = (string)SGReflection.Prop(property, "referenceName")
            };
        }

        // ================================================================
        //  shader_graph_modify_node
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_modify_node", "修改节点的属性")]
        public static object ModifyNode(JObject parameters)
        {
            string nodeId = parameters["nodeId"]?.ToString();
            string nodeName = parameters["nodeName"]?.ToString();

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);
            var node = SGReflection.FindNode(graph, nodeId, nodeName);
            if (node == null) return new { success = false, message = "Node not found" };

            bool needsRefresh = false;
            var nodeType = node.GetType();

            // Position
            float? posX = parameters["x"]?.Value<float>();
            float? posY = parameters["y"]?.Value<float>();
            if (posX.HasValue || posY.HasValue)
            {
                var drawState = SGReflection.Prop(node, "drawState");
                var position = (Rect)SGReflection.Prop(drawState, "position");
                var newX = posX ?? position.x;
                var newY = posY ?? position.y;
                SGReflection.SetNodePosition(node, newX, newY);
            }

            // DisplayName
            string newDisplayName = parameters["displayName"]?.ToString();
            if (!string.IsNullOrEmpty(newDisplayName))
            {
                SGReflection.SetProp(node, "displayName", newDisplayName);
            }

            // SamplerStateNode
            if (nodeType == SGReflection.T_SamplerStateNode || nodeType.IsSubclassOf(SGReflection.T_SamplerStateNode))
            {
                string filterStr = parameters["filter"]?.ToString();
                string wrapStr = parameters["wrap"]?.ToString();
                string anisotropicStr = parameters["anisotropic"]?.ToString();
                if (!string.IsNullOrEmpty(filterStr)) { SGReflection.SetProp(node, "filter", SGReflection.ParseEnum(SGReflection.T_FilterMode, filterStr)); needsRefresh = true; }
                if (!string.IsNullOrEmpty(wrapStr)) { SGReflection.SetProp(node, "wrap", SGReflection.ParseEnum(SGReflection.T_WrapMode, wrapStr)); needsRefresh = true; }
                if (!string.IsNullOrEmpty(anisotropicStr)) { SGReflection.SetProp(node, "anisotropic", SGReflection.ParseEnum(SGReflection.T_Anisotropic, anisotropicStr)); needsRefresh = true; }
            }
            // SampleTexture2DNode
            else if (nodeType == SGReflection.T_SampleTexture2DNode || nodeType.IsSubclassOf(SGReflection.T_SampleTexture2DNode))
            {
                string textureTypeStr = parameters["textureType"]?.ToString();
                string normalMapSpaceStr = parameters["normalMapSpace"]?.ToString();
                if (!string.IsNullOrEmpty(textureTypeStr)) { SGReflection.SetProp(node, "textureType", SGReflection.ParseEnum(SGReflection.T_TextureType, textureTypeStr)); needsRefresh = true; }
                if (!string.IsNullOrEmpty(normalMapSpaceStr)) { SGReflection.SetProp(node, "normalMapSpace", SGReflection.ParseEnum(SGReflection.T_NormalMapSpace, normalMapSpaceStr)); needsRefresh = true; }
            }
            // PropertyNode
            else if (nodeType == SGReflection.T_PropertyNode || nodeType.IsSubclassOf(SGReflection.T_PropertyNode))
            {
                string propRefName = parameters["propertyReferenceName"]?.ToString();
                if (!string.IsNullOrEmpty(propRefName))
                {
                    var prop = SGReflection.FindPropertyByRefName(graph, propRefName);
                    if (prop != null) { SGReflection.SetProp(node, "property", prop); needsRefresh = true; }
                }
            }
            // ColorNode
            else if (nodeType == SGReflection.T_ColorNode || nodeType.IsSubclassOf(SGReflection.T_ColorNode))
            {
                float? r = parameters["r"]?.Value<float>();
                float? g = parameters["g"]?.Value<float>();
                float? b = parameters["b"]?.Value<float>();
                float? a = parameters["a"]?.Value<float>();
                string colorModeStr = parameters["colorMode"]?.ToString();

                if (r.HasValue || g.HasValue || b.HasValue || a.HasValue)
                {
                    var colorObj = SGReflection.Prop(node, "color");
                    var currentColor = (Color)SGReflection.Field(colorObj, "color");
                    var newColor = new Color(r ?? currentColor.r, g ?? currentColor.g, b ?? currentColor.b, a ?? currentColor.a);

                    var newMode = SGReflection.Field(colorObj, "mode");
                    if (!string.IsNullOrEmpty(colorModeStr))
                    {
                        var parsed = SGReflection.ParseEnum(SGReflection.T_ColorMode, colorModeStr);
                        if (parsed != null) newMode = parsed;
                    }

                    // Create new ColorNode.Color struct
                    var newColorObj = Activator.CreateInstance(SGReflection.T_ColorNodeColor, newColor, newMode);
                    SGReflection.SetProp(node, "color", newColorObj);
                    needsRefresh = true;
                }
            }
            // Vector1Node
            else if (nodeType == SGReflection.T_Vector1Node || nodeType.IsSubclassOf(SGReflection.T_Vector1Node))
            {
                float? xVal = parameters["x"]?.Value<float>();
                if (xVal.HasValue)
                {
                    var slot = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector1MaterialSlot, Vector1NodeInputSlotXId());
                    if (slot != null) { SGReflection.SetSlotValue(slot, xVal.Value); needsRefresh = true; }
                }
            }
            // Vector2Node
            else if (nodeType == SGReflection.T_Vector2Node || nodeType.IsSubclassOf(SGReflection.T_Vector2Node))
            {
                float? xVal = parameters["x"]?.Value<float>();
                float? yVal = parameters["y"]?.Value<float>();
                if (xVal.HasValue) { var s = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector2MaterialSlot, Vector2NodeInputSlotXId()); if (s != null) SGReflection.SetSlotValue(s, xVal.Value); }
                if (yVal.HasValue) { var s = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector2MaterialSlot, Vector2NodeInputSlotYId()); if (s != null) SGReflection.SetSlotValue(s, yVal.Value); }
                needsRefresh = xVal.HasValue || yVal.HasValue;
            }
            // Vector3Node
            else if (nodeType == SGReflection.T_Vector3Node || nodeType.IsSubclassOf(SGReflection.T_Vector3Node))
            {
                float? xVal = parameters["x"]?.Value<float>();
                float? yVal = parameters["y"]?.Value<float>();
                float? zVal = parameters["z"]?.Value<float>();
                if (xVal.HasValue) { var s = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector3MaterialSlot, Vector3NodeInputSlotXId()); if (s != null) SGReflection.SetSlotValue(s, xVal.Value); }
                if (yVal.HasValue) { var s = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector3MaterialSlot, Vector3NodeInputSlotYId()); if (s != null) SGReflection.SetSlotValue(s, yVal.Value); }
                if (zVal.HasValue) { var s = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector3MaterialSlot, Vector3NodeInputSlotZId()); if (s != null) SGReflection.SetSlotValue(s, zVal.Value); }
                needsRefresh = xVal.HasValue || yVal.HasValue || zVal.HasValue;
            }
            // Vector4Node
            else if (nodeType == SGReflection.T_Vector4Node || nodeType.IsSubclassOf(SGReflection.T_Vector4Node))
            {
                float? xVal = parameters["x"]?.Value<float>();
                float? yVal = parameters["y"]?.Value<float>();
                float? zVal = parameters["z"]?.Value<float>();
                float? wVal = parameters["w"]?.Value<float>();
                if (xVal.HasValue) { var s = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector4MaterialSlot, Vector4NodeInputSlotXId()); if (s != null) SGReflection.SetSlotValue(s, xVal.Value); }
                if (yVal.HasValue) { var s = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector4MaterialSlot, Vector4NodeInputSlotYId()); if (s != null) SGReflection.SetSlotValue(s, yVal.Value); }
                if (zVal.HasValue) { var s = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector4MaterialSlot, Vector4NodeInputSlotZId()); if (s != null) SGReflection.SetSlotValue(s, zVal.Value); }
                if (wVal.HasValue) { var s = SGReflection.FindInputSlotOnNode(node, SGReflection.T_Vector4MaterialSlot, Vector4NodeInputSlotWId()); if (s != null) SGReflection.SetSlotValue(s, wVal.Value); }
                needsRefresh = xVal.HasValue || yVal.HasValue || zVal.HasValue || wVal.HasValue;
            }
            // CustomFunctionNode
            else if (nodeType == SGReflection.T_CustomFunctionNode || nodeType.IsSubclassOf(SGReflection.T_CustomFunctionNode))
            {
                string fn = parameters["functionName"]?.ToString();
                string hlslCode = parameters["hlslCode"]?.ToString();
                string hlslFileName = parameters["hlslFileName"]?.ToString();
                string srcType = parameters["sourceType"]?.ToString();
                if (!string.IsNullOrEmpty(fn)) { SGReflection.SetProp(node, "functionName", fn); needsRefresh = true; }
                if (!string.IsNullOrEmpty(srcType)) { var st = SGReflection.ParseEnum(SGReflection.T_HlslSourceType, srcType); if (st != null) SGReflection.SetProp(node, "sourceType", st); needsRefresh = true; }
                if (!string.IsNullOrEmpty(hlslCode)) { SGReflection.SetProp(node, "functionBody", hlslCode); needsRefresh = true; }
                if (!string.IsNullOrEmpty(hlslFileName)) {
                    var guid = UnityEditor.AssetDatabase.AssetPathToGUID(hlslFileName);
                    SGReflection.SetProp(node, "functionSource", !string.IsNullOrEmpty(guid) ? guid : hlslFileName);
                    needsRefresh = true;
                }
            }

            if (needsRefresh)
            {
                SGReflection.ValidateNode(node);
                SGReflection.DirtyNode(node);
                SGReflection.RefreshGraphUI();
            }

            return new { success = true, message = $"Modified node: {GetNodeName(node)}", nodeId = (string)SGReflection.Prop(node, "objectId") };
        }

        // ================================================================
        //  shader_graph_set_slot_value
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_set_slot_value", "设置节点 Slot 的值")]
        public static object SetSlotValue(JObject parameters)
        {
            string nodeId = parameters["nodeId"]?.ToString();
            int? slotId = parameters["slotId"]?.Value<int>();
            object value = parameters["value"];

            if (!slotId.HasValue)
                return new { success = false, message = "Missing slotId parameter" };

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);
            var node = SGReflection.FindNode(graph, nodeId, null);
            if (node == null) return new { success = false, message = "Node not found" };

            var slot = SGReflection.FindSlotOnNode(node, slotId.Value);
            if (slot == null)
                return new { success = false, message = $"Slot {slotId} not found on node" };

            var slotType = slot.GetType();
            try
            {
                if (slotType == SGReflection.T_Vector1MaterialSlot || slotType.IsSubclassOf(SGReflection.T_Vector1MaterialSlot))
                {
                    float? v = parameters["value"]?.Value<float>();
                    if (v.HasValue) { SGReflection.SetSlotValue(slot, v.Value); SGReflection.DirtyNode(node); SGReflection.RefreshGraphUI(); return new { success = true, message = "Set slot value" }; }
                }
                else if (slotType == SGReflection.T_Vector2MaterialSlot || slotType.IsSubclassOf(SGReflection.T_Vector2MaterialSlot))
                {
                    float? sx = parameters["x"]?.Value<float>();
                    float? sy = parameters["y"]?.Value<float>();
                    if (sx.HasValue || sy.HasValue)
                    {
                        var cur = (Vector2)SGReflection.GetSlotValue(slot);
                        SGReflection.SetSlotValue(slot, new Vector2(sx ?? cur.x, sy ?? cur.y));
                        SGReflection.DirtyNode(node);
                        SGReflection.RefreshGraphUI();
                        return new { success = true, message = "Set slot value" };
                    }
                }
                else if (slotType == SGReflection.T_Vector3MaterialSlot || slotType.IsSubclassOf(SGReflection.T_Vector3MaterialSlot))
                {
                    float? sx = parameters["x"]?.Value<float>();
                    float? sy = parameters["y"]?.Value<float>();
                    float? sz = parameters["z"]?.Value<float>();
                    if (sx.HasValue || sy.HasValue || sz.HasValue)
                    {
                        var cur = (Vector3)SGReflection.GetSlotValue(slot);
                        SGReflection.SetSlotValue(slot, new Vector3(sx ?? cur.x, sy ?? cur.y, sz ?? cur.z));
                        SGReflection.DirtyNode(node);
                        SGReflection.RefreshGraphUI();
                        return new { success = true, message = "Set slot value" };
                    }
                }
                else if (slotType == SGReflection.T_Vector4MaterialSlot || slotType.IsSubclassOf(SGReflection.T_Vector4MaterialSlot))
                {
                    float? sx = parameters["x"]?.Value<float>();
                    float? sy = parameters["y"]?.Value<float>();
                    float? sz = parameters["z"]?.Value<float>();
                    float? sw = parameters["w"]?.Value<float>();
                    if (sx.HasValue || sy.HasValue || sz.HasValue || sw.HasValue)
                    {
                        var cur = (Vector4)SGReflection.GetSlotValue(slot);
                        SGReflection.SetSlotValue(slot, new Vector4(sx ?? cur.x, sy ?? cur.y, sz ?? cur.z, sw ?? cur.w));
                        SGReflection.DirtyNode(node);
                        SGReflection.RefreshGraphUI();
                        return new { success = true, message = "Set slot value" };
                    }
                }
                else if (slotType == SGReflection.T_BooleanMaterialSlot || slotType.IsSubclassOf(SGReflection.T_BooleanMaterialSlot))
                {
                    bool? v = parameters["value"]?.Value<bool>();
                    if (v.HasValue) { SGReflection.SetSlotValue(slot, v.Value); SGReflection.DirtyNode(node); SGReflection.RefreshGraphUI(); return new { success = true, message = "Set slot value" }; }
                }

                return new { success = false, message = $"Unsupported slot type: {slotType.Name} or missing value parameter" };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error setting slot value: {e.Message}" };
            }
        }

        // ================================================================
        //  shader_graph_create_hlsl_file
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_create_hlsl_file", "创建HLSL Include文件用于Custom Function节点")]
        public static object CreateHlslFile(JObject parameters)
        {
            string filePath = parameters["filePath"]?.ToString();
            string content = parameters["content"]?.ToString();
            bool overwrite = parameters["overwrite"]?.Value<bool>() ?? false;

            if (string.IsNullOrEmpty(filePath)) return new { success = false, message = "Missing filePath parameter" };
            if (string.IsNullOrEmpty(content)) return new { success = false, message = "Missing content parameter" };

            if (!filePath.StartsWith("Assets/")) filePath = "Assets/" + filePath;
            if (!filePath.EndsWith(".hlsl")) filePath += ".hlsl";

            if (File.Exists(filePath) && !overwrite)
                return new { success = false, message = $"File already exists: {filePath}. Set overwrite=true to replace it." };

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(filePath, content);
                AssetDatabase.Refresh();
                string guid = AssetDatabase.AssetPathToGUID(filePath);

                // Check if it was imported as Shader Include
                var importer = AssetImporter.GetAtPath(filePath);
                bool isShaderInclude = importer != null && importer.GetType().Name.Contains("ShaderInclude");

                return new { success = true, message = $"Created HLSL file: {filePath}", path = filePath, guid, isShaderInclude };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Failed to create file: {e.Message}" };
            }
        }

        // ================================================================
        //  shader_graph_connect_nodes
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_connect_nodes", "连接两个节点的 Slot")]
        public static object ConnectNodes(JObject parameters)
        {
            string fromNodeId = parameters["fromNodeId"]?.ToString();
            int fromSlotId = parameters["fromSlotId"]?.Value<int>() ?? 0;
            string toNodeId = parameters["toNodeId"]?.ToString();
            int toSlotId = parameters["toSlotId"]?.Value<int>() ?? 0;

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            var fromNode = SGReflection.FindNodeById(graph, fromNodeId);
            var toNode = SGReflection.FindNodeById(graph, toNodeId);
            if (fromNode == null) return new { success = false, message = $"From node not found: {fromNodeId}" };
            if (toNode == null) return new { success = false, message = $"To node not found: {toNodeId}" };

            // Validate slots exist
            var fromSlot = SGReflection.FindSlotOnNode(fromNode, fromSlotId);
            var toSlot = SGReflection.FindSlotOnNode(toNode, toSlotId);
            if (fromSlot == null) return new { success = false, message = $"Source slot {fromSlotId} not found on node {GetNodeName(fromNode)}" };
            if (toSlot == null) return new { success = false, message = $"Target slot {toSlotId} not found on node {GetNodeName(toNode)}" };

            var edge = SGReflection.ConnectSlots(graph, fromNode, fromSlotId, toNode, toSlotId);
            if (edge == null)
                return new { success = false, message = "Connection failed" };

            SGReflection.RefreshGraphUI();

            return new
            {
                success = true,
                message = $"Connected: {GetNodeName(fromNode)}.{SGReflection.GetSlotDisplayName(fromSlot)} -> {GetNodeName(toNode)}.{SGReflection.GetSlotDisplayName(toSlot)}"
            };
        }

        // ================================================================
        //  shader_graph_disconnect_nodes
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_disconnect_nodes", "断开节点之间的连接")]
        public static object DisconnectNodes(JObject parameters)
        {
            string fromNodeId = parameters["fromNodeId"]?.ToString();
            int? fromSlotId = parameters["fromSlotId"]?.Value<int>();
            string toNodeId = parameters["toNodeId"]?.ToString();
            int? toSlotId = parameters["toSlotId"]?.Value<int>();

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            int disconnectedCount = 0;
            var edgesToRemove = new List<object>();

            foreach (var edge in SGReflection.GetEdges(graph))
            {
                var outputNode = SGReflection.GetEdgeOutputNode(edge);
                var inputNode = SGReflection.GetEdgeInputNode(edge);
                var outputSlotId = SGReflection.GetEdgeOutputSlotId(edge);
                var inputSlotId = SGReflection.GetEdgeInputSlotId(edge);

                var outputNodeId = (string)SGReflection.Prop(outputNode, "objectId");
                var inputNodeId = (string)SGReflection.Prop(inputNode, "objectId");

                bool match = true;
                if (!string.IsNullOrEmpty(fromNodeId) && outputNodeId != fromNodeId) match = false;
                if (fromSlotId.HasValue && outputSlotId != fromSlotId.Value) match = false;
                if (!string.IsNullOrEmpty(toNodeId) && inputNodeId != toNodeId) match = false;
                if (toSlotId.HasValue && inputSlotId != toSlotId.Value) match = false;

                if (match) edgesToRemove.Add(edge);
            }

            if (edgesToRemove.Count == 0)
                return new { success = false, message = "No matching connections found" };

            var owner = SGReflection.Prop(graph, "owner");
            SGReflection.CacheMethod(owner.GetType(), "RegisterCompleteObjectUndo")?.Invoke(owner, new object[] { "Disconnect Nodes" });

            foreach (var edge in edgesToRemove)
            {
                SGReflection.RemoveEdge(graph, edge);
                disconnectedCount++;
            }

            SGReflection.RefreshGraphUI();

            return new { success = true, message = $"Disconnected {disconnectedCount} edges", disconnectedCount };
        }

        // ================================================================
        //  shader_graph_delete_node
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_delete_node", "删除指定的节点")]
        public static object DeleteNode(JObject parameters)
        {
            string nodeId = parameters["nodeId"]?.ToString();

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);
            var node = SGReflection.FindNodeById(graph, nodeId);
            if (node == null) return new { success = false, message = $"Node not found: {nodeId}" };

            var name = GetNodeName(node);
            SGReflection.RemoveNodeFromGraph(graph, node, $"Delete Node {name}");
            SGReflection.RefreshGraphUI();

            return new { success = true, message = $"Deleted node: {name}", nodeId };
        }

        // ================================================================
        //  shader_graph_delete_nodes
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_delete_nodes", "批量删除节点")]
        public static object DeleteNodes(JObject parameters)
        {
            var nodeIds = parameters["nodeIds"]?.Select(t => t.ToString()).ToList();

            if (nodeIds == null || nodeIds.Count == 0)
                return new { success = false, message = "Missing nodeIds parameter" };

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            int deletedCount = 0;
            foreach (var nid in nodeIds)
            {
                var node = SGReflection.FindNodeById(graph, nid);
                if (node != null)
                {
                    SGReflection.RemoveNodeFromGraph(graph, node, "Delete Node");
                    deletedCount++;
                }
            }

            SGReflection.RefreshGraphUI();

            return new { success = true, message = $"Deleted {deletedCount} nodes", deletedCount };
        }

        // ================================================================
        //  shader_graph_duplicate_node
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_duplicate_node", "复制节点（使用 MultiJson 序列化/反序列化，完整保留所有属性和 Slots）")]
        public static object DuplicateNode(JObject parameters)
        {
            string nodeId = parameters["nodeId"]?.ToString();
            string nodeName = parameters["nodeName"]?.ToString();
            float offsetX = parameters["offsetX"]?.Value<float>() ?? 50f;
            float offsetY = parameters["offsetY"]?.Value<float>() ?? 50f;

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);
            var sourceNode = SGReflection.FindNode(graph, nodeId, nodeName);
            if (sourceNode == null) return new { success = false, message = "Source node not found" };

            var nodeType = sourceNode.GetType();

            // BlockNode lives inside Context — cannot be duplicated independently
            if (nodeType == SGReflection.T_BlockNode || nodeType.IsSubclassOf(SGReflection.T_BlockNode))
                return new { success = false, message = "BlockNode cannot be duplicated independently" };

            // Check canCopyNode
            var canCopy = SGReflection.Prop(sourceNode, "canCopyNode");
            if (canCopy is bool b && !b)
                return new { success = false, message = $"Node {GetNodeName(sourceNode)} does not support copy" };

            // Get source position
            var drawState = SGReflection.Prop(sourceNode, "drawState");
            var pos = (Rect)SGReflection.Prop(drawState, "position");

            // Use MultiJson to serialize/deserialize — same mechanism as ShaderGraph's own copy/paste
            // FunctionRegistry.ProvideFunction deduplicates by hlslFunctionName, so same functionName
            // across multiple Custom Function String nodes is safe (function body emitted only once).
            var multiJsonType = SGReflection.GetSGType("UnityEditor.ShaderGraph.Serialization.MultiJson");
            if (multiJsonType == null)
                return new { success = false, message = "MultiJson type not found in ShaderGraph assembly" };

            var serializeMethod = multiJsonType.GetMethod("Serialize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var deserializeMethod = multiJsonType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Deserialize" && m.IsGenericMethodDefinition && m.GetParameters().Length == 4);

            if (serializeMethod == null || deserializeMethod == null)
                return new { success = false, message = "MultiJson Serialize/Deserialize methods not found" };

            // Serialize source node to JSON
            string json;
            try
            {
                json = (string)serializeMethod.Invoke(null, new object[] { sourceNode });
            }
            catch (Exception e)
            {
                return new { success = false, message = $"MultiJson.Serialize failed: {e.InnerException?.Message ?? e.Message}" };
            }

            if (string.IsNullOrEmpty(json))
                return new { success = false, message = "Serialization produced empty JSON" };

            // Deserialize into a new node instance
            var newNode = Activator.CreateInstance(nodeType);
            var concreteDeserialize = deserializeMethod.MakeGenericMethod(nodeType);

            try
            {
                // Deserialize(nodeToOverwrite, json, referenceRoot, rewriteIds)
                // rewriteIds=true generates new objectIds to avoid collisions
                concreteDeserialize.Invoke(null, new object[] { newNode, json, null, true });
            }
            catch (Exception e)
            {
                return new { success = false, message = $"MultiJson.Deserialize failed: {e.InnerException?.Message ?? e.Message}" };
            }

            // Setup slots (called during OnAfterMultiDeserialize in CopyPasteGraph, but we call it manually)
            var setupSlotsMethod = SGReflection.CacheMethod(nodeType, "SetupSlots")
                ?? SGReflection.CacheMethod(SGReflection.T_AbstractMaterialNode, "SetupSlots");
            setupSlotsMethod?.Invoke(newNode, null);

            // Offset position
            SGReflection.SetNodePosition(newNode, pos.x + offsetX, pos.y + offsetY);

            // For PropertyNode: re-link to the same property in the graph
            if (nodeType == SGReflection.T_PropertyNode || nodeType.IsSubclassOf(SGReflection.T_PropertyNode))
            {
                var prop = SGReflection.Prop(sourceNode, "property");
                if (prop != null) SGReflection.SetProp(newNode, "property", prop);
            }

            // Add to graph
            SGReflection.AddNodeToGraph(graph, newNode, $"Duplicate {nodeType.Name}");
            SGReflection.DirtyNode(newNode);
            SGReflection.RefreshGraphUI();

            var sourceDisplayName = (string)SGReflection.Prop(sourceNode, "displayName") ?? GetNodeName(sourceNode);

            return new
            {
                success = true,
                message = $"Duplicated: {sourceDisplayName}",
                nodeId = (string)SGReflection.Prop(newNode, "objectId"),
                nodeName = GetNodeName(newNode),
                originalNodeId = (string)SGReflection.Prop(sourceNode, "objectId"),
                originalNodeName = GetNodeName(sourceNode)
            };
        }

        // ================================================================
        //  shader_graph_add_slot
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_add_slot", "为节点添加端口(Slot)，支持 Custom Function 等节点的端口扩展")]
        public static object AddSlot(JObject parameters)
        {
            string nodeId = parameters["nodeId"]?.ToString();
            string nodeName = parameters["nodeName"]?.ToString();
            string displayName = parameters["displayName"]?.ToString();
            string slotType = parameters["slotType"]?.ToString();   // e.g. "Vector2", "Float", "Texture2D"
            string direction = parameters["direction"]?.ToString();  // "Input" or "Output"
            int? slotId = parameters["slotId"]?.Value<int>();

            if (string.IsNullOrEmpty(displayName))
                return new { success = false, message = "Missing displayName parameter" };
            if (string.IsNullOrEmpty(slotType))
                return new { success = false, message = "Missing slotType parameter (e.g. Vector1, Vector2, Vector3, Vector4, Float, Color, Texture2D, Boolean, DynamicVector)" };

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);
            var node = SGReflection.FindNode(graph, nodeId, nodeName);
            if (node == null) return new { success = false, message = $"Node not found: {nodeId ?? nodeName}" };

            bool isInput = string.IsNullOrEmpty(direction) || direction.Equals("Input", StringComparison.OrdinalIgnoreCase);

            // Auto-assign slotId if not provided: find max existing slot id + 1
            if (!slotId.HasValue)
            {
                var allSlots = SGReflection.GetInputSlots(node).Concat(SGReflection.GetOutputSlots(node));
                int maxId = -1;
                foreach (var s in allSlots)
                {
                    int id = SGReflection.GetSlotId(s);
                    if (id > maxId) maxId = id;
                }
                slotId = maxId + 1;
            }

            // Create the slot
            var slot = SGReflection.CreateMaterialSlot(slotType, slotId.Value, displayName, isInput);
            if (slot == null)
                return new { success = false, message = $"Failed to create slot of type: {slotType}" };

            // Add to node
            bool added = SGReflection.AddSlotToNode(node, slot);
            if (!added)
                return new { success = false, message = "Failed to add slot to node (AddSlot method not available)" };

            SGReflection.RefreshGraphUI();

            return new
            {
                success = true,
                message = $"Added {(isInput ? "input" : "output")} slot '{displayName}' (type={slotType}, id={slotId.Value}) to node",
                nodeId = (string)SGReflection.Prop(node, "objectId"),
                slotId = slotId.Value,
                displayName,
                slotType,
                direction = isInput ? "Input" : "Output"
            };
        }

        // ================================================================
        //  shader_graph_delete_property
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_delete_property", "删除属性")]
        public static object DeleteProperty(JObject parameters)
        {
            string referenceName = parameters["referenceName"]?.ToString();
            string displayName = parameters["displayName"]?.ToString();

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);

            var property = SGReflection.FindProperty(graph, referenceName, displayName);
            if (property == null)
                return new { success = false, message = $"Property not found: {referenceName ?? displayName}" };

            var name = (string)SGReflection.Prop(property, "displayName");
            SGReflection.RemoveGraphInput(graph, property, $"Delete Property {name}");
            SGReflection.RefreshGraphUI();

            return new { success = true, message = $"Deleted property: {name}", referenceName = (string)SGReflection.Prop(property, "referenceName") };
        }

        // ================================================================
        //  shader_graph_copy_custom_function
        // ================================================================
        [ExecuteCustomTool.CustomTool("shader_graph_copy_custom_function",
            "复制 Custom Function 节点（含所有 Slot、函数体、Source 引用）。同名同源的复制由 ShaderGraph 内部去重保证安全。")]
        public static object CopyCustomFunction(JObject parameters)
        {
            string nodeId = parameters["nodeId"]?.ToString();
            string nodeName = parameters["nodeName"]?.ToString();

            var window = SGReflection.GetFocusedShaderGraphWindow();
            if (window == null) return new { success = false, message = "No Shader Graph window open" };
            var graph = SGReflection.GetGraphFromWindow(window);
            var srcNode = SGReflection.FindNode(graph, nodeId, nodeName);
            if (srcNode == null) return new { success = false, message = $"Source node not found: {nodeId ?? nodeName}" };

            var nodeType = srcNode.GetType();
            if (nodeType != SGReflection.T_CustomFunctionNode && !nodeType.IsSubclassOf(SGReflection.T_CustomFunctionNode))
                return new { success = false, message = $"Node is not a Custom Function node: {nodeType.Name}" };

            // --- Read source configuration ---
            string srcFunctionName = (string)SGReflection.Prop(srcNode, "functionName");
            string srcSourceTypeStr = SGReflection.Prop(srcNode, "sourceType")?.ToString();
            string srcFunctionBody = (string)SGReflection.Prop(srcNode, "functionBody");
            string srcFunctionSource = (string)SGReflection.Prop(srcNode, "functionSource");
            bool isFileMode = srcSourceTypeStr == "File";

            // --- Read position ---
            var drawState = SGReflection.Prop(srcNode, "drawState");
            var srcPos = (Rect)SGReflection.Prop(drawState, "position");
            float newX = parameters["x"]?.Value<float>() ?? srcPos.x + 300;
            float newY = parameters["y"]?.Value<float>() ?? srcPos.y;

            // --- Read source slots (capture references BEFORE creating new node) ---
            var srcInputs = SGReflection.GetInputSlots(srcNode);
            var srcOutputs = SGReflection.GetOutputSlots(srcNode);

            var slotCaptures = new List<(int id, string rawDisplay, string typeName, object srcSlotRef, bool isInput, bool bareResource)>();
            foreach (var slot in srcInputs)
            {
                slotCaptures.Add((
                    SGReflection.GetSlotId(slot),
                    SGReflection.GetSlotRawDisplayName(slot),
                    SGReflection.GetSlotTypeName(slot),
                    slot, true,
                    SGReflection.GetSlotBareResource(slot)
                ));
            }
            foreach (var slot in srcOutputs)
            {
                slotCaptures.Add((
                    SGReflection.GetSlotId(slot),
                    SGReflection.GetSlotRawDisplayName(slot),
                    SGReflection.GetSlotTypeName(slot),
                    slot, false,
                    SGReflection.GetSlotBareResource(slot)
                ));
            }

            // --- Handle file copy ---
            // copyFile creates an independent .hlsl copy with include guards,
            // for when the user wants to modify the HLSL independently.
            bool copyFile = parameters["copyFile"]?.Value<bool>() ?? false;
            string newFileGuid = null;
            string newFilePathResolved = null;

            if (isFileMode && copyFile)
            {
                string origPath = AssetDatabase.GUIDToAssetPath(srcFunctionSource);
                if (string.IsNullOrEmpty(origPath)) origPath = srcFunctionSource;

                if (string.IsNullOrEmpty(origPath) || !File.Exists(origPath))
                    return new { success = false, message = $"Cannot copy file: original file not found for GUID/path: {srcFunctionSource}" };

                string fileContent = File.ReadAllText(origPath);

                string newFilePath = parameters["newFilePath"]?.ToString();
                if (string.IsNullOrEmpty(newFilePath))
                {
                    string dir = Path.GetDirectoryName(origPath);
                    string nameNoExt = Path.GetFileNameWithoutExtension(origPath);
                    string ext = Path.GetExtension(origPath);
                    newFilePath = Path.Combine(dir, $"{nameNoExt}_Copy{ext}");
                }
                newFilePath = newFilePath.Replace('\\', '/');
                if (!newFilePath.StartsWith("Assets/") && !newFilePath.StartsWith("assets/"))
                    newFilePath = "Assets/" + newFilePath;

                // Wrap with include guard so both files can coexist in the same shader
                string guardName = GenerateIncludeGuardName(newFilePath);
                string guardedContent =
                    $"#ifndef {guardName}\n" +
                    $"#define {guardName}\n\n" +
                    $"{fileContent}\n\n" +
                    $"#endif // {guardName}\n";

                var dirPath = Path.GetDirectoryName(newFilePath);
                if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);

                File.WriteAllText(newFilePath, guardedContent);
                AssetDatabase.Refresh();
                newFileGuid = AssetDatabase.AssetPathToGUID(newFilePath);
                newFilePathResolved = newFilePath;
            }

            // --- Create new node (same function name, same source as original) ---
            var newNode = Activator.CreateInstance(SGReflection.T_CustomFunctionNode);
            SGReflection.SetNodePosition(newNode, newX, newY);

            var parsedSourceType = SGReflection.ParseEnum(SGReflection.T_HlslSourceType, srcSourceTypeStr);
            if (parsedSourceType != null) SGReflection.SetProp(newNode, "sourceType", parsedSourceType);

            SGReflection.SetProp(newNode, "functionName", srcFunctionName);

            if (isFileMode)
            {
                // Use copied file if requested, otherwise same source GUID (IncludeCollection dedups)
                SGReflection.SetProp(newNode, "functionSource",
                    (copyFile && newFileGuid != null) ? newFileGuid : srcFunctionSource);
            }
            else
            {
                if (!string.IsNullOrEmpty(srcFunctionBody))
                    SGReflection.SetProp(newNode, "functionBody", srcFunctionBody);
            }

            SGReflection.AddNodeToGraph(graph, newNode, $"Copy Custom Function {srcFunctionName}");

            // --- Copy all slots (official ShaderGraph pattern from ReorderableSlotListView) ---
            int slotsCopied = 0;
            var orderedSlotIds = new List<int>();

            foreach (var sc in slotCaptures)
            {
                var newSlot = SGReflection.CreateMaterialSlot(sc.typeName, sc.id, sc.rawDisplay, sc.isInput);
                if (newSlot == null) continue;

                SGReflection.CopySlotValues(newSlot, sc.srcSlotRef);
                SGReflection.SetSlotBareResource(newSlot, sc.bareResource);
                SGReflection.AddSlotToNode(newNode, newSlot);
                orderedSlotIds.Add(sc.id);
                slotsCopied++;
            }

            if (orderedSlotIds.Count > 0)
                SGReflection.SetSlotOrder(newNode, orderedSlotIds);

            SGReflection.ValidateNode(newNode);
            SGReflection.DirtyNode(newNode);
            SGReflection.RefreshGraphUI();

            return new
            {
                success = true,
                message = $"Copied Custom Function node: {srcFunctionName}",
                nodeId = (string)SGReflection.Prop(newNode, "objectId"),
                nodeName = GetNodeName(newNode),
                sourceNodeName = GetNodeName(srcNode),
                functionName = srcFunctionName,
                sourceType = srcSourceTypeStr,
                slotsCopied,
                fileCopied = copyFile && isFileMode,
                newFilePath = newFilePathResolved,
                includeGuardApplied = copyFile && isFileMode
            };
        }

        /// <summary>
        /// Generates a unique include guard name from a file path.
        /// </summary>
        private static string GenerateIncludeGuardName(string filePath)
        {
            string relative = filePath.Replace('\\', '/');
            if (relative.StartsWith("Assets/")) relative = relative.Substring(7);
            var sb = new System.Text.StringBuilder();
            foreach (char c in relative)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToUpperInvariant(c));
                else
                    sb.Append('_');
            }
            sb.Append("_INCLUDED");
            return sb.ToString();
        }

        // ================================================================
        //  Helpers
        // ================================================================
        private static string GetNodeName(object node)
        {
            if (node is UnityEngine.Object uo) return uo.name;
            var nameProp = SGReflection.CacheProp(node.GetType(), "name") ?? SGReflection.CacheProp(node.GetType(), "displayName");
            return nameProp?.GetValue(node)?.ToString() ?? node.GetType().Name;
        }

        private static int GetNodeStaticFieldId(Type nodeType, string fieldName)
        {
            var field = nodeType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null) return (int)field.GetValue(null);
            Debug.LogWarning($"[MCPtoolsShaderGraph] Could not find field {fieldName} on {nodeType.Name}");
            return 0;
        }

        private static int Vector1NodeInputSlotXId() => GetNodeStaticFieldId(SGReflection.T_Vector1Node, "InputSlotXId");
        private static int Vector2NodeInputSlotXId() => GetNodeStaticFieldId(SGReflection.T_Vector2Node, "InputSlotXId");
        private static int Vector2NodeInputSlotYId() => GetNodeStaticFieldId(SGReflection.T_Vector2Node, "InputSlotYId");
        private static int Vector3NodeInputSlotXId() => GetNodeStaticFieldId(SGReflection.T_Vector3Node, "InputSlotXId");
        private static int Vector3NodeInputSlotYId() => GetNodeStaticFieldId(SGReflection.T_Vector3Node, "InputSlotYId");
        private static int Vector3NodeInputSlotZId() => GetNodeStaticFieldId(SGReflection.T_Vector3Node, "InputSlotZId");
        private static int Vector4NodeInputSlotXId() => GetNodeStaticFieldId(SGReflection.T_Vector4Node, "InputSlotXId");
        private static int Vector4NodeInputSlotYId() => GetNodeStaticFieldId(SGReflection.T_Vector4Node, "InputSlotYId");
        private static int Vector4NodeInputSlotZId() => GetNodeStaticFieldId(SGReflection.T_Vector4Node, "InputSlotZId");
        private static int Vector4NodeInputSlotWId() => GetNodeStaticFieldId(SGReflection.T_Vector4Node, "InputSlotWId");
    }
}

