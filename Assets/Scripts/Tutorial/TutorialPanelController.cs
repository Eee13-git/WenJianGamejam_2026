using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 操作手册面板 — Start 菜单"操作说明"按钮打开的图文说明面板。
/// 半透明面板 + 可滚动文字，汇总全部游戏机制。
/// 由 StartMenuController 调用 Open/Close。
/// </summary>
public class TutorialPanelController : MonoBehaviour
{
    [Header("面板引用")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _contentText;
    [SerializeField] private Button _closeButton;

    private static TutorialPanelController _instance;

    public static void Toggle()
    {
        if (_instance == null)
        {
            // 面板初始隐藏（inactive），须包含 inactive 对象查找
            var found = Object.FindObjectOfType<TutorialPanelController>(true);
            if (found == null)
            {
                Debug.LogWarning("TutorialPanelController 未找到（Start 场景需配置）");
                return;
            }
            _instance = found;
        }
        _instance.SetVisible(!_instance._panel.activeSelf);
    }

    private void Awake()
    {
        if (_instance == null)
            _instance = this;

        if (_closeButton != null)
            _closeButton.onClick.AddListener(() => SetVisible(false));

        // 填充手册内容
        if (_contentText != null && string.IsNullOrEmpty(_contentText.text))
            _contentText.text = BuildManualText();

        // 注意：不在 Awake 中隐藏面板——面板初始 inactive，由 SetVisible 控制；
        // 若此处 SetActive(false) 会在每次 SetVisible(true) 激活时被 Awake 再次隐藏（开关反相）
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public void SetVisible(bool visible)
    {
        if (_panel != null)
            _panel.SetActive(visible);
    }

    /// <summary>手册内容（操作 + 核心机制说明）</summary>
    public static string BuildManualText()
    {
        return
            "【操作说明】\n" +
            "WASD / 方向键 —— 移动\n" +
            "鼠标 —— 瞄准\n" +
            "鼠标左键 —— 普通攻击\n" +
            "Q / E / Z / X —— 技能1~4\n" +
            "数字键 1~0 —— 扩展技能槽（随技能槽解锁）\n" +
            "Esc —— 暂停菜单\n\n" +
            "【核心机制】\n" +
            "· 探索：击败每个房间的所有敌人后房门开启，寻找通往下一层的出口。\n" +
            "· 侵蚀技能：侵蚀弹命中敌人后二选一——\n" +
            "   吞噬：夺取敌人技能（可装备/升级到自己的技能槽）\n" +
            "   同化：将敌人收为随从（Boss 不可同化）\n" +
            "· 进化倾向：吞噬提升「宿主」倾向（金色），同化提升「独特」倾向（紫色）。\n" +
            "   倾向影响技能伤害与随从属性。\n" +
            "· 随从：同化的随从会跟随作战，同种可升级，异种可替换。\n" +
            "· 道具：房间内拾取道具获得被动效果，商店可购买。\n" +
            "· 图鉴：击杀/同化/吞噬过的敌人与技能会记录在左上角图鉴中。\n" +
            "· Boss：击败每层 Boss 开启下一层出口。\n" +
            "· 难度：越深层的楼层敌人越强、种类越多。";
    }
}
