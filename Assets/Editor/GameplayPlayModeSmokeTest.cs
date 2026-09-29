using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameplayPlayModeSmokeTest
{
    private const string PendingKey = "GameplayBugFix.PlayModePending";
    private const string InitialScenePath = "Assets/Scenes/InitialScene.unity";
    private const string Scene1AssetPath = "Assets/GameSO/GameSceneSO/Scene1.asset";
    private const string Scene2AssetPath = "Assets/GameSO/GameSceneSO/Scene2.asset";
    private const string LoadEventPath = "Assets/GameSO/Events/SceneLoadEventSO.asset";
    private const string MeatPath = "Assets/GameSO/ItemSO/Meat.asset";

    private static int phase;
    private static double deadline;
    private static GameSceneSO scene1;
    private static GameSceneSO scene2;
    private static SceneLoadEventSO loadEvent;
    private static string defeatedEnemyId;

    static GameplayPlayModeSmokeTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/游戏修复/运行实际游戏冒烟测试")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("编辑器已经处于播放或切换状态。");

        // 临时工程运行时使用独立 persistentDataPath，不接触玩家真实存档。
        PlayerSettings.companyName = "CodexValidation";
        PlayerSettings.productName = "ARPG_BugFixValidation";
        SessionState.SetBool(PendingKey, true);
        EditorSceneManager.OpenScene(InitialScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        if (state != PlayModeStateChange.EnteredPlayMode) return;

        phase = 0;
        deadline = EditorApplication.timeSinceStartup + 45d;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException($"实际游戏测试超时，停在阶段 {phase}。");

            switch (phase)
            {
                case 0:
                    WaitForPersistentManagers();
                    break;
                case 1:
                    WaitForScene1AndDefeatEnemy();
                    break;
                case 2:
                    WaitForScene2ThenReturn();
                    break;
                case 3:
                    VerifyEnemyRemainsDefeated();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void WaitForPersistentManagers()
    {
        if (SceneChanger.Instance == null || InventoryManager.Instance == null
            || StatsManager.Instance == null
            || SceneChanger.Instance.GetCurrentGameScene() == null
            || SceneChanger.Instance.GetCurrentGameScene().sceneType != MyEnums.SceneType.Menu)
            return;

        scene1 = AssetDatabase.LoadAssetAtPath<GameSceneSO>(Scene1AssetPath);
        scene2 = AssetDatabase.LoadAssetAtPath<GameSceneSO>(Scene2AssetPath);
        loadEvent = AssetDatabase.LoadAssetAtPath<SceneLoadEventSO>(LoadEventPath);
        ItemSO meat = AssetDatabase.LoadAssetAtPath<ItemSO>(MeatPath);
        if (scene1 == null || scene2 == null || loadEvent == null || meat == null)
            throw new InvalidOperationException("实际游戏测试资源加载失败。");

        InventoryManager inventory = InventoryManager.Instance;
        int goldBeforeGrant = inventory.GoldAmount;
        inventory.UpdateGold(-100);
        int purchaseBudget = inventory.GoldAmount;
        for (int i = 0; i < 4; i++)
        {
            if (!inventory.TryPurchaseItem(meat, 10))
                throw new InvalidOperationException($"实际运行中第 {i + 1} 次生肉购买失败。");
        }
        if (inventory.GetItemQuantity(meat) != 4 || inventory.GoldAmount != purchaseBudget - 40)
            throw new InvalidOperationException("实际运行中生肉跨格显示或扣钱结果错误。");

        if (!inventory.TryRemoveItems(new Dictionary<ItemSO, int> { [meat] = 3 })
            || inventory.GetItemQuantity(meat) != 1)
            throw new InvalidOperationException("实际运行中任务物品扣除失败。");

        // 消除未使用变量警告，同时确认授予金币确实生效。
        if (purchaseBudget != goldBeforeGrant + 100)
            throw new InvalidOperationException("测试金币授予失败。");

        phase = 1;
        loadEvent.RaiseLoadRequestEvent(scene1, scene1.initialPosition, false);
    }

    private static void WaitForScene1AndDefeatEnemy()
    {
        if (SceneChanger.Instance == null || SceneChanger.Instance.GetCurrentGameScene() != scene1)
            return;

        ExpManager expManager = ExpManager.Instance;
        if (expManager == null || !expManager.gameObject.activeInHierarchy)
            throw new InvalidOperationException("实际运行中 ExpManager 或经验画布未激活。");

        Canvas canvas = expManager.GetComponent<Canvas>();
        CanvasGroup canvasGroup = expManager.GetComponent<CanvasGroup>();
        if (canvas == null || !canvas.enabled || (canvasGroup != null && canvasGroup.alpha <= 0.001f))
            throw new InvalidOperationException("实际运行中经验画布未渲染。");

        Slider slider = typeof(ExpManager)
            .GetField("expSlider", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(expManager) as Slider;
        if (slider == null || !slider.gameObject.activeInHierarchy || !RectOverlapsScreen(slider.transform as RectTransform))
            throw new InvalidOperationException("实际运行中经验 Slider 不可见。");

        bool hasVisibleGraphic = false;
        foreach (Graphic graphic in slider.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject.activeInHierarchy && graphic.enabled && graphic.color.a > 0.01f)
            {
                hasVisibleGraphic = true;
                break;
            }
        }
        if (!hasVisibleGraphic)
            throw new InvalidOperationException("实际运行中经验条没有可渲染的图形。");

        EnemyHealth[] enemies = UnityEngine.Object.FindObjectsOfType<EnemyHealth>();
        if (enemies.Length == 0) throw new InvalidOperationException("Scene1 没有可测试敌人。");

        EnemyHealth enemy = enemies[0];
        defeatedEnemyId = enemy.PersistentId;
        if (string.IsNullOrEmpty(defeatedEnemyId))
            throw new InvalidOperationException("实际运行中的敌人没有稳定 ID。");

        int expBefore = StatsManager.Instance.GetCurrentExp();
        enemy.ChangeHealth(-100000);
        int expAfter = StatsManager.Instance.GetCurrentExp();
        if (expAfter <= expBefore || !Mathf.Approximately(slider.value, expAfter))
            throw new InvalidOperationException("击败敌人后经验数据或经验条没有更新。");
        if (slider.fillRect == null || slider.fillRect.localScale.y < 0.5f)
            throw new InvalidOperationException("经验条填充区被压缩，实际画面上不易看见。");

        if (DataManager.Instance?.GetData?.enemiesStatsDic == null
            || !DataManager.Instance.GetData.enemiesStatsDic.TryGetValue(defeatedEnemyId, out EnemyStatus status)
            || !status.isDead)
            throw new InvalidOperationException("敌人死亡没有立即写入运行态存档。");

        phase = 2;
        loadEvent.RaiseLoadRequestEvent(scene2, scene2.initialPosition, false);
    }

    private static bool RectOverlapsScreen(RectTransform rectTransform)
    {
        if (rectTransform == null || rectTransform.rect.width <= 0f || rectTransform.rect.height <= 0f)
            return false;

        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        for (int i = 0; i < corners.Length; i++)
        {
            if (screenRect.Contains(corners[i])) return true;
        }

        Rect sliderRect = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        return sliderRect.Overlaps(screenRect, true);
    }

    private static void WaitForScene2ThenReturn()
    {
        if (SceneChanger.Instance == null || SceneChanger.Instance.GetCurrentGameScene() != scene2)
            return;

        phase = 3;
        loadEvent.RaiseLoadRequestEvent(scene1, scene1.initialPosition, false);
    }

    private static void VerifyEnemyRemainsDefeated()
    {
        if (SceneChanger.Instance == null || SceneChanger.Instance.GetCurrentGameScene() != scene1)
            return;

        foreach (EnemyHealth enemy in UnityEngine.Object.FindObjectsOfType<EnemyHealth>())
        {
            if (enemy.PersistentId == defeatedEnemyId)
                throw new InvalidOperationException("从 Scene2 返回 Scene1 后，已死亡敌人重新出现。");
        }

        EditorApplication.update -= Tick;
        SessionState.SetBool(PendingKey, false);
        Debug.Log("实际游戏冒烟测试通过：生肉第 4 个跨格显示且正确扣钱、任务物品扣除、经验条更新、Scene1→Scene2→Scene1 敌人保持死亡。");
        EditorApplication.Exit(0);
    }

    private static void Fail(Exception exception)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(PendingKey, false);
        Debug.LogException(exception);
        EditorApplication.Exit(1);
    }
}
