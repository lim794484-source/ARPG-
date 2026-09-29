using System;
using System.Collections.Generic;
using UnityEngine;
public class ShopManager : YSingleton<ShopManager>, ICanvasManager
{
    [SerializeField] private ShopSlot[] shopSlots;
    [SerializeField] private CanvasGroup shopCanvasGroup;

    [Header("Events To Trigger")]
    [SerializeField] private InventorySlotsStatsSO InventoryUpdateRequest;
    [Header("Events To Receive")]
    [SerializeField] private ToggleCanvasEventSO toggleShopCanvasEvent;
    public ToggleCanvasEventSO ToggleCanvasEvent => toggleShopCanvasEvent;


    private List<ShopItems> shopItems;
    private List<ShopItems> shopWeapon;
    private List<ShopItems> shopArmor;
    private ShopKeeper activeShopKeeper;
    private Canvas canvas;

    public Transform CurrentPortraitTarget
    {
        get
        {
            if (activeShopKeeper != null)
                return activeShopKeeper.PortraitTarget;
            return null;
        }
    }
    private bool isShopOpen = false;
    public bool IsShopOpen => isShopOpen;

    protected override void OnSingletonInitialized()
    {
        canvas = shopCanvasGroup.GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        toggleShopCanvasEvent.toggleCanvasEvent += OnShopToggle;
        toggleShopCanvasEvent.focusEvent += OnFocus;
    }

    private void OnDisable()
    {
        toggleShopCanvasEvent.toggleCanvasEvent -= OnShopToggle;
        toggleShopCanvasEvent.focusEvent -= OnFocus;
    }

    public void RegisterActiveShopKeeper(ShopKeeper keeper)
    {
        activeShopKeeper = keeper;
    }

    public void UnregisterActiveShopKeeper()
    {
        activeShopKeeper = null;
    }

    private void OnShopToggle(bool state)
    {
        if (state)
        {
            if (!isShopOpen && activeShopKeeper != null)
            {
                OpenShop(
                    activeShopKeeper.ShopItems,
                    activeShopKeeper.ShopWeapon,
                    activeShopKeeper.ShopArmor);
            }
        }
        else
        {
            CloseShop();
        }
    }

    private void OnFocus()
    {
        if (!isShopOpen) return;
        ((ICanvasManager)this).RefreshCanvaOrder(canvas, MyEnums.CanvasToToggle.Shop, true);
    }

    public void OpenShop(
        List<ShopItems> items,
        List<ShopItems> weapon,
        List<ShopItems> armor)
    {
        shopItems = items;
        shopWeapon = weapon;
        shopArmor = armor;
        OpenItemShop();
        isShopOpen = true;
        ((ICanvasManager)this).ToggleCanvas(shopCanvasGroup, canvas, MyEnums.CanvasToToggle.Shop, true);
    }

    public void CloseShop()
    {
        isShopOpen = false;
        ((ICanvasManager)this).ToggleCanvas(shopCanvasGroup, canvas, MyEnums.CanvasToToggle.Shop, false);
    }

    private void PopulateShopItems(List<ShopItems> shopItems)
    {
        if (shopItems == null) return;
        for (int i = 0; i < shopItems.Count && i < shopSlots.Length; i++)
        {
            ShopItems shopItem = shopItems[i];

            shopSlots[i].Initialize(shopItem.item, shopItem.price);
            shopSlots[i].gameObject.SetActive(true);
        }
        for (int i = shopItems.Count; i < shopSlots.Length; i++)// 清除多余的商店槽位。
        {
            shopSlots[i].gameObject.SetActive(false);

        }
    }

    public void TryBuyItem(ItemSO item, int price)
    {
        InventoryUpdateRequest.RaiseInventoryUpdateRequest(item, price, 1);
    }
    public void SellItem(ItemSO item)
    {
        if (item == null) return;
        foreach (var slot in shopSlots)// 查找玩家想要出售的物品。
        {
            if (slot.GetItemSO() == item)
            {
                // 使用负数价格和数量表示出售。
                InventoryUpdateRequest.RaiseInventoryUpdateRequest(item, -slot.GetPrice(), -1);
                return;
            }
        }
    }

    public void OpenItemShop()
    {
        PopulateShopItems(shopItems);
    }

    public void OpenWeaponShop()
    {
        PopulateShopItems(shopWeapon);
    }

    public void OpenArmorShop()
    {
        PopulateShopItems(shopArmor);
    }
}
[System.Serializable]
public class ShopItems
{
    public ItemSO item;
    public int price;
}
