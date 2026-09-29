using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
public class InventoryManager : YSingleton<InventoryManager>, ISaveable
{

    [SerializeField] private Transform hotbarParent;
    [SerializeField] private Transform backpackParent;
    private List<InventorySlot> inventorySlotsList = new();
    [SerializeField] private UseItem useItem;
    [SerializeField] private TMP_Text goldAmountText;
    [SerializeField] private GameObject lootPrefab;
    [SerializeField] private Transform player;
    [Tooltip("用于把存档中的稳定物品键还原成 ItemSO。必须包含所有可能进入背包的物品。")]
    [SerializeField] private ItemSO[] knownItems;

    public int GoldAmount => goldAmount;
    private int goldAmount;


    [Header("Events")]
    [SerializeField] private InventorySlotsStatsSO ShoppingRequest;
    [SerializeField] private InventorySlotsStatsSO QuestRewardRequest;

    [SerializeField] private LootEventSO lootEvent;


    private InventorySlot slotBeenClicked;
    private bool slotsInitialized;


    private void Start()
    {
        EnsureSlotsInitialized();
    }
    private void OnEnable()
    {
        lootEvent.LootEvent += OnItemLootedHandler;
        ShoppingRequest.InventoryUpdateRequestEvent += HandleShopping;
        QuestRewardRequest.InventoryUpdateRequestEvent += HandleQuestReward;

        if (DataManager.Instance != null)
        {
            ((ISaveable)this).RegisterSaveable();
        }

    }


    private void OnDisable()
    {
        lootEvent.LootEvent -= OnItemLootedHandler;
        ShoppingRequest.InventoryUpdateRequestEvent -= HandleShopping;
        QuestRewardRequest.InventoryUpdateRequestEvent -= HandleQuestReward;

        if (DataManager.Instance != null)
        {
            ((ISaveable)this).UnRegisterSaveable();
        }

    }

    private void HandleQuestReward(ItemSO item, int price, int amount)
    {
        UpdateInventorySlots(item, amount);
    }

    private void OnItemLootedHandler(ItemSO item, int quantity, Loot lootObj)
    {
        UpdateInventorySlots(item, quantity, lootObj);
    }
    private void HandleShopping(ItemSO item, int price, int amount)
    {
        if (amount > 0)//购买
        {
            TryPurchaseItem(item, price, amount);
        }
        else if (amount < 0)//出售
        {
            TrySellSelectedItem(item, price, -amount);
        }
    }

    /// <summary>
    /// 原子购买：只有完整数量成功放入背包后才扣钱。
    /// stackableSize 表示“单格堆叠上限”，超过后会进入下一空格。
    /// </summary>
    public bool TryPurchaseItem(ItemSO item, int price, int amount = 1)
    {
        if (item == null || amount <= 0 || price < 0 || goldAmount < price)
        {
            return false;
        }

        bool received;
        if (item.isEXP)
        {
            received = ExpManager.Instance != null;
            if (received) ExpManager.Instance.GainExp(amount);
        }
        else if (item.isGold)
        {
            // 商店出售货币本身会让价格语义混乱，配置错误时直接拒绝。
            Debug.LogWarning("商店不能购买金币类物品。");
            received = false;
        }
        else
        {
            received = TryAddExact(item, amount);
        }

        if (!received) return false;

        UpdateGold(price);
        return true;
    }

    private bool TrySellSelectedItem(ItemSO item, int price, int amount)
    {
        if (item == null || amount <= 0 || price > 0 || slotBeenClicked == null
            || slotBeenClicked.ItemSO != item || slotBeenClicked.Quantity < amount)
        {
            return false;
        }

        int removed = slotBeenClicked.RemoveItem(amount);
        if (removed != amount) return false;

        RecordItemHistory(item, -removed);
        UpdateGold(price);
        return true;
    }


    private void UpdateInventorySlots(ItemSO item, int quantity, Loot lootObj = null)
    {
        if (item == null || quantity == 0) return;
        EnsureSlotsInitialized();

        //金币
        if (item.isGold)
        {
            goldAmount += quantity;
            RecordItemHistory(item, quantity);

            RefreshGoldUI();
            lootObj?.MarkAsDisable();
            return;
        }
        if (item.isEXP)
        {
            ExpManager.Instance.GainExp(quantity);
            return;
        }
        //普通物品
        if (quantity < 0)//物品出售
        {
            if (slotBeenClicked == null)
            {
                Debug.Log("No slot been Marked");
            }
            else if (slotBeenClicked.Quantity > 0)
            {
                int removed = slotBeenClicked.RemoveItem(-quantity);
                RecordItemHistory(item, -removed);
                return;
            }
        }
        else if (quantity > 0)//物品拾取以及购买
        {
            foreach (InventorySlot slot in inventorySlotsList)
            {
                if (slot.IsEmpty || slot.ItemSO == item)//空格子 或 可堆叠格子
                {
                    int placed = slot.AddItem(item, quantity);
                    if (placed > 0)
                    {
                        RecordItemHistory(item, placed);
                        quantity -= placed;
                    }

                    if (quantity <= 0)
                    {
                        lootObj?.MarkAsDisable();
                        return;
                    }
                }
            }

            if (quantity > 0)
                DropLoot(item, quantity, lootObj);//减剩下的quantity丢掉
        }

    }
    private void EnsureSlotsInitialized()
    {
        if (slotsInitialized) return;

        inventorySlotsList.Clear();
        AddSlotsFromParent(hotbarParent);
        AddSlotsFromParent(backpackParent);

        foreach (InventorySlot slot in inventorySlotsList)
        {
            if (slot != null) slot.UpdateUI();
        }

        slotsInitialized = true;
    }

    private void AddSlotsFromParent(Transform parent)
    {
        if (parent == null) return;
        foreach (InventorySlot slot in parent.GetComponentsInChildren<InventorySlot>(true))
        {
            if (slot != null && !inventorySlotsList.Contains(slot))
            {
                inventorySlotsList.Add(slot);
            }
        }
    }

    private int GetSpaceForItem(ItemSO item)
    {
        if (item == null || item.stackableSize <= 0) return 0;
        EnsureSlotsInitialized();

        int space = 0;
        foreach (var slot in inventorySlotsList)
        {
            if (slot != null) space += Mathf.Max(0, slot.SpaceRemaining(item));
        }
        return space;
    }

    private bool TryAddExact(ItemSO item, int amount)
    {
        if (item == null || amount <= 0 || GetSpaceForItem(item) < amount)
        {
            return false;
        }

        int remaining = amount;
        foreach (InventorySlot slot in inventorySlotsList)
        {
            if (slot == null || (slot.ItemSO != null && slot.ItemSO != item)) continue;
            remaining -= slot.AddItem(item, remaining);
            if (remaining <= 0) break;
        }

        if (remaining != 0)
        {
            Debug.LogError($"库存预检通过但放入失败：{item.itemName}，剩余 {remaining}。本次购买未扣钱。");
            RemoveFromSlots(item, amount - remaining);
            return false;
        }

        RecordItemHistory(item, amount);
        return true;
    }

    public int GetItemQuantity(ItemSO item)
    {
        if (item == null) return 0;
        EnsureSlotsInitialized();

        int total = 0;
        foreach (InventorySlot slot in inventorySlotsList)
        {
            if (slot != null && slot.ItemSO == item)
            {
                total += slot.Quantity;
            }
        }
        return total;
    }

    /// <summary>先检查所有物品，再一次性扣除；任一不足时库存完全不变。</summary>
    public bool TryRemoveItems(Dictionary<ItemSO, int> requirements)
    {
        if (requirements == null) return false;
        EnsureSlotsInitialized();

        foreach (var requirement in requirements)
        {
            if (requirement.Key == null || requirement.Value <= 0) continue;
            if (GetItemQuantity(requirement.Key) < requirement.Value) return false;
        }

        foreach (var requirement in requirements)
        {
            if (requirement.Key == null || requirement.Value <= 0) continue;
            int removed = RemoveFromSlots(requirement.Key, requirement.Value);
            if (removed != requirement.Value)
            {
                Debug.LogError($"任务物品扣除异常：{requirement.Key.itemName} 应扣 {requirement.Value}，实际 {removed}。");
                return false;
            }
            RecordItemHistory(requirement.Key, -removed);
        }
        return true;
    }

    private int RemoveFromSlots(ItemSO item, int amount)
    {
        int remaining = amount;
        foreach (InventorySlot slot in inventorySlotsList)
        {
            if (slot == null || slot.ItemSO != item) continue;
            remaining -= slot.RemoveItem(remaining);
            if (remaining <= 0) break;
        }
        return amount - remaining;
    }
    private void DropLoot(ItemSO item, int quantity, Loot existingLoot = null)
    {
        if (existingLoot != null)
        {
            // 直接位移现有loot对象到玩家脚下
            existingLoot.transform.position = player.position;
            existingLoot.Initialize(item, quantity);
            existingLoot.sr.enabled = true;
            existingLoot.gameObject.SetActive(true);
            StartCoroutine(ResetLootState(existingLoot));
        }
        else
        {
            var sceneChanger = FindObjectOfType<SceneChanger>();
            Scene currentScene = sceneChanger != null ? sceneChanger.GetCurrentScene() : SceneManager.GetActiveScene();
            GameObject lootObj = Instantiate(lootPrefab, player.position, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(lootObj, currentScene);
            Loot loot = lootObj.GetComponent<Loot>();
            loot.Initialize(item, quantity);
        }
    }
    private IEnumerator ResetLootState(Loot loot)
    {
        yield return new WaitForFixedUpdate();
        AnimatorStateInfo stateInfo = loot.animator.GetCurrentAnimatorStateInfo(0);
        yield return new WaitForSeconds(stateInfo.length * 0.3f);
        loot.canBePick = true;
        loot.hasBeenPicked = false;
        loot.animator.SetBool("isPicked", false);
    }
    public void SetSlotBeenClicked(InventorySlot slot)
    {
        slotBeenClicked = slot;
    }
    public void DropByClick(InventorySlot slot)
    {
        if (slot.ItemSO == null) return;
        ItemSO droppedItem = slot.ItemSO;
        DropLoot(droppedItem, 1);
        int removed = slot.RemoveItem(1);
        RecordItemHistory(droppedItem, -removed);
    }


    public void UseItem(InventorySlot slot)
    {
        if (slot.ItemSO != null && slot.Quantity > 0)
        {
            useItem.ApplyItemEffects(slot.ItemSO);//使用效果
            ItemSO used = slot.ItemSO;
            slot.RemoveItem(1);
            RecordItemHistory(used, -1);
        }
    }
    public void UpdateGold(int price)
    {
        goldAmount = Mathf.Max(0, goldAmount - price);
        RefreshGoldUI();
    }

    private void RefreshGoldUI()
    {
        if (goldAmountText != null)
        {
            goldAmountText.text = goldAmount.ToString();
        }
    }

    private static void RecordItemHistory(ItemSO item, int quantity)
    {
        if (ItemHistoryManager.Instance != null)
        {
            ItemHistoryManager.Instance.RecordItem(item, quantity);
        }
    }

    public DataDefinition GetDataID() => null;

    public void SaveData(Data data)
    {
        if (data == null) return;
        EnsureSlotsInitialized();

        data.goldAmount = Mathf.Max(0, goldAmount);
        data.inventorySlots = new List<InventorySlotStatus>(inventorySlotsList.Count);
        foreach (InventorySlot slot in inventorySlotsList)
        {
            ItemSO item = slot != null ? slot.ItemSO : null;
            int quantity = slot != null ? slot.Quantity : 0;
            data.inventorySlots.Add(new InventorySlotStatus(
                item != null ? item.name : string.Empty,
                item != null ? Mathf.Max(0, quantity) : 0));
        }
    }

    public void LoadData(Data data)
    {
        if (data == null) return;
        EnsureSlotsInitialized();

        var itemById = new Dictionary<string, ItemSO>(StringComparer.Ordinal);
        if (knownItems != null)
        {
            foreach (ItemSO item in knownItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.name) && !itemById.ContainsKey(item.name))
                {
                    itemById.Add(item.name, item);
                }
            }
        }

        foreach (InventorySlot slot in inventorySlotsList)
        {
            slot?.RestoreItem(null, 0);
        }

        List<InventorySlotStatus> savedSlots = data.inventorySlots ?? new List<InventorySlotStatus>();
        int restoreCount = Mathf.Min(savedSlots.Count, inventorySlotsList.Count);
        for (int i = 0; i < restoreCount; i++)
        {
            InventorySlotStatus saved = savedSlots[i];
            if (saved == null || string.IsNullOrEmpty(saved.itemId) || saved.quantity <= 0) continue;

            if (!itemById.TryGetValue(saved.itemId, out ItemSO item))
            {
                Debug.LogWarning($"存档中的物品资源不存在，已跳过：{saved.itemId}");
                continue;
            }

            inventorySlotsList[i]?.RestoreItem(item, saved.quantity);
        }

        goldAmount = Mathf.Max(0, data.goldAmount);
        RefreshGoldUI();
        RebuildItemHistory();
    }

    private void RebuildItemHistory()
    {
        if (ItemHistoryManager.Instance == null) return;
        ItemHistoryManager.Instance.ClearHistory();
        foreach (InventorySlot slot in inventorySlotsList)
        {
            if (slot != null && slot.ItemSO != null && slot.Quantity > 0)
            {
                ItemHistoryManager.Instance.RecordItem(slot.ItemSO, slot.Quantity);
            }
        }
    }

}
