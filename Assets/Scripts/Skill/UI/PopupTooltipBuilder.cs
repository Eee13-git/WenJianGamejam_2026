using UnityEngine;
using TMPro;

/// <summary>
/// 弹窗悬停 tooltip 构建器 —— 运行时动态创建半透明信息面板（名称 + 描述）。
/// 各弹窗（技能夺取/装配槽/随从槽）在 Awake 中调用，避免在 prefab 中手工搭建 UI。
/// 所有 Graphic 的 raycastTarget=false，防止悬浮层抢走下层槽位的 raycast 导致闪烁。
/// </summary>
public static class PopupTooltipBuilder
{
    /// <summary>在 parent 下创建 SlotTooltip，返回根 GameObject，输出 Name/Desc 文本组件。</summary>
    public static GameObject Create(Transform parent, out TMP_Text nameText, out TMP_Text descText)
    {
        nameText = null;
        descText = null;

        var tooltipGo = new GameObject("SlotTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        tooltipGo.transform.SetParent(parent, false);
        tooltipGo.transform.SetAsLastSibling();
        var rt = tooltipGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(340f, 160f);
        rt.anchoredPosition = new Vector2(200f, -100f);

        var bg = tooltipGo.GetComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        bg.raycastTarget = false;

        var font = TMP_Settings.defaultFontAsset;

        // Name（顶部横条）
        var nameGo = CreateText(tooltipGo.transform, "Name", font, 22f, Color.white,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -40f), new Vector2(-16f, -10f), new Vector2(0.5f, 1f));
        nameText = nameGo.GetComponent<TMP_Text>();

        // Desc（主体，自动换行）
        var descGo = CreateText(tooltipGo.transform, "Desc", font, 16f, new Color(0.85f, 0.85f, 0.85f),
            TextAlignmentOptions.TopLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 12f), new Vector2(-16f, -48f), new Vector2(0.5f, 0.5f));
        descGo.GetComponent<TMP_Text>().enableWordWrapping = true;
        descText = descGo.GetComponent<TMP_Text>();

        tooltipGo.SetActive(false);
        return tooltipGo;
    }

    private static GameObject CreateText(Transform parent, string name, TMP_FontAsset font,
        float fontSize, Color color, TextAlignmentOptions align,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.text = "";
        return go;
    }
}
