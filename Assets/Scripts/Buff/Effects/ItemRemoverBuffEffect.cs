using UnityEngine;

/// <summary>
/// 溶酶体 Buff 效果 — OnApply 打开道具移除面板，面板关闭后移除 Buff。
/// 溶酶体不进入背包，OnRemove 无需清理道具。
/// </summary>
[CreateAssetMenu(fileName = "ItemRemoverBuffEffect", menuName = "Game/Buff Effect/Item Remover")]
public class ItemRemoverBuffEffect : BuffEffectBase
{
    [SerializeField] private int _removeCount = 2;

    [Header("面板预制体")]
    [Tooltip("找不到场景实例时，从该 prefab 实例化到根 Canvas 下")]
    [SerializeField] private ItemRemoverPanel _panelPrefab;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var itemManager = target.GetComponent<ItemManager>();
        var panel = FindObjectOfType<ItemRemoverPanel>(true);

        // 独立 prefab 懒加载：场景中没有面板实例时，从 prefab 实例化。
        // prefab 根自带 Canvas(GraphicRaycaster)，实例化为独立根画布（SetParent(null)），
        // 避免被挂到其他根 Canvas（如 [LayerAnnouncement]，其 CanvasGroup alpha=0 且无 GraphicRaycaster）导致不可见/不可点。
        if (panel == null && _panelPrefab != null)
        {
            panel = Instantiate(_panelPrefab);
            panel.name = "ItemRemoverPanel";
            panel.transform.SetParent(null);   // 提升为独立根画布，sortingOrder 生效
        }

        if (itemManager == null || panel == null || panel.IsVisible) return;

        panel.Show(itemManager, _removeCount, () => OnPanelClosed(target, buff));
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        // 溶酶体从未进入背包，无需移除
    }

    private void OnPanelClosed(GameObject target, BuffInstance buff)
    {
        var buffManager = target.GetComponent<BuffManager>();
        if (buffManager != null)
            buffManager.RemoveBuff(buff);
    }
}
