using System;
using System.Collections.Generic;
using UnityEngine;


public class Data
{
    public Dictionary<string, LootStatus> lootsStatsDic = new();//string是GUID
    public Dictionary<string, EnemyStatus> enemiesStatsDic = new();
    // 背包和金币也属于运行进度。字段初始化保证旧存档缺少这些字段时仍可安全读取。
    public List<InventorySlotStatus> inventorySlots = new();
    public int goldAmount;
    public Dictionary<string, SkillStatus> skillStats = new();
    public HashSet<string> chattedCharacterIds = new();
    public HashSet<string> chattedDialogIds = new();
    public Dictionary<string, QuestProgressStatus> questProgressDic = new();
    public SceneAndPosition sceneIDAndPlayerPos;
    public PlayerStatsData playerStatsData;
}
public class SaveInfo
{
    public string saveID;//时间戳
    public MyEnums.SaveType saveType;
    public SaveInfo(string saveID, MyEnums.SaveType saveType)
    {
        this.saveID = saveID;
        this.saveType = saveType;
    }
}


[Serializable]
public class SerializableVector3
{
    public float x, y, z;
    public SerializableVector3() { }
    public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[Serializable]
public class LootStatus
{
    public SerializableVector3 position;
    public bool hasBeenPicked;
    public LootStatus() { }
    public LootStatus(Vector3 pos, bool picked)
    {
        position = new SerializableVector3(pos);
        hasBeenPicked = picked;
    }
}

[Serializable]
public class EnemyStatus
{
    public bool isDead;
    public int currentHealth;

    public EnemyStatus() { }
    public EnemyStatus(bool dead, int health)
    {
        isDead = dead;
        currentHealth = health;
    }
}

[Serializable]
public class InventorySlotStatus
{
    // 使用资源文件名作为稳定键；显示名称可以继续本地化而不影响旧档。
    public string itemId;
    public int quantity;

    public InventorySlotStatus() { }
    public InventorySlotStatus(string itemId, int quantity)
    {
        this.itemId = itemId;
        this.quantity = quantity;
    }
}

[Serializable]
public class SkillStatus
{
    public int level;
    public bool isUnlocked;

    public SkillStatus() { }
    public SkillStatus(int level, bool isUnlocked)
    {
        this.level = level;
        this.isUnlocked = isUnlocked;
    }
}

[Serializable]
public class QuestProgressStatus
{
    // 枚举按 int 保存，并在加载时校验范围，避免损坏/未来版本数据直接注入非法状态。
    public int questState;
    // QuestObjective 没有独立 ID，因此按 QuestSO 中的稳定列表顺序保存；增删目标时按交集恢复。
    public List<int> objectiveProgress = new();
}

[Serializable]
public class SceneAndPosition
{
    public string sceneID;
    public SerializableVector3 position;
    public SceneAndPosition() { }
    public SceneAndPosition(string sceneID, Vector3 pos)
    {
        this.sceneID = sceneID;
        position = new SerializableVector3(pos);
    }
}
