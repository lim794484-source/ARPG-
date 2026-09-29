using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ShopInfo : MonoBehaviour
{
    [SerializeField] private CanvasGroup infoPanel;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemDescriptionText;

    [Header("Stats")] [SerializeField] private TMP_Text[] itemStatsText;

    private RectTransform infoPanelRect;


    private void Awake()
    {
        infoPanelRect = GetComponent<RectTransform>();
    }

    public void ShowItemInfo(ItemSO item)
    {
        infoPanel.alpha = 1;
        itemNameText.text = item.itemName;
        itemDescriptionText.text = item.itemDescription;

        List<string> stats = new List<string>();

        if (item.currentHealth > 0) stats.Add("生命:" + item.currentHealth.ToString());
        if (item.maxHealth > 0) stats.Add("最大生命:" + item.maxHealth.ToString());
        if (item.damage > 0) stats.Add("攻击力:" + item.damage.ToString());
        if (item.speed > 0) stats.Add("速度:" + item.speed.ToString());
        if (item.duration > 0) stats.Add("持续时间:" + item.duration.ToString());


        for (int i = 0; i < itemStatsText.Length; i++)
        {
            if (i < stats.Count)
            {
                itemStatsText[i].text = stats[i];
                itemStatsText[i].gameObject.SetActive(true);
            }
            else
            {
                itemStatsText[i].gameObject.SetActive(false);
                //不需要显示的位置就禁用游戏对象，而且不会因为删除和使用了gridgroup而改变显示布局
            }
        }
    }


    public void HideItemInfo()
    {
        infoPanel.alpha = 0;
        itemNameText.text = "";
        itemDescriptionText.text = "";
    }

    public void FollowMouse()
    {
        if (Mouse.current == null) return;
        Vector3 mousePosition = Mouse.current.position.ReadValue();
        mousePosition += new Vector3(10, -10, 0); //偏移位置以免遮挡
        infoPanelRect.position = mousePosition;
    }
}