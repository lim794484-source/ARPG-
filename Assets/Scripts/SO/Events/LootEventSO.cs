using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LootEventSO", menuName = "Events/LootEventSO", order = 0)]
public class LootEventSO : ScriptableObject
{
    public event Action<ItemSO, int, Loot> LootEvent;

    public void OnEventRaised(ItemSO item, int quantity, Loot loot)
    {
        LootEvent?.Invoke(item, quantity, loot);
    }
}
