using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class QuestRewardsSlot : MonoBehaviour
{
    [SerializeField] private Image rewardImage;
    [SerializeField] private TMP_Text rewardQuantity;

    public void DisplayReward(Sprite sprite,int quantity)
    {
        rewardImage.sprite=sprite;
        rewardQuantity.text=quantity.ToString();
    }
}
