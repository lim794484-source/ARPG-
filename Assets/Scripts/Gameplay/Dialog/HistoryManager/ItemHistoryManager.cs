using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class ItemHistoryManager : YSingleton<ItemHistoryManager>
{

    private Dictionary<ItemSO, int> itemHasPicked = new();

    public void ClearHistory()
    {
        itemHasPicked.Clear();
    }

    public void RecordItem(ItemSO item, int quantity)
    {
        if (item == null || quantity == 0) return;

        itemHasPicked.TryGetValue(item, out int currentAmount);
        int newAmount = Mathf.Max(0, currentAmount + quantity);
        if (newAmount == 0)
        {
            itemHasPicked.Remove(item);
        }
        else
        {
            itemHasPicked[item] = newAmount;
        }
    }

    public bool HasPickedOverAmount(ItemSO item, int amount)//实际上会记录是否捡过和数量，当前数量归零的并不会删除
    {
        if (itemHasPicked.ContainsKey(item) && itemHasPicked[item] >= amount)
            return true;

        return false;
    }
    public int GetItemQuantity(ItemSO item)
    {
        if (itemHasPicked.TryGetValue(item, out int amount))
            return amount;
        return 0;
    }
}
