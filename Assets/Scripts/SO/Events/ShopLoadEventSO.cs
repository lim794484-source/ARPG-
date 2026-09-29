using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "ShopLoadEventSO", menuName = "Events/ShopLoadEventSO", order = 0)]

public class ShopLoadEventSO : ScriptableObject
{
    public event Action<List<ShopItems>, List<ShopItems>, List<ShopItems>, Transform> ShopLoadEvent;

    public void RaiseShopLoadRequest(
        List<ShopItems> shopItems,
        List<ShopItems> shopWeapon,
        List<ShopItems> shopArmor,
        Transform portraitTarget)
    {
        ShopLoadEvent?.Invoke(shopItems, shopWeapon, shopArmor, portraitTarget);
    }
}
