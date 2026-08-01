using Codely.Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityTcp.Editor.Tools;

namespace UnityTcp.MaterialTools
{
    /// <summary>
    /// Material 处理工具集 - 细粒度、单一职责的工具
    /// </summary>
    public static class MCPtoolsMaterial
    {
        #region Material 操作工具

        /// <summary>
        /// 获取材质信息
        /// 参数:
        ///   path - Material 资产路径
        ///   guid - Material GUID (与 path 二选一)
        /// </summary>
        [ExecuteCustomTool.CustomTool("material_get_info", "获取材质的详细信息和属性列表")]
        internal static object GetMaterialInfo(JObject parameters)
        {
            string path = parameters["path"]?.ToString();
            string guid = parameters["guid"]?.ToString();

            if (!string.IsNullOrEmpty(guid))
            {
                path = AssetDatabase.GUIDToAssetPath(guid);
            }

            if (string.IsNullOrEmpty(path))
            {
                return new { success = false, message = "path or guid parameter is required" };
            }

            try
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    return new { success = false, message = $"Material not found at: {path}" };
                }

                var properties = new List<object>();
                var shader = mat.shader;

                for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                {
                    string propName = ShaderUtil.GetPropertyName(shader, i);
                    var propType = ShaderUtil.GetPropertyType(shader, i);
                    var propDesc = ShaderUtil.GetPropertyDescription(shader, i);

                    var propInfo = new Dictionary<string, object>
                    {
                        ["name"] = propName,
                        ["displayName"] = propDesc,
                        ["type"] = propType.ToString()
                    };

                    // 获取当前值
                    switch (propType)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            propInfo["value"] = new { r = mat.GetColor(propName).r, g = mat.GetColor(propName).g, b = mat.GetColor(propName).b, a = mat.GetColor(propName).a };
                            break;
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            propInfo["value"] = mat.GetFloat(propName);
                            break;
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            var tex = mat.GetTexture(propName);
                            propInfo["texture"] = tex != null ? tex.name : null;
                            propInfo["texturePath"] = tex != null ? AssetDatabase.GetAssetPath(tex) : null;
                            propInfo["offset"] = new { x = mat.GetTextureOffset(propName).x, y = mat.GetTextureOffset(propName).y };
                            propInfo["scale"] = new { x = mat.GetTextureScale(propName).x, y = mat.GetTextureScale(propName).y };
                            break;
                        case ShaderUtil.ShaderPropertyType.Vector:
                            var v = mat.GetVector(propName);
                            propInfo["value"] = new { x = v.x, y = v.y, z = v.z, w = v.w };
                            break;
                    }

                    properties.Add(propInfo);
                }

                return new
                {
                    success = true,
                    material = new
                    {
                        name = mat.name,
                        path = path,
                        guid = AssetDatabase.AssetPathToGUID(path),
                        shader = shader.name,
                        shaderPath = AssetDatabase.GetAssetPath(shader),
                        propertyCount = properties.Count,
                        properties = properties
                    }
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error getting material info: {e.Message}" };
            }
        }

        /// <summary>
        /// 设置材质的 Shader
        /// 参数:
        ///   materialPath - Material 资产路径
        ///   materialGuid - Material GUID (与 materialPath 二选一)
        ///   shaderPath - Shader 资产路径
        ///   shaderGuid - Shader GUID (与 shaderPath 二选一)
        ///   shaderName - Shader 名称 (如 "Standard", "HDRP/Lit")
        /// </summary>
        [ExecuteCustomTool.CustomTool("material_set_shader", "设置材质的 Shader")]
        internal static object SetMaterialShader(JObject parameters)
        {
            string materialPath = parameters["materialPath"]?.ToString();
            string materialGuid = parameters["materialGuid"]?.ToString();
            string shaderPath = parameters["shaderPath"]?.ToString();
            string shaderGuid = parameters["shaderGuid"]?.ToString();
            string shaderName = parameters["shaderName"]?.ToString();

            // 解析 Material 路径
            if (!string.IsNullOrEmpty(materialGuid))
            {
                materialPath = AssetDatabase.GUIDToAssetPath(materialGuid);
            }

            if (string.IsNullOrEmpty(materialPath))
            {
                return new { success = false, message = "materialPath or materialGuid is required" };
            }

            // 解析 Shader
            Shader shader = null;

            if (!string.IsNullOrEmpty(shaderGuid))
            {
                shaderPath = AssetDatabase.GUIDToAssetPath(shaderGuid);
            }

            if (!string.IsNullOrEmpty(shaderPath))
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            }
            else if (!string.IsNullOrEmpty(shaderName))
            {
                shader = Shader.Find(shaderName);
            }

            if (shader == null)
            {
                return new { success = false, message = $"Shader not found. Provide shaderPath, shaderGuid, or shaderName" };
            }

            try
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (mat == null)
                {
                    return new { success = false, message = $"Material not found at: {materialPath}" };
                }

                string oldShader = mat.shader.name;
                mat.shader = shader;

                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();

                return new
                {
                    success = true,
                    message = $"Shader changed from '{oldShader}' to '{shader.name}'",
                    material = materialPath,
                    oldShader = oldShader,
                    newShader = shader.name
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error setting shader: {e.Message}" };
            }
        }

        /// <summary>
        /// 设置材质的贴图属性
        /// 参数:
        ///   materialPath - Material 资产路径
        ///   materialGuid - Material GUID
        ///   propertyName - 贴图属性名 (如 "_MainTex", "_BaseMap")
        ///   texturePath - 贴图资产路径
        ///   textureGuid - 贴图 GUID
        ///   offset - UV 偏移 [x, y] (可选)
        ///   scale - UV 缩放 [x, y] (可选)
        /// </summary>
        [ExecuteCustomTool.CustomTool("material_set_texture", "设置材质的贴图属性")]
        internal static object SetMaterialTexture(JObject parameters)
        {
            string materialPath = parameters["materialPath"]?.ToString();
            string materialGuid = parameters["materialGuid"]?.ToString();
            string propertyName = parameters["propertyName"]?.ToString();
            string texturePath = parameters["texturePath"]?.ToString();
            string textureGuid = parameters["textureGuid"]?.ToString();

            // 解析 Material 路径
            if (!string.IsNullOrEmpty(materialGuid))
            {
                materialPath = AssetDatabase.GUIDToAssetPath(materialGuid);
            }

            if (string.IsNullOrEmpty(materialPath))
            {
                return new { success = false, message = "materialPath or materialGuid is required" };
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                return new { success = false, message = "propertyName is required" };
            }

            // 解析 Texture 路径
            if (!string.IsNullOrEmpty(textureGuid))
            {
                texturePath = AssetDatabase.GUIDToAssetPath(textureGuid);
            }

            try
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (mat == null)
                {
                    return new { success = false, message = $"Material not found at: {materialPath}" };
                }

                if (!mat.HasProperty(propertyName))
                {
                    return new { success = false, message = $"Material does not have property: {propertyName}" };
                }

                Texture oldTexture = mat.GetTexture(propertyName);
                string oldTextureName = oldTexture != null ? oldTexture.name : null;

                if (string.IsNullOrEmpty(texturePath))
                {
                    // 清除贴图
                    mat.SetTexture(propertyName, null);
                }
                else
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture == null)
                    {
                        return new { success = false, message = $"Texture not found at: {texturePath}" };
                    }
                    mat.SetTexture(propertyName, texture);
                }

                // 设置 UV offset 和 scale
                if (parameters["offset"] is JArray offsetArr && offsetArr.Count == 2)
                {
                    mat.SetTextureOffset(propertyName, new Vector2(offsetArr[0].Value<float>(), offsetArr[1].Value<float>()));
                }

                if (parameters["scale"] is JArray scaleArr && scaleArr.Count == 2)
                {
                    mat.SetTextureScale(propertyName, new Vector2(scaleArr[0].Value<float>(), scaleArr[1].Value<float>()));
                }

                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();

                return new
                {
                    success = true,
                    message = $"Texture '{propertyName}' set successfully",
                    material = materialPath,
                    property = propertyName,
                    oldTexture = oldTextureName,
                    newTexture = string.IsNullOrEmpty(texturePath) ? null : Path.GetFileName(texturePath)
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error setting texture: {e.Message}" };
            }
        }

        /// <summary>
        /// 设置材质的数值属性 (Float, Range, Color, Vector)
        /// 参数:
        ///   materialPath - Material 资产路径
        ///   materialGuid - Material GUID
        ///   propertyName - 属性名
        ///   value - 属性值 (float, [r,g,b,a], [x,y,z,w])
        /// </summary>
        [ExecuteCustomTool.CustomTool("material_set_property", "设置材质的数值属性")]
        internal static object SetMaterialProperty(JObject parameters)
        {
            string materialPath = parameters["materialPath"]?.ToString();
            string materialGuid = parameters["materialGuid"]?.ToString();
            string propertyName = parameters["propertyName"]?.ToString();
            var value = parameters["value"];

            // 解析 Material 路径
            if (!string.IsNullOrEmpty(materialGuid))
            {
                materialPath = AssetDatabase.GUIDToAssetPath(materialGuid);
            }

            if (string.IsNullOrEmpty(materialPath) || string.IsNullOrEmpty(propertyName))
            {
                return new { success = false, message = "materialPath/materialGuid and propertyName are required" };
            }

            try
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (mat == null)
                {
                    return new { success = false, message = $"Material not found at: {materialPath}" };
                }

                if (!mat.HasProperty(propertyName))
                {
                    return new { success = false, message = $"Material does not have property: {propertyName}" };
                }

                // 根据值类型设置
                if (value is JValue jval && jval.Value is IConvertible)
                {
                    mat.SetFloat(propertyName, Convert.ToSingle(jval.Value));
                }
                else if (value is JArray arr)
                {
                    if (arr.Count == 4)
                    {
                        // Color 或 Vector
                        mat.SetColor(propertyName, new Color(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>(), arr[3].Value<float>()));
                    }
                    else if (arr.Count == 3)
                    {
                        mat.SetVector(propertyName, new Vector3(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>()));
                    }
                    else if (arr.Count == 2)
                    {
                        mat.SetVector(propertyName, new Vector2(arr[0].Value<float>(), arr[1].Value<float>()));
                    }
                }

                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();

                return new
                {
                    success = true,
                    message = $"Property '{propertyName}' set successfully",
                    material = materialPath,
                    property = propertyName
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error setting property: {e.Message}" };
            }
        }

        /// <summary>
        /// 创建新的 Material
        /// 参数:
        ///   path - 保存路径 (如 "Assets/Materials/NewMat.mat")
        ///   shaderPath - Shader 路径 (可选)
        ///   shaderName - Shader 名称 (可选)
        /// </summary>
        [ExecuteCustomTool.CustomTool("material_create", "创建新的 Material")]
        internal static object CreateMaterial(JObject parameters)
        {
            string path = parameters["path"]?.ToString();
            string shaderPath = parameters["shaderPath"]?.ToString();
            string shaderName = parameters["shaderName"]?.ToString();

            if (string.IsNullOrEmpty(path))
            {
                return new { success = false, message = "path is required (e.g. 'Assets/Materials/NewMat.mat')" };
            }

            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 解析 Shader
                Shader shader = null;
                if (!string.IsNullOrEmpty(shaderPath))
                {
                    shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                }
                else if (!string.IsNullOrEmpty(shaderName))
                {
                    shader = Shader.Find(shaderName);
                }

                // 创建 Material
                Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
                mat.name = Path.GetFileNameWithoutExtension(path);

                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new
                {
                    success = true,
                    message = $"Material created at: {path}",
                    material = new
                    {
                        name = mat.name,
                        path = path,
                        guid = AssetDatabase.AssetPathToGUID(path),
                        shader = mat.shader.name
                    }
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error creating material: {e.Message}" };
            }
        }

        #endregion

        #region FBX 嵌入材质工具

        /// <summary>
        /// 获取 FBX 文件中嵌入的材质列表
        /// 参数:
        ///   path - FBX 文件路径
        ///   guid - FBX 文件 GUID
        /// </summary>
        [ExecuteCustomTool.CustomTool("fbx_get_embedded_materials", "获取 FBX 文件中嵌入的材质列表")]
        internal static object GetFBXEmbeddedMaterials(JObject parameters)
        {
            string path = parameters["path"]?.ToString();
            string guid = parameters["guid"]?.ToString();

            if (!string.IsNullOrEmpty(guid))
            {
                path = AssetDatabase.GUIDToAssetPath(guid);
            }

            if (string.IsNullOrEmpty(path))
            {
                return new { success = false, message = "path or guid is required" };
            }

            try
            {
                var materials = new List<object>();
                var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);

                foreach (var asset in assets)
                {
                    if (asset is Material mat)
                    {
                        materials.Add(new
                        {
                            name = mat.name,
                            instanceID = mat.GetInstanceID(),
                            shader = mat.shader.name
                        });
                    }
                }

                return new
                {
                    success = true,
                    fbxPath = path,
                    fbxGuid = AssetDatabase.AssetPathToGUID(path),
                    materialCount = materials.Count,
                    materials = materials
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error getting embedded materials: {e.Message}" };
            }
        }

        /// <summary>
        /// 提取 FBX 嵌入的材质为独立资产
        /// 参数:
        ///   fbxPath - FBX 文件路径
        ///   fbxGuid - FBX 文件 GUID
        ///   outputFolder - 输出目录 (可选，默认与 FBX 同目录)
        ///   materialNames - 要提取的材质名称列表 (可选，默认提取全部)
        ///   useFbxNameAsPrefix - 是否使用 FBX 名称作为材质名前缀 (默认 true)
        /// </summary>
        [ExecuteCustomTool.CustomTool("fbx_extract_materials", "提取 FBX 嵌入的材质为独立资产")]
        internal static object ExtractFBXMaterials(JObject parameters)
        {
            string fbxPath = parameters["fbxPath"]?.ToString();
            string fbxGuid = parameters["fbxGuid"]?.ToString();
            string outputFolder = parameters["outputFolder"]?.ToString();
            var materialNames = parameters["materialNames"] as JArray;
            bool useFbxNameAsPrefix = parameters["useFbxNameAsPrefix"]?.Value<bool>() ?? true;

            if (!string.IsNullOrEmpty(fbxGuid))
            {
                fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuid);
            }

            if (string.IsNullOrEmpty(fbxPath))
            {
                return new { success = false, message = "fbxPath or fbxGuid is required" };
            }

            try
            {
                if (string.IsNullOrEmpty(outputFolder))
                {
                    outputFolder = Path.GetDirectoryName(fbxPath);
                }

                // 确保输出目录存在
                if (!AssetDatabase.IsValidFolder(outputFolder))
                {
                    string parent = Path.GetDirectoryName(outputFolder);
                    string folderName = Path.GetFileName(outputFolder);
                    if (!AssetDatabase.IsValidFolder(parent))
                    {
                        Directory.CreateDirectory(parent);
                        AssetDatabase.Refresh();
                    }
                    if (!AssetDatabase.IsValidFolder(outputFolder))
                    {
                        AssetDatabase.CreateFolder(parent, folderName);
                    }
                }

                // 获取 FBX 名称作为前缀
                string fbxName = Path.GetFileNameWithoutExtension(fbxPath);

                var extractedMaterials = new List<object>();
                var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
                HashSet<string> targetNames = null;

                if (materialNames != null && materialNames.Count > 0)
                {
                    targetNames = new HashSet<string>(materialNames.Select(n => n.ToString()), StringComparer.OrdinalIgnoreCase);
                }

                foreach (var asset in assets)
                {
                    if (asset is Material mat)
                    {
                        if (targetNames != null && !targetNames.Contains(mat.name))
                            continue;

                        // 使用 FBX 名称作为前缀
                        string materialName = useFbxNameAsPrefix ? $"{fbxName}_{mat.name}" : mat.name;
                        string materialPath = $"{outputFolder}/{materialName}.mat";

                        // 检查是否已存在
                        Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (existingMat == null)
                        {
                            // 复制材质
                            Material newMat = new Material(mat);
                            newMat.name = materialName;
                            AssetDatabase.CreateAsset(newMat, materialPath);
                            extractedMaterials.Add(new { name = materialName, originalName = mat.name, path = materialPath, isNew = true });
                        }
                        else
                        {
                            extractedMaterials.Add(new { name = materialName, originalName = mat.name, path = materialPath, isNew = false });
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new
                {
                    success = true,
                    message = $"Extracted {extractedMaterials.Count} materials",
                    fbxPath = fbxPath,
                    outputFolder = outputFolder,
                    materials = extractedMaterials
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error extracting materials: {e.Message}" };
            }
        }

        #endregion

        #region 贴图导入设置工具

        /// <summary>
        /// 获取贴图的导入设置
        /// 参数:
        ///   path - 贴图路径
        ///   guid - 贴图 GUID
        /// </summary>
        [ExecuteCustomTool.CustomTool("texture_get_import_settings", "获取贴图的导入设置")]
        internal static object GetTextureImportSettings(JObject parameters)
        {
            string path = parameters["path"]?.ToString();
            string guid = parameters["guid"]?.ToString();

            if (!string.IsNullOrEmpty(guid))
            {
                path = AssetDatabase.GUIDToAssetPath(guid);
            }

            if (string.IsNullOrEmpty(path))
            {
                return new { success = false, message = "path or guid is required" };
            }

            try
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    return new { success = false, message = $"Texture importer not found for: {path}" };
                }

                return new
                {
                    success = true,
                    texture = new
                    {
                        name = Path.GetFileNameWithoutExtension(path),
                        path = path,
                        guid = AssetDatabase.AssetPathToGUID(path),
                        textureType = importer.textureType.ToString(),
                        sRGB = importer.sRGBTexture,
                        isReadable = importer.isReadable,
                        maxTextureSize = importer.maxTextureSize,
                        textureFormat = importer.GetAutomaticFormat("Default").ToString(),
                        wrapMode = importer.wrapMode.ToString(),
                        filterMode = importer.filterMode.ToString(),
                        anisoLevel = importer.anisoLevel,
                        mipMapEnabled = importer.mipmapEnabled
                    }
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error getting texture settings: {e.Message}" };
            }
        }

        /// <summary>
        /// 设置贴图的导入设置
        /// 参数:
        ///   path - 贴图路径
        ///   guid - 贴图 GUID
        ///   textureType - 贴图类型 ("Default", "NormalMap", "Sprite", etc.)
        ///   sRGB - 是否为 sRGB 色彩空间 (true/false)
        ///   isReadable - 是否可读
        ///   maxTextureSize - 最大尺寸
        ///   wrapMode - 环绕模式
        ///   filterMode - 过滤模式
        ///   anisoLevel - 各向异性等级
        ///   mipMapEnabled - 是否启用 Mipmap
        /// </summary>
        [ExecuteCustomTool.CustomTool("texture_set_import_settings", "设置贴图的导入设置")]
        internal static object SetTextureImportSettings(JObject parameters)
        {
            string path = parameters["path"]?.ToString();
            string guid = parameters["guid"]?.ToString();

            if (!string.IsNullOrEmpty(guid))
            {
                path = AssetDatabase.GUIDToAssetPath(guid);
            }

            if (string.IsNullOrEmpty(path))
            {
                return new { success = false, message = "path or guid is required" };
            }

            try
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    return new { success = false, message = $"Texture importer not found for: {path}" };
                }

                var changes = new List<string>();

                // Texture Type
                if (parameters["textureType"] != null)
                {
                    string typeStr = parameters["textureType"].ToString();
                    if (Enum.TryParse<TextureImporterType>(typeStr, out var texType))
                    {
                        importer.textureType = texType;
                        changes.Add($"textureType: {texType}");
                    }
                }

                // sRGB
                if (parameters["sRGB"] != null)
                {
                    bool srgb = parameters["sRGB"].Value<bool>();
                    importer.sRGBTexture = srgb;
                    changes.Add($"sRGB: {srgb}");
                }

                // isReadable
                if (parameters["isReadable"] != null)
                {
                    importer.isReadable = parameters["isReadable"].Value<bool>();
                    changes.Add($"isReadable: {importer.isReadable}");
                }

                // maxTextureSize
                if (parameters["maxTextureSize"] != null)
                {
                    importer.maxTextureSize = parameters["maxTextureSize"].Value<int>();
                    changes.Add($"maxTextureSize: {importer.maxTextureSize}");
                }

                // wrapMode
                if (parameters["wrapMode"] != null)
                {
                    string wrapStr = parameters["wrapMode"].ToString();
                    if (Enum.TryParse<TextureWrapMode>(wrapStr, out var wrapMode))
                    {
                        importer.wrapMode = wrapMode;
                        changes.Add($"wrapMode: {wrapMode}");
                    }
                }

                // filterMode
                if (parameters["filterMode"] != null)
                {
                    string filterStr = parameters["filterMode"].ToString();
                    if (Enum.TryParse<FilterMode>(filterStr, out var filterMode))
                    {
                        importer.filterMode = filterMode;
                        changes.Add($"filterMode: {filterMode}");
                    }
                }

                // anisoLevel
                if (parameters["anisoLevel"] != null)
                {
                    importer.anisoLevel = parameters["anisoLevel"].Value<int>();
                    changes.Add($"anisoLevel: {importer.anisoLevel}");
                }

                // mipMapEnabled
                if (parameters["mipMapEnabled"] != null)
                {
                    importer.mipmapEnabled = parameters["mipMapEnabled"].Value<bool>();
                    changes.Add($"mipMapEnabled: {importer.mipmapEnabled}");
                }

                if (changes.Count > 0)
                {
                    importer.SaveAndReimport();
                }

                return new
                {
                    success = true,
                    message = changes.Count > 0 ? $"Updated: {string.Join(", ", changes)}" : "No changes made",
                    texture = path,
                    changes = changes
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error setting texture settings: {e.Message}" };
            }
        }

        /// <summary>
        /// 批量设置贴图导入设置
        /// 参数:
        ///   paths - 贴图路径数组
        ///   guids - 贴图 GUID 数组
        ///   textureType - 贴图类型
        ///   sRGB - 是否为 sRGB
        ///   ... (其他参数同 texture_set_import_settings)
        /// </summary>
        [ExecuteCustomTool.CustomTool("texture_batch_set_import_settings", "批量设置多个贴图的导入设置")]
        internal static object BatchSetTextureImportSettings(JObject parameters)
        {
            var paths = new List<string>();
            var pathArr = parameters["paths"] as JArray;
            var guidArr = parameters["guids"] as JArray;

            if (pathArr != null)
            {
                paths.AddRange(pathArr.Select(p => p.ToString()));
            }

            if (guidArr != null)
            {
                foreach (var g in guidArr)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g.ToString());
                    if (!string.IsNullOrEmpty(path))
                        paths.Add(path);
                }
            }

            if (paths.Count == 0)
            {
                return new { success = false, message = "paths or guids array is required" };
            }

            try
            {
                int successCount = 0;
                int failCount = 0;
                var results = new List<object>();

                foreach (string path in paths)
                {
                    var singleParams = new JObject(parameters);
                    singleParams["path"] = path;
                    singleParams.Remove("paths");
                    singleParams.Remove("guids");

                    var result = SetTextureImportSettings(singleParams);
                    var resultDict = result as Dictionary<string, object>;

                    if (resultDict != null && resultDict.ContainsKey("success") && (bool)resultDict["success"])
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                    }

                    results.Add(new { path = path, success = resultDict?["success"] ?? false });
                }

                return new
                {
                    success = true,
                    message = $"Processed {paths.Count} textures: {successCount} succeeded, {failCount} failed",
                    total = paths.Count,
                    succeeded = successCount,
                    failed = failCount,
                    results = results
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error in batch operation: {e.Message}" };
            }
        }

        #endregion

        #region 资产搜索工具

        /// <summary>
        /// 按名称模式搜索贴图
        /// 参数:
        ///   folder - 搜索目录 (默认: "Assets")
        ///   pattern - 名称模式 (支持通配符，如 "*_Normal*", "*BaseColor*")
        /// </summary>
        [ExecuteCustomTool.CustomTool("texture_find_by_name", "按名称模式搜索贴图")]
        internal static object FindTexturesByName(JObject parameters)
        {
            string folder = parameters["folder"]?.ToString() ?? "Assets";
            string pattern = parameters["pattern"]?.ToString();

            if (string.IsNullOrEmpty(pattern))
            {
                return new { success = false, message = "pattern is required" };
            }

            try
            {
                var results = new List<object>();
                string[] guids = AssetDatabase.FindAssets($"{pattern} t:Texture2D", new[] { folder });

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    results.Add(new
                    {
                        name = Path.GetFileNameWithoutExtension(path),
                        path = path,
                        guid = guid
                    });
                }

                return new
                {
                    success = true,
                    folder = folder,
                    pattern = pattern,
                    count = results.Count,
                    textures = results
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error searching textures: {e.Message}" };
            }
        }

        /// <summary>
        /// 按类型搜索资产
        /// 参数:
        ///   folder - 搜索目录
        ///   type - 资产类型 ("Material", "Texture2D", "Model", "Shader", etc.)
        ///   pattern - 名称模式 (可选)
        /// </summary>
        [ExecuteCustomTool.CustomTool("asset_find", "按类型和名称模式搜索资产")]
        internal static object FindAssets(JObject parameters)
        {
            string folder = parameters["folder"]?.ToString() ?? "Assets";
            string type = parameters["type"]?.ToString();
            string pattern = parameters["pattern"]?.ToString();

            if (string.IsNullOrEmpty(type))
            {
                return new { success = false, message = "type is required (e.g. 'Material', 'Texture2D', 'Model')" };
            }

            try
            {
                var results = new List<object>();
                string searchFilter = string.IsNullOrEmpty(pattern) ? $"t:{type}" : $"{pattern} t:{type}";
                string[] guids = AssetDatabase.FindAssets(searchFilter, new[] { folder });

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    results.Add(new
                    {
                        name = Path.GetFileNameWithoutExtension(path),
                        path = path,
                        guid = guid,
                        type = type
                    });
                }

                return new
                {
                    success = true,
                    folder = folder,
                    type = type,
                    pattern = pattern,
                    count = results.Count,
                    assets = results
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error searching assets: {e.Message}" };
            }
        }

        /// <summary>
        /// 获取目录下的子文件夹列表
        /// 参数:
        ///   folder - 父目录路径
        /// </summary>
        [ExecuteCustomTool.CustomTool("asset_get_subfolders", "获取目录下的子文件夹列表")]
        internal static object GetSubfolders(JObject parameters)
        {
            string folder = parameters["folder"]?.ToString() ?? "Assets";

            try
            {
                var folders = new List<object>();
                string[] subFolders = AssetDatabase.GetSubFolders(folder);

                foreach (string sub in subFolders)
                {
                    folders.Add(new
                    {
                        name = Path.GetFileName(sub),
                        path = sub,
                        guid = AssetDatabase.AssetPathToGUID(sub)
                    });
                }

                return new
                {
                    success = true,
                    parentFolder = folder,
                    count = folders.Count,
                    folders = folders
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error getting subfolders: {e.Message}" };
            }
        }

        #endregion

        #region FBX 嵌入材质操作工具

        /// <summary>
        /// 设置 FBX 嵌入材质的 Shader
        /// 参数:
        ///   fbxPath - FBX 文件路径
        ///   fbxGuid - FBX 文件 GUID
        ///   materialName - 材质名称 (可选，默认设置所有嵌入材质)
        ///   shaderPath - Shader 资产路径
        ///   shaderGuid - Shader GUID
        ///   shaderName - Shader 名称
        /// </summary>
        [ExecuteCustomTool.CustomTool("fbx_set_embedded_material_shader", "设置 FBX 嵌入材质的 Shader")]
        internal static object SetFBXEmbeddedMaterialShader(JObject parameters)
        {
            string fbxPath = parameters["fbxPath"]?.ToString();
            string fbxGuid = parameters["fbxGuid"]?.ToString();
            string materialName = parameters["materialName"]?.ToString();
            string shaderPath = parameters["shaderPath"]?.ToString();
            string shaderGuid = parameters["shaderGuid"]?.ToString();
            string shaderName = parameters["shaderName"]?.ToString();

            // 解析 FBX 路径
            if (!string.IsNullOrEmpty(fbxGuid))
            {
                fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuid);
            }

            if (string.IsNullOrEmpty(fbxPath))
            {
                return new { success = false, message = "fbxPath or fbxGuid is required" };
            }

            // 解析 Shader
            Shader shader = null;
            if (!string.IsNullOrEmpty(shaderGuid))
            {
                shaderPath = AssetDatabase.GUIDToAssetPath(shaderGuid);
            }

            if (!string.IsNullOrEmpty(shaderPath))
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            }
            else if (!string.IsNullOrEmpty(shaderName))
            {
                shader = Shader.Find(shaderName);
            }

            if (shader == null)
            {
                return new { success = false, message = "Shader not found. Provide shaderPath, shaderGuid, or shaderName" };
            }

            try
            {
                var results = new List<object>();
                var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
                int updatedCount = 0;

                foreach (var asset in assets)
                {
                    if (asset is Material mat)
                    {
                        if (!string.IsNullOrEmpty(materialName) && mat.name != materialName)
                            continue;

                        string oldShader = mat.shader.name;
                        mat.shader = shader;
                        updatedCount++;

                        results.Add(new
                        {
                            materialName = mat.name,
                            oldShader = oldShader,
                            newShader = shader.name
                        });
                    }
                }

                if (updatedCount > 0)
                {
                    AssetDatabase.SaveAssets();
                }

                return new
                {
                    success = true,
                    message = $"Updated {updatedCount} embedded material(s)",
                    fbxPath = fbxPath,
                    shader = shader.name,
                    updatedCount = updatedCount,
                    materials = results
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error setting embedded material shader: {e.Message}" };
            }
        }

        /// <summary>
        /// 设置 FBX 嵌入材质的贴图
        /// 参数:
        ///   fbxPath - FBX 文件路径
        ///   fbxGuid - FBX 文件 GUID
        ///   materialName - 材质名称 (可选，默认设置所有嵌入材质)
        ///   propertyName - 贴图属性名
        ///   texturePath - 贴图资产路径
        ///   textureGuid - 贴图 GUID
        /// </summary>
        [ExecuteCustomTool.CustomTool("fbx_set_embedded_material_texture", "设置 FBX 嵌入材质的贴图")]
        internal static object SetFBXEmbeddedMaterialTexture(JObject parameters)
        {
            string fbxPath = parameters["fbxPath"]?.ToString();
            string fbxGuid = parameters["fbxGuid"]?.ToString();
            string materialName = parameters["materialName"]?.ToString();
            string propertyName = parameters["propertyName"]?.ToString();
            string texturePath = parameters["texturePath"]?.ToString();
            string textureGuid = parameters["textureGuid"]?.ToString();

            // 解析 FBX 路径
            if (!string.IsNullOrEmpty(fbxGuid))
            {
                fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuid);
            }

            if (string.IsNullOrEmpty(fbxPath))
            {
                return new { success = false, message = "fbxPath or fbxGuid is required" };
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                return new { success = false, message = "propertyName is required" };
            }

            // 解析贴图路径
            if (!string.IsNullOrEmpty(textureGuid))
            {
                texturePath = AssetDatabase.GUIDToAssetPath(textureGuid);
            }

            try
            {
                Texture2D texture = null;
                if (!string.IsNullOrEmpty(texturePath))
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture == null)
                    {
                        return new { success = false, message = $"Texture not found at: {texturePath}" };
                    }
                }

                var results = new List<object>();
                var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
                int updatedCount = 0;

                foreach (var asset in assets)
                {
                    if (asset is Material mat)
                    {
                        if (!string.IsNullOrEmpty(materialName) && mat.name != materialName)
                            continue;

                        if (!mat.HasProperty(propertyName))
                        {
                            results.Add(new { materialName = mat.name, success = false, message = $"Property '{propertyName}' not found" });
                            continue;
                        }

                        mat.SetTexture(propertyName, texture);
                        updatedCount++;

                        results.Add(new
                        {
                            materialName = mat.name,
                            success = true,
                            property = propertyName,
                            texture = texture != null ? texture.name : null
                        });
                    }
                }

                if (updatedCount > 0)
                {
                    AssetDatabase.SaveAssets();
                }

                return new
                {
                    success = true,
                    message = $"Set texture for {updatedCount} embedded material(s)",
                    fbxPath = fbxPath,
                    propertyName = propertyName,
                    texturePath = texturePath,
                    updatedCount = updatedCount,
                    materials = results
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error setting embedded material texture: {e.Message}" };
            }
        }

        /// <summary>
        /// 批量设置 FBX 嵌入材质的贴图 (根据贴图命名规则自动匹配)
        /// 参数:
        ///   fbxPath - FBX 文件路径
        ///   fbxGuid - FBX 文件 GUID
        ///   textureFolder - 贴图所在目录
        ///   textureMappings - 贴图映射规则数组，每项包含 { "textureType": "BaseColor", "propertyName": "_Albedo" }
        /// </summary>
        [ExecuteCustomTool.CustomTool("fbx_auto_assign_textures", "根据命名规则自动为 FBX 嵌入材质分配贴图")]
        internal static object FBXAutoAssignTextures(JObject parameters)
        {
            string fbxPath = parameters["fbxPath"]?.ToString();
            string fbxGuid = parameters["fbxGuid"]?.ToString();
            string textureFolder = parameters["textureFolder"]?.ToString();
            var textureMappings = parameters["textureMappings"] as JArray;

            // 解析 FBX 路径
            if (!string.IsNullOrEmpty(fbxGuid))
            {
                fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuid);
            }

            if (string.IsNullOrEmpty(fbxPath))
            {
                return new { success = false, message = "fbxPath or fbxGuid is required" };
            }

            // 默认贴图映射
            if (textureMappings == null || textureMappings.Count == 0)
            {
                textureMappings = new JArray
                {
                    new JObject { ["textureType"] = "BaseColor", ["propertyName"] = "_Albedo" },
                    new JObject { ["textureType"] = "Normal", ["propertyName"] = "_Normal" },
                    new JObject { ["textureType"] = "AO", ["propertyName"] = "_AO" },
                    new JObject { ["textureType"] = "Cavity", ["propertyName"] = "_Cavity" },
                    new JObject { ["textureType"] = "Roughness", ["propertyName"] = "_Roughness" },
                    new JObject { ["textureType"] = "Gloss", ["propertyName"] = "_Roughness" },
                    new JObject { ["textureType"] = "Specular", ["propertyName"] = "_Specular" },
                    new JObject { ["textureType"] = "Bump", ["propertyName"] = "_Normal" }
                };
            }

            // 如果没有指定贴图目录，使用 FBX 所在目录
            if (string.IsNullOrEmpty(textureFolder))
            {
                textureFolder = Path.GetDirectoryName(fbxPath);
            }

            try
            {
                // 获取贴图
                var textureDict = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
                string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder });

                foreach (string texGuid in texGuids)
                {
                    string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
                    string texName = Path.GetFileNameWithoutExtension(texPath);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    if (tex != null)
                    {
                        textureDict[texName] = tex;
                    }
                }

                // 获取 FBX 名称前缀
                string fbxName = Path.GetFileNameWithoutExtension(fbxPath);

                // 获取嵌入材质
                var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
                var results = new List<object>();
                int totalAssigned = 0;

                foreach (var asset in assets)
                {
                    if (asset is Material mat)
                    {
                        var matResult = new Dictionary<string, object>
                        {
                            ["materialName"] = mat.name,
                            ["textures"] = new Dictionary<string, string>()
                        };

                        foreach (var mapping in textureMappings)
                        {
                            string textureType = mapping["textureType"]?.ToString();
                            string propertyName = mapping["propertyName"]?.ToString();

                            if (string.IsNullOrEmpty(textureType) || string.IsNullOrEmpty(propertyName))
                                continue;

                            if (!mat.HasProperty(propertyName))
                                continue;

                            // 查找匹配的贴图
                            string expectedName = $"{fbxName}_8K_{textureType}";
                            if (textureDict.TryGetValue(expectedName, out Texture2D tex))
                            {
                                mat.SetTexture(propertyName, tex);
                                totalAssigned++;
                                ((Dictionary<string, string>)matResult["textures"])[propertyName] = tex.name;
                            }
                        }

                        results.Add(matResult);
                    }
                }

                if (totalAssigned > 0)
                {
                    AssetDatabase.SaveAssets();
                }

                return new
                {
                    success = true,
                    message = $"Assigned {totalAssigned} texture(s)",
                    fbxPath = fbxPath,
                    textureFolder = textureFolder,
                    totalAssigned = totalAssigned,
                    materials = results
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error auto-assigning textures: {e.Message}" };
            }
        }

        /// <summary>
        /// 设置 FBX 使用外部材质
        /// 参数:
        ///   fbxPath - FBX 文件路径
        ///   fbxGuid - FBX 文件 GUID
        ///   materialMappings - 材质映射数组，每项包含 { "embeddedName": "MatID_1", "externalMaterialPath": "Assets/.../Material.mat" } 或 { "embeddedName": "MatID_1", "externalMaterialGuid": "..." }
        ///   searchMode - 材质搜索模式 ("Local", "Recursive", "Everywhere")
        /// </summary>
        [ExecuteCustomTool.CustomTool("fbx_set_external_materials", "设置 FBX 使用外部材质")]
        internal static object SetFBXExternalMaterials(JObject parameters)
        {
            string fbxPath = parameters["fbxPath"]?.ToString();
            string fbxGuid = parameters["fbxGuid"]?.ToString();
            var materialMappings = parameters["materialMappings"] as JArray;
            string searchMode = parameters["searchMode"]?.ToString() ?? "Local";

            // 解析 FBX 路径
            if (!string.IsNullOrEmpty(fbxGuid))
            {
                fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuid);
            }

            if (string.IsNullOrEmpty(fbxPath))
            {
                return new { success = false, message = "fbxPath or fbxGuid is required" };
            }

            try
            {
                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    return new { success = false, message = $"Model importer not found for: {fbxPath}" };
                }

                // 设置材质搜索模式
                if (!string.IsNullOrEmpty(searchMode))
                {
                    switch (searchMode.ToLower())
                    {
                        case "local":
                            importer.materialSearch = ModelImporterMaterialSearch.Local;
                            break;
                        case "recursive":
                            importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
                            break;
                        case "everywhere":
                            importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
                            break;
                    }
                }

                var results = new List<object>();
                int successCount = 0;

                // 设置材质映射
                if (materialMappings != null && materialMappings.Count > 0)
                {
                    foreach (var mapping in materialMappings)
                    {
                        string embeddedName = mapping["embeddedName"]?.ToString();
                        string externalPath = mapping["externalMaterialPath"]?.ToString();
                        string externalGuid = mapping["externalMaterialGuid"]?.ToString();

                        if (string.IsNullOrEmpty(embeddedName))
                            continue;

                        // 解析外部材质路径
                        if (!string.IsNullOrEmpty(externalGuid))
                        {
                            externalPath = AssetDatabase.GUIDToAssetPath(externalGuid);
                        }

                        if (string.IsNullOrEmpty(externalPath))
                            continue;

                        Material externalMat = AssetDatabase.LoadAssetAtPath<Material>(externalPath);
                        if (externalMat == null)
                        {
                            results.Add(new { embeddedName = embeddedName, success = false, message = $"Material not found: {externalPath}" });
                            continue;
                        }

                        // 查找嵌入材质
                        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
                        foreach (var asset in assets)
                        {
                            if (asset is Material embeddedMat && embeddedMat.name == embeddedName)
                            {
                                // 设置外部材质映射
                                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embeddedMat), externalMat);
                                results.Add(new { embeddedName = embeddedName, externalMaterial = externalPath, success = true });
                                successCount++;
                                break;
                            }
                        }
                    }
                }

                // 只有在有成功映射时才保存
                if (successCount > 0)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }

                return new
                {
                    success = true,
                    message = $"Set external materials for {fbxPath}",
                    fbxPath = fbxPath,
                    searchMode = searchMode,
                    mappings = results
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error setting external materials: {e.Message}" };
            }
        }

        /// <summary>
        /// 自动将 FBX 嵌入材质映射到提取的外部材质
        /// 参数:
        ///   fbxPath - FBX 文件路径
        ///   fbxGuid - FBX 文件 GUID
        ///   materialsFolder - 外部材质所在目录
        /// </summary>
        [ExecuteCustomTool.CustomTool("fbx_auto_link_materials", "自动将 FBX 嵌入材质映射到提取的外部材质")]
        internal static object FBXAutoLinkMaterials(JObject parameters)
        {
            string fbxPath = parameters["fbxPath"]?.ToString();
            string fbxGuid = parameters["fbxGuid"]?.ToString();
            string materialsFolder = parameters["materialsFolder"]?.ToString();

            // 解析 FBX 路径
            if (!string.IsNullOrEmpty(fbxGuid))
            {
                fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuid);
            }

            if (string.IsNullOrEmpty(fbxPath))
            {
                return new { success = false, message = "fbxPath or fbxGuid is required" };
            }

            try
            {
                // 如果没有指定材质目录，使用 FBX 同目录下的 Materials 文件夹
                if (string.IsNullOrEmpty(materialsFolder))
                {
                    materialsFolder = Path.Combine(Path.GetDirectoryName(fbxPath), "Materials");
                }

                string fbxName = Path.GetFileNameWithoutExtension(fbxPath);

                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    return new { success = false, message = $"Model importer not found for: {fbxPath}" };
                }

                // 设置材质搜索模式为 Local
                importer.materialSearch = ModelImporterMaterialSearch.Local;

                var results = new List<object>();

                // 获取嵌入材质
                var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
                foreach (var asset in assets)
                {
                    if (asset is Material embeddedMat)
                    {
                        string embeddedName = embeddedMat.name;
                        
                        // 查找对应的外部材质 (使用 FBX 名称作为前缀)
                        string expectedMaterialName = $"{fbxName}_{embeddedName}";
                        string expectedPath = $"{materialsFolder}/{expectedMaterialName}.mat";

                        Material externalMat = AssetDatabase.LoadAssetAtPath<Material>(expectedPath);
                        if (externalMat != null)
                        {
                            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embeddedMat), externalMat);
                            results.Add(new { embeddedName = embeddedName, externalMaterial = expectedPath, success = true });
                        }
                        else
                        {
                            // 尝试不带前缀的名称
                            expectedPath = $"{materialsFolder}/{embeddedName}.mat";
                            externalMat = AssetDatabase.LoadAssetAtPath<Material>(expectedPath);
                            if (externalMat != null)
                            {
                                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embeddedMat), externalMat);
                                results.Add(new { embeddedName = embeddedName, externalMaterial = expectedPath, success = true });
                            }
                            else
                            {
                                results.Add(new { embeddedName = embeddedName, success = false, message = $"External material not found: {expectedPath}" });
                            }
                        }
                    }
                }

                // 只有在有映射变化时才保存
                if (results.Count > 0)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }

                return new
                {
                    success = true,
                    message = $"Linked {results.Count(r => ((dynamic)r).success)} materials",
                    fbxPath = fbxPath,
                    materialsFolder = materialsFolder,
                    mappings = results
                };
            }
            catch (Exception e)
            {
                return new { success = false, message = $"Error auto-linking materials: {e.Message}" };
            }
        }

        #endregion
    }
}