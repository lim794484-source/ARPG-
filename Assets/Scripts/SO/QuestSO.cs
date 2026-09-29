using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "New Quest", menuName = "QuestSO")]
public class QuestSO : ScriptableObject
{
    public string questName;
    public int lv;
    [TextArea] public string questDescription;

    public List<QuestObjective> questObjectives;
    public List<Reward> rewards;
}

[Serializable]
public class Reward
{
    public ItemSO rewardItem;
    public int quantity;
}
[Serializable]
public class QuestObjective
{
    public string description;
    public ItemSO targetItem;
    public CharacterSO targetCharacter;
    public LocationSO targetLocation;


    public int requiredAmount;
    public int currentAmount;
}

/*
 * ============================================================
 * QuestSO —— 任务配置说明
 * ============================================================
 * 用途：定义一个任务（剧情/支线/日常等），包含任务信息、目标列表、奖励列表。
 * 创建：Project 窗口右键 > Create > QuestSO，
 *      会生成 fileName 为 "New Quest" 的资源文件，
 *      建议重命名为有业务含义的名字（如 Quest_SlayTheDragon）。
 * 挂载位置：无需挂载到 GameObject，纯数据资源；由任务系统按引用读取。
 *
 * 依赖的 SO：
 *   - ItemSO       目标物品 / 奖励物品，路径 GameLootSO/ItemSO
 *   - CharacterSO  目标角色（击杀/护送/对话），路径 Dialog/Character
 *   - LocationSO   目标地点（到达/探索），LocationSO.cs
 *
 * 配置要点（Inspector 字段）：
 *
 * 【任务基础信息】
 *   - questName        : 任务标题（显示在任务面板）。
 *   - lv               : 建议等级 / 任务难度等级。
 *   - questDescription : 任务描述正文（TextArea 大文本框）。
 *
 * 【questObjectives[] —— 任务目标列表】
 *   每个 QuestObjective 代表一项需达成的条件，全部完成则任务完成。
 *   - description    : 该目标的说明文字（如“前往酒馆找村长”）。
 *   - targetItem     : 目标物品（采集/上交类任务）。
 *   - targetCharacter: 目标角色（击杀/对话/护送类任务）。
 *   - targetLocation : 目标地点（到达/探索类任务）。
 *   - requiredAmount : 需要达成的数量（如“收集 5 个草药”填 5）。
 *   - currentAmount  : 【当前未使用 / 疑似遗留字段】注意：项目运行时进度并不存在这里，
 *                      而是 QuestManager 用 questProgress 字典单独维护
 *                      （参见 QuestManager.GetCurrentObjAmount）。该字段目前无任何代码读写，
 *                      可考虑移除，或明确其用途。
 *
 * 【rewards[] —— 奖励列表】
 *   每个 Reward 代表一项发放奖励。
 *   - rewardItem : 奖励物品（ItemSO，注意 isGold/isEXP 也可作为金币/经验奖励）。
 *   - quantity   : 发放数量。
 *
 * 推荐配置流程：
 *   1) 先创建好所需 ItemSO / CharacterSO / LocationSO。
 *   2) 创建 QuestSO，填 questName / lv / questDescription。
 *   3) 在 questObjectives 里逐项添加目标，按任务类型只填相关字段
 *      （物品类填 targetItem，角色类填 targetCharacter，地点类填 targetLocation），
 *      并设置 requiredAmount。
 *   4) 在 rewards 里挂奖励物品与数量。
 *
 * 注意事项：
 *   - 一个 QuestObjective 通常只用一种 target（物品/角色/地点），其余留空，
 *     避免任务系统判定时产生歧义。
 *   - currentAmount 字段目前未被任何代码使用（运行时进度由 QuestManager 的
 *     questProgress 字典维护），属于遗留字段，谨慎依赖或建议清理。
 *   - 奖励的金币/经验建议用 isGold / isEXP 标记的 ItemSO 统一表示，便于系统统计。
 *   - SO 之间是引用关系，重命名/移动资源会断引用。
 * ============================================================
 */