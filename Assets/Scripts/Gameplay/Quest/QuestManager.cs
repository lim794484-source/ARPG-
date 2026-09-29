using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : YSingleton<QuestManager>, ICanvasManager, ISaveable
{
    [SerializeField] private CanvasGroup questCanvaGroup;

    [Header("Events To Receive")]
    [SerializeField] private VoidEventSO openQuestEventSO;
    [SerializeField] private LoadQuestEventSO loadQuestEventSO;
    [SerializeField] private QuestOptionsEventSO questOptionsEventSO;
    [SerializeField] private ToggleCanvasEventSO toggleQuestEvent;
    public ToggleCanvasEventSO ToggleCanvasEvent => toggleQuestEvent;


    [Header("Events To Trigger")]

    [SerializeField] private InventorySlotsStatsSO QuestRewardRequest;



    [Header("Options")]
    [SerializeField] private CanvasGroup acceptCanvaGroup;
    [SerializeField] private CanvasGroup declineCanvaGroup;
    [SerializeField] private CanvasGroup completeCanvaGroup;


    [Header("QuestLogSlots")]
    [SerializeField] private QuestLogSlot[] questLogSlots;

    [Header("Canvas To Operate While No Quests")]
    [SerializeField] private CanvasGroup detailsCanvaGroup;
    [SerializeField] private CanvasGroup promptCanvaGroup;

    [Header("QuestLogUI")]
    [SerializeField] private QuestLogUI questLogUI;

    private MyEnums.QuestState currentQuestState = MyEnums.QuestState.Idle;

    //（任务（任务状态，任务要求（要求条目）））
    private readonly Dictionary<QuestSO, QuestProgressData> questProgress = new();
    // QuestSO 没有持久化 GUID；资源名比会被本地化的 questName 和每次运行变化的
    // GetInstanceID 更稳定。未加载到当前场景的任务记录也保留在这里，避免中途存档时丢失。
    private readonly Dictionary<string, QuestProgressStatus> loadedQuestProgress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QuestSO> questOwnersByKey = new(StringComparer.Ordinal);
    private bool canvasIsActive;
    private List<QuestSO> currentBoardLoadQuests;
    private QuestSO currentQuest;
    private Canvas canvas;

    public void SetCurrentQuest(QuestSO quest)
    {
        currentQuest = quest;
    }
    public bool CanvasIsActive => canvasIsActive;
    class QuestProgressData
    {
        public QuestProgressData(List<QuestObjective> objectives)
        {
            if (objectives == null) return;
            foreach (var obj in objectives)
            {
                if (obj != null)
                {
                    questObjectives[obj] = 0;
                }
            }
        }
        public MyEnums.QuestState questState = MyEnums.QuestState.Idle;
        public Dictionary<QuestObjective, int> questObjectives = new();
    }
    protected override void OnSingletonInitialized()
    {
        canvas = questCanvaGroup.GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        openQuestEventSO.VoidEvent += OnOpenQuestBoard;
        loadQuestEventSO.LoadQuestEvent += OnReFreshQuestState;
        questOptionsEventSO.questOptionsEvent += OnQuestOptionChose;
        toggleQuestEvent.toggleCanvasEvent += OnToggleQuest;
        toggleQuestEvent.focusEvent += OnFocus;

        if (DataManager.Instance != null)
        {
            ((ISaveable)this).RegisterSaveable();
        }

    }


    private void OnDisable()
    {
        openQuestEventSO.VoidEvent -= OnOpenQuestBoard;
        loadQuestEventSO.LoadQuestEvent -= OnReFreshQuestState;
        questOptionsEventSO.questOptionsEvent -= OnQuestOptionChose;
        toggleQuestEvent.toggleCanvasEvent -= OnToggleQuest;
        toggleQuestEvent.focusEvent -= OnFocus;

        if (DataManager.Instance != null)
        {
            ((ISaveable)this).UnRegisterSaveable();
        }

    }

    private void OnToggleQuest(bool state)
    {
        if (state)
        {
            // 打开由 openQuestEvent 处理，reorder 由 focusEvent 处理，这里不再响应
            return;
        }

        CloseQuestBoard();
    }

    private void OnFocus()
    {
        if (!canvasIsActive) return;
        ((ICanvasManager)this).RefreshCanvaOrder(canvas, MyEnums.CanvasToToggle.Quest, true);
    }
    private void OnQuestOptionChose(MyEnums.QuestState questStateToShift)
    {
        if (currentQuest == null) return;

        if (questStateToShift == MyEnums.QuestState.Completed)
        {
            RefreshQuestObjectives(currentQuest);
            if (IsQuestObjDone(currentQuest) && TryConsumeQuestItems(currentQuest))
            {
                QuestStateChanged(currentQuest, questStateToShift);
            }
            else
            {
                Debug.Log("任务目标物品不足，未提交任务且未发放奖励。");
                if (questLogUI != null) questLogUI.DisPlayObjectives();
            }
        }
        else QuestStateChanged(currentQuest, questStateToShift);

    }
    private void OnOpenQuestBoard()
    {
        if (!canvasIsActive)
        {
            SetQuestCanvasState(true);
        }
    }

    public void CloseQuestBoard()
    {
        SetQuestCanvasState(false);
        currentBoardLoadQuests = null;
        currentQuest = null;
    }

    public bool IsDisplayingQuestBoard(List<QuestSO> quests)
    {
        return canvasIsActive && currentBoardLoadQuests == quests;
    }

    private void OnReFreshQuestState(List<QuestSO> quests)
    {
        if (quests == null) return;
        currentBoardLoadQuests = quests;
        InitializeQuestProgress(quests);

        foreach (var quest in quests)
        {
            RefreshQuestObjectives(quest);
        }

        InitiateQuestSlots(quests);

        if (quests.Count == 0
         || IsAllQuestsCompleted(quests)
        )
        {
            SetNoQuestsState(true);
            return;
        }
        else
            SetNoQuestsState(false);

        foreach (var quest in quests)
        {
            if (IsQuestObjDone(quest) && GetQuestStateFromProgress(quest) == MyEnums.QuestState.Accepted)
            {
                QuestStateChanged(quest, MyEnums.QuestState.IsToComplete);
            }
        }
    }
    private void RaiseRewardEvent(QuestSO quest)
    {
        if (quest?.rewards == null) return;
        foreach (var reward in quest.rewards)
        {
            if (reward == null || reward.rewardItem == null || reward.quantity <= 0) continue;
            ItemSO item = reward.rewardItem;
            int quantity = reward.quantity;
            QuestRewardRequest.RaiseInventoryUpdateRequest(item, 0, quantity);
        }
    }

    private bool TryConsumeQuestItems(QuestSO quest)
    {
        var requirements = new Dictionary<ItemSO, int>();
        if (quest?.questObjectives == null) return true;
        foreach (var objective in quest.questObjectives)
        {
            if (objective == null || objective.targetItem == null || objective.requiredAmount <= 0)
                continue;

            requirements.TryGetValue(objective.targetItem, out int amount);
            requirements[objective.targetItem] = amount + objective.requiredAmount;
        }

        if (requirements.Count == 0) return true;
        return InventoryManager.Instance != null
            && InventoryManager.Instance.TryRemoveItems(requirements);
    }
    private void SetNoQuestsState(bool isOpenWhiteUI)
    {
        SetCanvaState(detailsCanvaGroup, !isOpenWhiteUI);
        SetCanvaState(promptCanvaGroup, isOpenWhiteUI);
    }
    private void InitializeQuestProgress(List<QuestSO> quests)
    {
        if (quests == null) return;
        foreach (var quest in quests)
        {
            EnsureQuestProgressInitialized(quest);
        }
    }

    private void EnsureQuestProgressInitialized(QuestSO quest)
    {
        if (quest == null || questProgress.ContainsKey(quest)) return;

        string key = GetQuestPersistenceKey(quest);
        bool keyIsOwnedByAnotherQuest = questOwnersByKey.TryGetValue(key, out QuestSO owner)
            && owner != null
            && owner != quest;
        if (keyIsOwnedByAnotherQuest)
        {
            // 两个不同 QuestSO 同名时不能安全共享一份记录；给后者新建空进度，避免串档。
            Debug.LogError($"任务资源名重复，无法安全恢复进度：{key}。请重命名其中一个 QuestSO 资源。");
            questProgress.Add(quest, new QuestProgressData(quest.questObjectives));
            return;
        }

        questOwnersByKey[key] = quest;
        var progress = new QuestProgressData(quest.questObjectives);
        if (loadedQuestProgress.TryGetValue(key, out QuestProgressStatus savedProgress))
        {
            ApplySavedProgress(quest, progress, savedProgress);
        }
        questProgress.Add(quest, progress);
    }
    private void InitiateQuestSlots(List<QuestSO> quests)
    {
        foreach (var questSlot in questLogSlots)
        {
            questSlot.SetQuestActive(false);
        }

        int length = Math.Min(questLogSlots.Length, quests.Count);
        for (int i = 0; i < length; i++)
        {
            QuestSO quest = quests[i];
            if (quest == null) continue;
            questLogSlots[i].SetQuest(quest);

            if (GetQuestStateFromProgress(quest) == MyEnums.QuestState.Completed)
            {
                SetQuestSlotToDoneState(quest);
            }
        }
    }
    public QuestSO GetFirstIncompletedQuest()
    {
        if (currentBoardLoadQuests == null) return null;
        foreach (var quest in currentBoardLoadQuests)
        {
            if (quest != null
                && questProgress.TryGetValue(quest, out QuestProgressData progress)
                && progress.questState != MyEnums.QuestState.Completed)
                return quest;
        }
        return null;
    }
    public MyEnums.QuestState GetQuestStateFromProgress(QuestSO quest)
    {
        if (quest == null) return MyEnums.QuestState.Idle;
        EnsureQuestProgressInitialized(quest);
        return questProgress.TryGetValue(quest, out QuestProgressData progress)
            ? progress.questState
            : MyEnums.QuestState.Idle;
    }
    public void QuestStateChanged(QuestSO quest, MyEnums.QuestState state)
    {
        if (quest == null) return;
        EnsureQuestProgressInitialized(quest);
        if (!questProgress.TryGetValue(quest, out QuestProgressData progress)) return;

        MyEnums.QuestState previousState = progress.questState;

        SetCanvaState(acceptCanvaGroup, false);
        SetCanvaState(declineCanvaGroup, false);
        SetCanvaState(completeCanvaGroup, false);

        currentQuestState = state;

        progress.questState = currentQuestState;


        if (currentQuestState == MyEnums.QuestState.Idle)
        {
            SetCanvaState(acceptCanvaGroup, true);

        }
        else if (currentQuestState == MyEnums.QuestState.Accepted)
        {
            SetCanvaState(declineCanvaGroup, true);
            SetCanvaState(completeCanvaGroup, true);

        }
        else if (currentQuestState == MyEnums.QuestState.Decline)
        {
            SetCanvaState(acceptCanvaGroup, true);
            SetCanvaState(declineCanvaGroup, true);

        }
        else if (currentQuestState == MyEnums.QuestState.IsToComplete)
        {
            SetCanvaState(declineCanvaGroup, true);
            SetCanvaState(completeCanvaGroup, true);

        }
        else if (currentQuestState == MyEnums.QuestState.Completed)
        {
            SetQuestSlotToDoneState(quest);
            // 读档后重新显示已完成任务、或重复点击任务详情时，不能再次发奖励。
            if (previousState != MyEnums.QuestState.Completed)
            {
                RaiseRewardEvent(quest);
            }

        }

        if (questLogUI != null) questLogUI.DisPlayObjectives();

    }
    //更新完成条件
    public void UpdateObjectiveProgress(QuestSO quest, QuestObjective obj)
    {
        if (quest == null || obj == null) return;
        EnsureQuestProgressInitialized(quest);
        if (!questProgress.TryGetValue(quest, out QuestProgressData progress)) return;

        var progressDictionary = progress.questObjectives;
        progressDictionary.TryGetValue(obj, out int newAmount);

        if (obj.targetItem != null)
        {
            newAmount = ItemHistoryManager.Instance != null
                ? ItemHistoryManager.Instance.GetItemQuantity(obj.targetItem)
                : newAmount;
        }
        else if (obj.targetCharacter != null
            && ConversationHistoryManager.Instance != null
            && ConversationHistoryManager.Instance.HasChatedWith(obj.targetCharacter))
        {
            newAmount = obj.requiredAmount;
        }

        // 对话/地点等一次性目标在历史管理器尚未恢复时保留读档值，不倒退为 0。
        progressDictionary[obj] = Mathf.Max(0, newAmount);
    }

    private void RefreshQuestObjectives(QuestSO quest)
    {
        if (quest == null || !questProgress.ContainsKey(quest)) return;
        if (quest.questObjectives == null) return;
        foreach (var objective in quest.questObjectives)
        {
            if (objective != null) UpdateObjectiveProgress(quest, objective);
        }
    }
    public string GetProgressText(QuestSO quest, QuestObjective obj)
    {
        int currentObjAmount = GetCurrentObjAmount(quest, obj);

        if (IsObjDone(quest, obj))
        {
            return "\u221A";//打勾
        }

        else if (obj != null)
            return $"{currentObjAmount}/{obj.requiredAmount}";
        else
            return "进行中";
    }
    public int GetCurrentObjAmount(QuestSO quest, QuestObjective obj)
    {
        if (quest == null || obj == null) return 0;
        if (questProgress.TryGetValue(quest, out var questProgressData))
            if (questProgressData.questObjectives.TryGetValue(obj, out int amount))
                return amount;
        return 0;
    }
    private bool IsAllQuestsCompleted(List<QuestSO> quests)
    {
        foreach (var quest in quests)
        {
            if (quest == null) continue;
            if (!questProgress.ContainsKey(quest)) return false;
            MyEnums.QuestState questState = questProgress[quest].questState;

            if (questState != MyEnums.QuestState.Completed)
                return false;
        }

        return true;
    }
    public void SetQuestSlotToDoneState(QuestSO quest)
    {
        foreach (var questSlot in questLogSlots)
        {
            if (questSlot.currentQuest == quest)
            {
                CanvasGroup questSlotCanvas = questSlot.GetComponent<CanvasGroup>();
                questSlotCanvas.alpha = .2f;
                questSlotCanvas.interactable = false;
                questSlotCanvas.blocksRaycasts = false;

            }
        }
    }
    private bool IsQuestObjDone(QuestSO quest)
    {
        if (quest == null) return false;
        if (!questProgress.ContainsKey(quest)) return false;

        MyEnums.QuestState questState = questProgress[quest].questState;
        if (questState == MyEnums.QuestState.Completed)
            return true;

        if (quest.questObjectives == null) return true;
        foreach (var obj in quest.questObjectives)
        {
            if (obj == null) continue;
            if (!IsObjDone(quest, obj))
                return false;
        }
        return true;
    }


    private bool IsObjDone(QuestSO quest, QuestObjective obj)
    {
        if (obj == null) return true;
        if (GetCurrentObjAmount(quest, obj) < obj.requiredAmount)
            return false;
        else
            return true;
    }
    private void SetQuestCanvasState(bool state)
    {
        ((ICanvasManager)this).ToggleCanvas(questCanvaGroup, canvas, MyEnums.CanvasToToggle.Quest, state);
        canvasIsActive = state;
    }
    private void SetCanvaState(CanvasGroup canva, bool state)
    {
        canva.alpha = state ? 1 : 0;
        canva.blocksRaycasts = state;
        canva.interactable = state;
    }

    public DataDefinition GetDataID()
    {
        // QuestManager 是唯一的全局管理器，数据使用 QuestSO 资源名分项索引。
        return null;
    }

    public void SaveData(Data data)
    {
        if (data == null) return;

        var records = new Dictionary<string, QuestProgressStatus>(StringComparer.Ordinal);
        foreach (var pair in loadedQuestProgress)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
            {
                records[pair.Key] = CloneStatus(pair.Value);
            }
        }

        var savedOwners = new Dictionary<string, QuestSO>(StringComparer.Ordinal);
        foreach (var pair in questProgress)
        {
            QuestSO quest = pair.Key;
            QuestProgressData progress = pair.Value;
            if (quest == null || progress == null) continue;

            string key = GetQuestPersistenceKey(quest);
            if (questOwnersByKey.TryGetValue(key, out QuestSO canonicalOwner)
                && canonicalOwner != null
                && canonicalOwner != quest)
            {
                Debug.LogError($"任务资源名重复，本次跳过重复记录以防串档：{key}");
                continue;
            }
            if (savedOwners.TryGetValue(key, out QuestSO owner) && owner != quest)
            {
                Debug.LogError($"任务资源名重复，本次跳过重复记录以防串档：{key}");
                continue;
            }

            savedOwners[key] = quest;
            records[key] = CreateStatus(quest, progress);
        }

        data.questProgressDic = records;
        ReplaceLoadedRecords(records);
    }

    public void LoadData(Data data)
    {
        var initializedQuests = new List<QuestSO>(questProgress.Keys);
        questProgress.Clear();
        questOwnersByKey.Clear();
        loadedQuestProgress.Clear();

        // 旧存档没有该字段时按“尚未接取任务”处理，且不抛异常。
        if (data?.questProgressDic != null)
        {
            foreach (var pair in data.questProgressDic)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) continue;
                loadedQuestProgress[pair.Key] = SanitizeStatus(pair.Value);
            }
        }

        // LoadFromData 可能发生在任务面板已经初始化以后；立即恢复这些已知 QuestSO。
        InitializeQuestProgress(initializedQuests);
        InitializeQuestProgress(currentBoardLoadQuests);
    }

    private static string GetQuestPersistenceKey(QuestSO quest)
    {
        string assetName = quest != null ? quest.name : null;
        return $"quest:{(string.IsNullOrWhiteSpace(assetName) ? "unnamed" : assetName.Trim())}";
    }

    private static QuestProgressStatus CreateStatus(QuestSO quest, QuestProgressData progress)
    {
        var status = new QuestProgressStatus
        {
            questState = (int)progress.questState,
            objectiveProgress = new List<int>()
        };

        if (quest.questObjectives == null) return status;
        foreach (QuestObjective objective in quest.questObjectives)
        {
            int amount = objective != null
                && progress.questObjectives.TryGetValue(objective, out int savedAmount)
                ? savedAmount
                : 0;
            status.objectiveProgress.Add(Mathf.Max(0, amount));
        }
        return status;
    }

    private static void ApplySavedProgress(
        QuestSO quest,
        QuestProgressData progress,
        QuestProgressStatus status)
    {
        if (status == null) return;

        progress.questState = Enum.IsDefined(typeof(MyEnums.QuestState), status.questState)
            ? (MyEnums.QuestState)status.questState
            : MyEnums.QuestState.Idle;

        if (quest.questObjectives == null || status.objectiveProgress == null) return;
        int count = Math.Min(quest.questObjectives.Count, status.objectiveProgress.Count);
        for (int i = 0; i < count; i++)
        {
            QuestObjective objective = quest.questObjectives[i];
            if (objective != null)
            {
                progress.questObjectives[objective] = Mathf.Max(0, status.objectiveProgress[i]);
            }
        }
    }

    private static QuestProgressStatus SanitizeStatus(QuestProgressStatus status)
    {
        var sanitized = new QuestProgressStatus
        {
            questState = Enum.IsDefined(typeof(MyEnums.QuestState), status.questState)
                ? status.questState
                : (int)MyEnums.QuestState.Idle,
            objectiveProgress = new List<int>()
        };

        if (status.objectiveProgress != null)
        {
            foreach (int amount in status.objectiveProgress)
            {
                sanitized.objectiveProgress.Add(Mathf.Max(0, amount));
            }
        }
        return sanitized;
    }

    private static QuestProgressStatus CloneStatus(QuestProgressStatus status)
    {
        return SanitizeStatus(status);
    }

    private void ReplaceLoadedRecords(Dictionary<string, QuestProgressStatus> records)
    {
        loadedQuestProgress.Clear();
        foreach (var pair in records)
        {
            loadedQuestProgress[pair.Key] = CloneStatus(pair.Value);
        }
    }

}
