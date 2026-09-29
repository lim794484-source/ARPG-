using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
public class SkillTreeManager : MonoBehaviour, ISaveable
{
    [SerializeField] private SkillSlot[] skillSlots;
    [SerializeField] private TMP_Text pointsText;
    private int[] initialLevels;
    private bool[] initialUnlocked;

    private void Awake()
    {
        int count = skillSlots != null ? skillSlots.Length : 0;
        initialLevels = new int[count];
        initialUnlocked = new bool[count];
        for (int i = 0; i < count; i++)
        {
            if (skillSlots[i] == null) continue;
            initialLevels[i] = skillSlots[i].CurrentLevel;
            initialUnlocked[i] = skillSlots[i].isUnlocked;
        }
    }


    private void OnEnable()
    {
        SkillSlot.OnAbilityPointSpent += HandleAbilityPointSpent;
        SkillSlot.OnMaxSkillLevel += HandleSkillMaxed;
        ExpManager.OnLevelUp += UpdateAbilityPoints;
        if (DataManager.Instance != null) ((ISaveable)this).RegisterSaveable();
    }


    private void OnDisable()
    {
        SkillSlot.OnAbilityPointSpent -= HandleAbilityPointSpent;
        SkillSlot.OnMaxSkillLevel -= HandleSkillMaxed;
        ExpManager.OnLevelUp -= UpdateAbilityPoints;
        if (DataManager.Instance != null) ((ISaveable)this).UnRegisterSaveable();

    }
    private void HandleAbilityPointSpent(SkillSlot skillSlot)
    {
        if (StatsManager.Instance.GetSkillPoints() > 0)
        {
            UpdateAbilityPoints(-1);
        }
    }

    private void HandleSkillMaxed(SkillSlot skillSlot)//传入slot以便获知哪个技能槽满了
    {
        foreach (SkillSlot slot in skillSlots)
        {
            if (slot.isUnlocked == false && slot.CanUnlockSkill())
                slot.Unlock();
        }
    }
    private void Start()
    {
        foreach (SkillSlot slot in skillSlots)
        {
            slot.skillButton.onClick.AddListener(()=>{
                if (StatsManager.Instance.GetSkillPoints() > 0)
                    slot.TryUpgradeSkill();
            });//注册事件处理器，但是由unity刷新时响应每次的事件
        }
        UpdateAbilityPoints(0);
    }
    public void UpdateAbilityPoints(int amount)
    {
        StatsManager.Instance.UpdateSkillPoints(amount);
        RefreshPointsText();
    }

    public DataDefinition GetDataID() => null;

    public void SaveData(Data data)
    {
        if (data == null) return;
        data.skillStats = new Dictionary<string, SkillStatus>();
        if (skillSlots == null) return;

        for (int i = 0; i < skillSlots.Length; i++)
        {
            SkillSlot slot = skillSlots[i];
            if (slot?.skillSO == null) continue;
            data.skillStats[GetSkillPersistenceKey(i, slot)] = new SkillStatus(slot.CurrentLevel, slot.isUnlocked);
        }
    }

    public void LoadData(Data data)
    {
        if (skillSlots == null) return;
        Dictionary<string, SkillStatus> saved = data?.skillStats;
        for (int i = 0; i < skillSlots.Length; i++)
        {
            SkillSlot slot = skillSlots[i];
            if (slot?.skillSO == null) continue;

            if (saved != null
                && saved.TryGetValue(GetSkillPersistenceKey(i, slot), out SkillStatus status)
                && status != null)
            {
                slot.RestoreProgress(status.level, status.isUnlocked);
            }
            else
            {
                int level = initialLevels != null && i < initialLevels.Length ? initialLevels[i] : 0;
                bool unlocked = initialUnlocked != null && i < initialUnlocked.Length && initialUnlocked[i];
                slot.RestoreProgress(level, unlocked);
            }
        }
        RefreshPointsText();
    }

    private static string GetSkillPersistenceKey(int index, SkillSlot slot)
    {
        // 多个 UI 槽可以引用同一个 SkillSO；仅用资源名会互相覆盖。
        // skillSlots 是场景中显式序列化的稳定顺序，因此“序号 + 资源名”可唯一标识当前槽。
        string skillName = slot?.skillSO != null ? slot.skillSO.name : "missing";
        return $"skill-slot:{index}:{skillName}";
    }

    private void RefreshPointsText()
    {
        if (pointsText != null && StatsManager.Instance != null)
        {
            pointsText.text = "技能点: " + StatsManager.Instance.GetSkillPoints();
        }
    }
}
