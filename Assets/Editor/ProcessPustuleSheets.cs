using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 脓毒疮复合体 boss 帧动画素材处理（Color32 版，修复 GetPixels float-alpha 阈值 bug）：
/// 1) 脓毒疮复合体施法.png → 10 帧 → 归一化 5x2 spritesheet（800x480 单元，内容居中）
/// 2) 脓疮复合体洪流.png → 4 宽帧 → 归一化 2x2 spritesheet（2048x512 单元，内容靠左垂直居中，pivot (0,0.5)）
/// 3) 脓毒疮复合体boss.png → 单帧待机 spritesheet（800x480 单元居中）
/// </summary>
public static class ProcessPustuleSheets
{
    private static string SrcDir = "Assets/Textures/Enemy1.1/脓毒疮复合体boss/";

    // 施法 10 帧内容边界（连通域分析结果，x/y/w/h）
    private static readonly int[,] CastFrames = new int[,]
    {
        { 132, 2796, 678, 300 },  // f0 待机姿态
        { 258, 3366, 546, 300 },  // f1 蓄力收缩
        { 1296, 3348, 456, 330 }, // f2
        { 2196, 3366, 432, 330 }, // f3
        { 3132, 3366, 456, 330 }, // f4 蓄力顶点
        { 3792, 3384, 546, 300 }, // f5 开始释放
        { 120, 3912, 678, 300 },  // f6 膨胀
        { 1056, 3918, 684, 282 }, // f7
        { 1920, 3930, 696, 276 }, // f8
        { 2784, 3954, 768, 234 }, // f9 释放完成
    };

    // 洪流 4 宽帧内容边界
    private static readonly int[,] TorrentFrames = new int[,]
    {
        { 480, 2808, 1878, 216 },
        { 468, 3162, 2028, 282 },
        { 480, 3594, 2034, 294 },
        { 474, 4026, 2028, 282 },
    };

    public static void Run()
    {
        ProcessCast();
        ProcessTorrent();
        ProcessIdle();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ProcessPustuleSheets] done");
    }

    /// <summary>加载源图并返回整图 Color32 数组（GetPixels32 可靠）</summary>
    private static (Texture2D tex, Color32[] px) LoadSource(string fileName)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), SrcDir, fileName));
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);
        return (tex, tex.GetPixels32());
    }

    /// <summary>扫描区域内精确内容边界（byte alpha > 20）</summary>
    private static Rect ScanContentBounds(Color32[] px, int texW, int texH, Rect region)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(region.x), 0, texW - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(region.y), 0, texH - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(region.xMax), x0 + 1, texW);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(region.yMax), y0 + 1, texH);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (px[y * texW + x].a > 20)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX < 0) return new Rect(x0, y0, 1, 1);
        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>把 src 内容绘制到 canvas 中（anchorX: 0=左对齐, 0.5=居中；垂直居中）</summary>
    private static void DrawContent(Color32[] srcPx, int srcW, Rect content,
                                    Color32[] canvasPx, int canvasW, int cellW, int cellH,
                                    int cellX, int cellY, float anchorX)
    {
        int cw = Mathf.RoundToInt(content.width), ch = Mathf.RoundToInt(content.height);
        int ox = Mathf.RoundToInt((cellW - cw) * anchorX);
        int oy = (cellH - ch) / 2;
        int sx = Mathf.RoundToInt(content.x), sy = Mathf.RoundToInt(content.y);
        for (int y = 0; y < ch; y++)
        {
            for (int x = 0; x < cw; x++)
            {
                var c = srcPx[(sy + y) * srcW + (sx + x)];
                if (c.a > 5)
                    canvasPx[(cellY + oy + y) * canvasW + (cellX + ox + x)] = c;
            }
        }
    }

    private static void ExportAndConfigure(string fileName, Texture2D tex, SpriteMetaData[] metas, float ppu)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), SrcDir, fileName);
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SrcDir + fileName, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(SrcDir + fileName);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Point;
        importer.maxTextureSize = 8192;
        importer.isReadable = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var st = importer.GetPlatformTextureSettings("Standalone");
        st.overridden = true;
        st.textureCompression = TextureImporterCompression.Uncompressed;
        st.maxTextureSize = 8192;
        importer.SetPlatformTextureSettings(st);
        var stDefault = importer.GetPlatformTextureSettings("DefaultTexturePlatform");
        stDefault.overridden = true;
        stDefault.textureCompression = TextureImporterCompression.Uncompressed;
        stDefault.maxTextureSize = 8192;
        importer.SetPlatformTextureSettings(stDefault);
#pragma warning disable CS0618 // TextureImporter.spritesheet 虽标记废弃，但项目实测 WriteImportSettingsIfDirty+ForceUpdate 流程可靠
        importer.spritesheet = metas;
        // 关键：WriteImportSettingsIfDirty + ForceUpdate 两步走
        AssetDatabase.WriteImportSettingsIfDirty(SrcDir + fileName);
        AssetDatabase.ImportAsset(SrcDir + fileName, ImportAssetOptions.ForceUpdate);
#pragma warning restore CS0618
        Debug.Log($"[ProcessPustuleSheets] configured {fileName} ({metas.Length} sprites, PPU={ppu})");
    }

    // ── 1) 施法 10 帧 → 5x2 sheet ──
    private static void ProcessCast()
    {
        var (src, px) = LoadSource("脓毒疮复合体施法.png");
        int srcW = src.width, srcH = src.height;
        const int cellW = 800, cellH = 480, cols = 5, rows = 2;
        int W = cellW * cols, H = cellH * rows;
        var canvas = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var canvasPx = new Color32[W * H];

        var metas = new List<SpriteMetaData>();
        for (int i = 0; i < 10; i++)
        {
            var region = new Rect(CastFrames[i, 0], CastFrames[i, 1], CastFrames[i, 2], CastFrames[i, 3]);
            var content = ScanContentBounds(px, srcW, srcH, region);
            int col = i % cols, row = i / cols;
            int cellX = col * cellW, cellY = (rows - 1 - row) * cellH;
            DrawContent(px, srcW, content, canvasPx, W, cellW, cellH, cellX, cellY, 0.5f);
            metas.Add(new SpriteMetaData
            {
                name = $"脓毒疮复合体施法_{i}",
                rect = new Rect(cellX, cellY, cellW, cellH),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            });
        }
        canvas.SetPixels32(canvasPx);
        canvas.Apply();
        ExportAndConfigure("脓毒疮复合体施法_frames.png", canvas, metas.ToArray(), 160f);
        Object.DestroyImmediate(src);
    }

    // ── 2) 洪流 4 宽帧 → 2x2 sheet（内容靠左，pivot (0,0.5)）──
    private static void ProcessTorrent()
    {
        var (src, px) = LoadSource("脓疮复合体洪流.png");
        int srcW = src.width, srcH = src.height;
        const int cellW = 2048, cellH = 512, cols = 2, rows = 2;
        int W = cellW * cols, H = cellH * rows;
        var canvas = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var canvasPx = new Color32[W * H];

        var metas = new List<SpriteMetaData>();
        for (int i = 0; i < 4; i++)
        {
            var region = new Rect(TorrentFrames[i, 0], TorrentFrames[i, 1], TorrentFrames[i, 2], TorrentFrames[i, 3]);
            var content = ScanContentBounds(px, srcW, srcH, region);
            int col = i % cols, row = i / cols;
            int cellX = col * cellW, cellY = (rows - 1 - row) * cellH;
            DrawContent(px, srcW, content, canvasPx, W, cellW, cellH, cellX, cellY, 0f);
            metas.Add(new SpriteMetaData
            {
                name = $"脓疮复合体洪流_{i}",
                rect = new Rect(cellX, cellY, cellW, cellH),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(0f, 0.5f)
            });
        }
        canvas.SetPixels32(canvasPx);
        canvas.Apply();
        ExportAndConfigure("脓疮复合体洪流_frames.png", canvas, metas.ToArray(), 204.8f);
        Object.DestroyImmediate(src);
    }

    // ── 3) Boss 待机单帧 → 800x480 ──
    private static void ProcessIdle()
    {
        var (src, px) = LoadSource("脓毒疮复合体boss.png");
        int srcW = src.width, srcH = src.height;
        const int cellW = 800, cellH = 480;
        var canvas = new Texture2D(cellW, cellH, TextureFormat.RGBA32, false);
        var canvasPx = new Color32[cellW * cellH];

        var content = ScanContentBounds(px, srcW, srcH, new Rect(0, 0, srcW, srcH));
        DrawContent(px, srcW, content, canvasPx, cellW, cellW, cellH, 0, 0, 0.5f);
        var metas = new[]
        {
            new SpriteMetaData
            {
                name = "脓毒疮复合体boss_idle",
                rect = new Rect(0, 0, cellW, cellH),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }
        };
        canvas.SetPixels32(canvasPx);
        canvas.Apply();
        ExportAndConfigure("脓毒疮复合体boss_idle.png", canvas, metas, 160f);
        Object.DestroyImmediate(src);
    }
}
