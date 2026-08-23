using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 脓毒疮复合体 boss 动画构建：
/// - idle.anim（单帧待机，8fps loop）
/// - cast.anim（施法 10 帧，8fps 非循环）
/// - AnimatorController（Idle + Cast 状态，IsCasting bool 参数，AnyState→Cast 条件，Cast→Idle exitTime 0.9）
/// - 技能图标（Resources/Skills/Data/脓疮洪流/Icon.png）
/// </summary>
public static class BuildPustuleBossAnim
{
    private static string TexDir = "Assets/Textures/Enemy1.1/脓毒疮复合体boss/";
    private static string AnimDir = "Assets/Animations/Enemy/脓毒疮复合体boss/";
    private static string IconDir = "Assets/Resources/Skills/Data/脓疮洪流/";
    private const string BossName = "脓毒疮复合体boss";

    public static void Run()
    {
        BuildIdle();
        BuildCast();
        BuildController();
        BuildIcon();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildPustuleBossAnim] done");
    }

    private static Sprite LoadSprite(string sheet, int index)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(TexDir + sheet);
        foreach (var o in all)
            if (o is Sprite s && s.name.EndsWith($"_{index}")) return s;
        foreach (var o in all)
            if (o is Sprite s) return s;
        return null;
    }

    private static Sprite LoadNamedSprite(string sheet, string name)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(TexDir + sheet))
            if (o is Sprite s && s.name == name) return s;
        return null;
    }

    private static AnimationClip CreateClip(string clipName, Sprite[] frames, float fps, bool loop)
    {
        var clip = new AnimationClip();
        clip.name = clipName;
        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / fps,
                value = frames[i]
            };
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void SaveClip(AnimationClip clip)
    {
        if (!Directory.Exists(AnimDir)) Directory.CreateDirectory(AnimDir);
        string path = AnimDir + BossName + "_" + clip.name + ".anim";
        // 删除旧资产避免同名冲突
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        Debug.Log("[BuildPustuleBossAnim] saved " + path);
    }

    private static void BuildIdle()
    {
        var sprite = LoadNamedSprite("脓毒疮复合体boss_idle.png", "脓毒疮复合体boss_idle");
        var clip = CreateClip("idle", new[] { sprite }, 8f, true);
        SaveClip(clip);
    }

    private static void BuildCast()
    {
        var frames = new List<Sprite>();
        for (int i = 0; i < 10; i++)
            frames.Add(LoadSprite("脓毒疮复合体施法_frames.png", i));
        var clip = CreateClip("cast", frames.ToArray(), 8f, false);
        SaveClip(clip);
    }

    private static void BuildController()
    {
        if (!Directory.Exists(AnimDir)) Directory.CreateDirectory(AnimDir);
        string path = AnimDir + BossName + "_Controller.controller";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var root = controller.layers[0].stateMachine;

        // 参数
        controller.AddParameter("IsCasting", AnimatorControllerParameterType.Bool);

        // Idle 状态（默认）
        var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + BossName + "_idle.anim");
        var idleState = root.AddState("Idle", new Vector3(250, 0, 0));
        idleState.motion = idleClip;

        // Cast 状态
        var castClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + BossName + "_cast.anim");
        var castState = root.AddState("Cast", new Vector3(250, 80, 0));
        castState.motion = castClip;

        // AnyState → Cast：IsCasting == true
        var anyToCast = root.AddAnyStateTransition(castState);
        anyToCast.hasExitTime = false;
        anyToCast.duration = 0f;
        anyToCast.AddCondition(AnimatorConditionMode.If, 0f, "IsCasting");

        // Cast → Idle：exitTime 0.9
        var castToIdle = castState.AddTransition(idleState);
        castToIdle.hasExitTime = true;
        castToIdle.exitTime = 0.9f;
        castToIdle.duration = 0.1f;

        root.defaultState = idleState;
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildPustuleBossAnim] controller saved " + path);
    }

    private static void BuildIcon()
    {
        if (!Directory.Exists(IconDir)) Directory.CreateDirectory(IconDir);
        // 从 boss idle 原图内容生成 256x256 图标（透明底）
        var bytes = File.ReadAllBytes(TexDir + "脓毒疮复合体boss.png");
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(bytes);

        // 精确内容边界
        var px = src.GetPixels32();
        int w = src.width, h = src.height;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 20)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        int size = 256, pad = 24;
        float scale = Mathf.Min((float)(size - pad * 2) / cw, (float)(size - pad * 2) / ch);
        int tw = Mathf.Max(1, Mathf.RoundToInt(cw * scale));
        int th = Mathf.Max(1, Mathf.RoundToInt(ch * scale));
        var icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var clear = new Color32[size * size];
        for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(0, 0, 0, 0);
        icon.SetPixels32(clear);
        int ox = (size - tw) / 2, oy = (size - th) / 2;
        for (int y = 0; y < th; y++)
        {
            int sy = minY + y * ch / th;
            for (int x = 0; x < tw; x++)
            {
                int sx = minX + x * cw / tw;
                icon.SetPixel(ox + x, oy + y, px[sy * w + sx]);
            }
        }
        icon.Apply();
        File.WriteAllBytes(IconDir + "Icon.png", icon.EncodeToPNG());
        Object.DestroyImmediate(icon);
        Object.DestroyImmediate(src);
        AssetDatabase.ImportAsset(IconDir + "Icon.png", ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(IconDir + "Icon.png");
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.filterMode = FilterMode.Point;
        imp.maxTextureSize = 256;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.spritePixelsPerUnit = 100;
        AssetDatabase.WriteImportSettingsIfDirty(IconDir + "Icon.png");
        AssetDatabase.ImportAsset(IconDir + "Icon.png", ImportAssetOptions.ForceUpdate);
        Debug.Log("[BuildPustuleBossAnim] icon saved " + IconDir + "Icon.png");
    }
}
