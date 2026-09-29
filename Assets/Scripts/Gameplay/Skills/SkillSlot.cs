using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
public class SkillSlot : MonoBehaviour
{
    public SkillSO skillSO;
    [SerializeField] private Image skillIcon;
    [SerializeField] private TMP_Text skillLevelText;
    public Button skillButton;
    [SerializeField] private List<SkillSlot> preRiquriedForSkillUnlock_List;

    [SerializeField] private int currentLevel;
    public bool isUnlocked = false;
    public int CurrentLevel => currentLevel;

    public static event Action<SkillSlot> OnAbilityPointSpent;//事件广播
    public static event Action<SkillSlot> OnMaxSkillLevel;//事件广播
    private void OnValidate()
    {
        if (skillSO != null && skillLevelText != null)
        {
            UpdateUI();
        }
    }
    private void UpdateUI()
    {
        skillIcon.sprite = skillSO.skillIcon;
        if (isUnlocked)
        {

            skillButton.interactable = true;
            skillLevelText.text = currentLevel.ToString() + "/" + skillSO.maxLevel.ToString();
            skillIcon.color = Color.white;
        }
        else
        {
            skillButton.interactable = false;
            skillLevelText.text = "未解锁";
            skillIcon.color = Color.grey;

        }
    }
    public void TryUpgradeSkill()
    {
        if (isUnlocked && currentLevel < skillSO.maxLevel)
        {
            currentLevel++;

            OnAbilityPointSpent?.Invoke(this);//如果事件非空： ?. 发送事件给订阅者
            if (currentLevel >= skillSO.maxLevel)
            {
                OnMaxSkillLevel?.Invoke(this);
            }
            UpdateUI();
        }
    }
    public void Unlock()
    {
        isUnlocked = true;
        UpdateUI();
    }

    public void RestoreProgress(int level, bool unlocked)
    {
        currentLevel = skillSO == null ? 0 : Mathf.Clamp(level, 0, Mathf.Max(0, skillSO.maxLevel));
        isUnlocked = unlocked;
        UpdateUI();
    }

    public bool CanUnlockSkill()
    {
        foreach (SkillSlot slot in preRiquriedForSkillUnlock_List)
        {
            if (!slot.isUnlocked || slot.currentLevel < slot.skillSO.maxLevel)
            {
                return false;
            }
        }
        return true;
    }
    //void OnDrawGizmos()
    //{
    //    Gizmos.color = Color.yellow;

    //    foreach (var next in preRiquriedForSkillUnlock_List)
    //    {
    //        if (next != null)
    //        {
    //            Gizmos.DrawLine(
    //                transform.position,
    //                next.transform.position
    //            );
    //        }
    //    }
    //}
}
