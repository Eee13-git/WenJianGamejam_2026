using UnityEngine;

/// <summary>
/// 溶酶体 Buff 效果 — OnApply 打开道具移除面板，面板关闭后移除 Buff。
/// 溶酶体不进入背包，OnRemove 无需清理道具。
/// </summary>
[CreateAssetMenu(fileName = "ItemRemoverBuffEffect", menuName = "Game/Buff Effect/Item Remover")]
public class ItemRemoverBuffEffect : BuffEffectBase
{
    [SerializeField] private int _removeCount = 2;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        var itemManager = target.GetComponent<ItemManager>();
        var panel = FindObjectOfType<ItemRemoverPanel>(true);
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
