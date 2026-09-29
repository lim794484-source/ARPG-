using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameplayBugFixSetup
{
    private const string GameplayScenesFolder = "Assets/Scenes/GameScene";
    private const string PersistentScenePath = "Assets/Scenes/GameScene/PersistentScene.unity";

    [MenuItem("Tools/游戏修复/应用并审核四项修复")]
    public static void ApplyAndValidate()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            int enemyCount = AssignStableEnemyIds();
            RepairExperienceCanvas();
            ValidateSceneConfiguration(enemyCount);
            ValidateInventoryTransactions();
            ValidateExperienceRules();
            ValidateSaveSerialization();
            AssetDatabase.SaveAssets();
            Debug.Log($"四项游戏修复审核通过：{enemyCount} 个敌人已配置稳定 ID；购买、任务扣除、经验规则与经验条均通过验证。");
        }
        finally
        {
            if (!Application.isBatchMode && previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }
    }

    private static int AssignStableEnemyIds()
    {
        var usedIds = new HashSet<string>();
        int count = 0;
        foreach (string scenePath in FindGameplayScenePaths())
        {
            if (!IsEnemyPersistenceScene(scenePath)) continue;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool changed = false;
            foreach (EnemyHealth enemy in FindInScene<EnemyHealth>(scene))
            {
                var serializedEnemy = new SerializedObject(enemy);
                SerializedProperty idProperty = serializedEnemy.FindProperty("persistentId");
                if (idProperty == null)
                    throw new InvalidOperationException($"{scenePath} 中 EnemyHealth 缺少 persistentId 字段。");

                string id = idProperty.stringValue;
                if (string.IsNullOrWhiteSpace(id) || !usedIds.Add(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    idProperty.stringValue = id;
                    serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
                    usedIds.Add(id);
                    changed = true;
                }
                count++;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        return count;
    }

    private static void RepairExperienceCanvas()
    {
        Scene scene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);
        GameObject expCanvas = FindGameObject(scene, "ExpCanvas");
        if (expCanvas == null) throw new InvalidOperationException("PersistentScene 缺少 ExpCanvas。");

        bool changed = false;
        Canvas expRootCanvas = expCanvas.GetComponent<Canvas>();
        if (expRootCanvas != null && !expRootCanvas.enabled)
        {
            expRootCanvas.enabled = true;
            EditorUtility.SetDirty(expRootCanvas);
            changed = true;
        }
        CanvasGroup expCanvasGroup = expCanvas.GetComponent<CanvasGroup>();
        if (expCanvasGroup != null && expCanvasGroup.alpha < 0.999f)
        {
            expCanvasGroup.alpha = 1f;
            EditorUtility.SetDirty(expCanvasGroup);
            changed = true;
        }

        StatsManager statsManager = FindInScene<StatsManager>(scene).FirstOrDefault();
        if (statsManager == null) throw new InvalidOperationException("PersistentScene 缺少 StatsManager。");
        PlayerStatsData stats = statsManager.GetStats();
        int oldLevel = stats.level;
        int oldThreshold = stats.expToUpgrade;
        float oldMultiplier = stats.expMultiplier;
        ExpManager.NormalizeStats(stats);
        if (oldLevel != stats.level || oldThreshold != stats.expToUpgrade
            || !Mathf.Approximately(oldMultiplier, stats.expMultiplier))
        {
            EditorUtility.SetDirty(statsManager);
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void ValidateSceneConfiguration(int expectedEnemyCount)
    {
        if (expectedEnemyCount <= 0) throw new InvalidOperationException("没有找到任何敌人实例。");

        var ids = new HashSet<string>();
        int count = 0;
        foreach (string scenePath in FindGameplayScenePaths())
        {
            if (!IsEnemyPersistenceScene(scenePath)) continue;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (EnemyHealth enemy in FindInScene<EnemyHealth>(scene))
            {
                if (string.IsNullOrWhiteSpace(enemy.PersistentId))
                    throw new InvalidOperationException($"{scenePath}/{enemy.name} 没有持久化 ID。");
                if (!ids.Add(enemy.PersistentId))
                    throw new InvalidOperationException($"敌人持久化 ID 重复：{enemy.PersistentId}");
                count++;
            }
        }
        if (count != expectedEnemyCount)
            throw new InvalidOperationException($"敌人数验证不一致：配置 {expectedEnemyCount}，复查 {count}。");

        Scene persistentScene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);
        GameObject expCanvas = FindGameObject(persistentScene, "ExpCanvas");
        ExpManager expManager = FindInScene<ExpManager>(persistentScene).FirstOrDefault();
        if (expManager == null) throw new InvalidOperationException("PersistentScene 缺少 ExpManager。");
        expManager.EnsureCanvasVisible();
        Canvas expRootCanvas = expCanvas != null ? expCanvas.GetComponent<Canvas>() : null;
        if (expCanvas == null || !expCanvas.activeSelf || expRootCanvas == null || !expRootCanvas.enabled)
            throw new InvalidOperationException("经验画布仍不可见。");

        var serializedExp = new SerializedObject(expManager);
        Slider expSlider = serializedExp.FindProperty("expSlider")?.objectReferenceValue as Slider;
        if (expSlider == null
            || serializedExp.FindProperty("currentLevelText")?.objectReferenceValue == null)
            throw new InvalidOperationException("ExpManager 的 Slider 或等级文本引用为空。");
        if (!expSlider.gameObject.activeSelf || expSlider.fillRect == null
            || expSlider.fillRect.localScale.y < 0.5f)
            throw new InvalidOperationException("经验条填充区域仍不可见。");

        InventoryManager inventoryManager = FindInScene<InventoryManager>(persistentScene).FirstOrDefault();
        if (inventoryManager == null) throw new InvalidOperationException("PersistentScene 缺少 InventoryManager。");
        SerializedProperty knownItems = new SerializedObject(inventoryManager).FindProperty("knownItems");
        if (knownItems == null || knownItems.arraySize == 0)
            throw new InvalidOperationException("背包存档缺少物品资源目录。");
        for (int i = 0; i < knownItems.arraySize; i++)
        {
            if (knownItems.GetArrayElementAtIndex(i).objectReferenceValue == null)
                throw new InvalidOperationException($"背包存档物品目录第 {i} 项为空。");
        }


        SkillTreeManager skillTree = FindInScene<SkillTreeManager>(persistentScene).FirstOrDefault();
        if (skillTree == null) throw new InvalidOperationException("PersistentScene 缺少 SkillTreeManager。");
        SerializedObject serializedSkillTree = new SerializedObject(skillTree);
        SerializedProperty skillSlots = serializedSkillTree.FindProperty("skillSlots");
        int expectedSkills = 0;
        for (int i = 0; i < skillSlots.arraySize; i++)
        {
            if (skillSlots.GetArrayElementAtIndex(i).objectReferenceValue != null) expectedSkills++;
        }
        var skillData = new Data();
        skillTree.SaveData(skillData);
        if (skillData.skillStats.Count != expectedSkills)
            throw new InvalidOperationException($"技能存档键不唯一：应保存 {expectedSkills} 个槽，实际 {skillData.skillStats.Count} 个。");

        SkillManager skillManager = FindInScene<SkillManager>(persistentScene).FirstOrDefault();
        if (skillManager == null
            || new SerializedObject(skillManager).FindProperty("combat")?.objectReferenceValue == null)
            throw new InvalidOperationException("SkillManager 缺少 PlayerCombat 引用。");
    }

    private static void ValidateInventoryTransactions()
    {
        GameObject root = new GameObject("InventoryTransactionValidation");
        root.SetActive(false);
        ItemSO item = ScriptableObject.CreateInstance<ItemSO>();
        item.name = "StableTestMeat";
        item.itemName = "测试生肉";
        item.stackableSize = 3;

        try
        {
            Transform hotbar = new GameObject("Hotbar").transform;
            hotbar.SetParent(root.transform);
            Transform backpack = new GameObject("Backpack").transform;
            backpack.SetParent(root.transform);

            InventorySlot first = new GameObject("FirstSlot").AddComponent<InventorySlot>();
            first.transform.SetParent(hotbar);
            InventorySlot hiddenSecond = new GameObject("HiddenSecondSlot").AddComponent<InventorySlot>();
            hiddenSecond.transform.SetParent(backpack);
            hiddenSecond.gameObject.SetActive(false);

            InventoryManager manager = root.AddComponent<InventoryManager>();
            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("hotbarParent").objectReferenceValue = hotbar;
            serializedManager.FindProperty("backpackParent").objectReferenceValue = backpack;
            SerializedProperty knownItems = serializedManager.FindProperty("knownItems");
            knownItems.arraySize = 1;
            knownItems.GetArrayElementAtIndex(0).objectReferenceValue = item;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            manager.UpdateGold(-100);
            for (int i = 0; i < 6; i++)
            {
                if (!manager.TryPurchaseItem(item, 10))
                    throw new InvalidOperationException($"第 {i + 1} 次购买意外失败。");
            }

            if (first.Quantity != 3 || hiddenSecond.Quantity != 3 || !hiddenSecond.gameObject.activeSelf)
                throw new InvalidOperationException("超过单格上限后没有正确进入并显示下一格。");
            if (manager.GoldAmount != 40)
                throw new InvalidOperationException("成功购买后的金币数错误。");

            if (manager.TryPurchaseItem(item, 10) || manager.GoldAmount != 40)
                throw new InvalidOperationException("背包满时仍然购买成功或扣除了金币。");

            var consumeFour = new Dictionary<ItemSO, int> { [item] = 4 };
            if (!manager.TryRemoveItems(consumeFour) || manager.GetItemQuantity(item) != 2)
                throw new InvalidOperationException("任务提交没有正确扣除跨槽物品。");

            var consumeTooMany = new Dictionary<ItemSO, int> { [item] = 3 };
            if (manager.TryRemoveItems(consumeTooMany) || manager.GetItemQuantity(item) != 2)
                throw new InvalidOperationException("任务物品不足时发生了部分扣除。");

            var saveData = new Data();
            manager.SaveData(saveData);
            if (!manager.TryRemoveItems(new Dictionary<ItemSO, int> { [item] = 2 }))
                throw new InvalidOperationException("存档回读测试准备失败。");
            manager.UpdateGold(40);
            manager.LoadData(saveData);
            if (manager.GetItemQuantity(item) != 2 || manager.GoldAmount != 40)
                throw new InvalidOperationException("背包或金币没有完整恢复。");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    private static void ValidateExperienceRules()
    {
        var stats = new PlayerStatsData { level = 0, currentExp = -1, expToUpgrade = 0, expMultiplier = 0 };
        ExpManager.NormalizeStats(stats);
        if (stats.level != 1 || stats.currentExp != 0 || stats.expToUpgrade < 10 || stats.expMultiplier <= 1f)
            throw new InvalidOperationException("经验默认值修复规则不合理。");
    }

    private static void ValidateSaveSerialization()
    {
        var data = new Data
        {
            goldAmount = 37,
            inventorySlots = new List<InventorySlotStatus> { new InventorySlotStatus("Meat", 4) },
            skillStats = new Dictionary<string, SkillStatus> { ["Dash"] = new SkillStatus(2, true) },
            chattedCharacterIds = new HashSet<string> { "npc-guid" },
            chattedDialogIds = new HashSet<string> { "Dialog_A" },
            questProgressDic = new Dictionary<string, QuestProgressStatus>
            {
                ["quest:Quest_A"] = new QuestProgressStatus
                {
                    questState = (int)MyEnums.QuestState.Accepted,
                    objectiveProgress = new List<int> { 2, 1 }
                }
            }
        };

        string json = JsonConvert.SerializeObject(data);
        Data restored = JsonConvert.DeserializeObject<Data>(json);
        if (restored == null
            || restored.goldAmount != 37
            || restored.inventorySlots?.Count != 1
            || restored.inventorySlots[0].itemId != "Meat"
            || restored.inventorySlots[0].quantity != 4
            || !restored.skillStats.TryGetValue("Dash", out SkillStatus skill) || skill.level != 2 || !skill.isUnlocked
            || !restored.chattedCharacterIds.Contains("npc-guid")
            || !restored.chattedDialogIds.Contains("Dialog_A")
            || !restored.questProgressDic.TryGetValue("quest:Quest_A", out QuestProgressStatus quest)
            || quest.objectiveProgress.Count != 2 || quest.objectiveProgress[0] != 2)
        {
            throw new InvalidOperationException("完整进度存档 JSON 往返校验失败。");
        }

        // 旧档没有新增字段也必须能读，字段初始化后按空进度处理。
        Data legacy = JsonConvert.DeserializeObject<Data>("{}");
        if (legacy == null || legacy.inventorySlots == null || legacy.skillStats == null
            || legacy.questProgressDic == null || legacy.chattedCharacterIds == null || legacy.chattedDialogIds == null)
        {
            throw new InvalidOperationException("旧存档兼容校验失败。");
        }
    }

    private static IEnumerable<string> FindGameplayScenePaths()
    {
        return AssetDatabase.FindAssets("t:Scene", new[] { GameplayScenesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .OrderBy(path => path);
    }

    private static bool IsEnemyPersistenceScene(string scenePath)
    {
        return scenePath != PersistentScenePath
            && !scenePath.EndsWith("/StartingMenu.unity", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static GameObject FindGameObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName) return child.gameObject;
            }
        }
        return null;
    }
}
