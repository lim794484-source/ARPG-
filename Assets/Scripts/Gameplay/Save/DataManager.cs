using System.Collections.Generic;
using System.Linq;
using UnityEngine;
[DefaultExecutionOrder(-100)]

//TODO使用流程：实现了IS接口的类，以loot为例，在特定时刻使用自己接口的注册方法注册自己到DataManager
//DataManager订阅事件，loot脚本特定时刻拉起事件，此时DataManager负责调用loot实现的对应函数，把数据存进去
//加载和存储基本对称

//Loot脚本不需要自己拉起事件，DataManager会订阅场景加载事件，并且调用已注册的loot的对应函数
public class DataManager : YSingleton<DataManager>
{

    public Data GetData => dataToSave;
    [Header("Send")]
    [SerializeField] private DataSaveEventSO dataSavedEvent;

    [Header("Receive")]
    [SerializeField] private SceneLoadEventSO sceneLoadEventSO;
    [SerializeField] private VoidEventSO sceneLoadedEvent;

    private List<ISaveable> saveables = new();

    private Data dataToSave = new Data();
    // 首次从主菜单进入游戏时不应写系统档，否则场景信息还没准备好。
    private MyEnums.SceneType lastSceneType = MyEnums.SceneType.Menu;
    private void OnEnable()
    {
        sceneLoadEventSO.LoadRequestEvent += OnAutoSave;
        sceneLoadedEvent.VoidEvent += OnAutoLoad;
    }
    private void OnDisable()
    {
        sceneLoadEventSO.LoadRequestEvent -= OnAutoSave;
        sceneLoadedEvent.VoidEvent -= OnAutoLoad;
    }

    public void RegisterSaveableData(ISaveable saveable)
    {
        if (!saveables.Contains(saveable))
        {
            saveables.Add(saveable);
        }
    }

    public void UnRegisterSaveableData(ISaveable saveable)
    {
        saveables.Remove(saveable);
    }
    public void RemoveLootRegistration(string lootId)
    {
        if (string.IsNullOrEmpty(lootId) || dataToSave == null || dataToSave.lootsStatsDic == null)
        {
            return;
        }

        dataToSave.lootsStatsDic.Remove(lootId);
    }
    public bool PrepareManualSaveData()
    {
        GameSceneSO currentScene = SceneChanger.Instance != null ? SceneChanger.Instance.GetCurrentGameScene() : null;
        if (currentScene == null || StatsManager.Instance == null)
        {
            return false;
        }

        dataToSave ??= new Data();
        dataToSave.lootsStatsDic ??= new Dictionary<string, LootStatus>();

        dataToSave.playerStatsData = StatsManager.Instance.GetStats();
        Vector3 savePosition = PlayerController.Instance != null
            ? PlayerController.Instance.GetPosition()
            : currentScene.initialPosition;
        dataToSave.sceneIDAndPlayerPos = new SceneAndPosition(currentScene.ID, savePosition);

        foreach (var saveable in saveables.ToList())
        {
            saveable.SaveData(dataToSave);
        }

        return true;
    }
    private void OnAutoSave(GameSceneSO sceneToLoadSO, Vector3 pos, bool isToFade)
    //关于位置，手动存的时候可以用玩家当前位置取代默认位置，自动存档用的是新位置或者场景默认位置
    {
        // 显式读档也会触发场景加载事件，但此时 dataToSave 已经是刚读出的快照。
        // 如果继续采集当前（即将卸载）场景的数据，会污染快照，并把存档位置改成目标场景初始点；
        // Location -> Location 读档时还会错误地再写一份被覆盖的系统档。
        bool isLoadingSaveRequest = SaveSystem.Instance != null && SaveSystem.Instance.IsLoadingSaveRequest;
        if (isLoadingSaveRequest)
        {
            if (sceneToLoadSO != null)
            {
                lastSceneType = sceneToLoadSO.sceneType;
            }
            return;
        }

        foreach (var saveable in saveables.ToList())
        {
            saveable.SaveData(dataToSave);
        }

        //TODO任务、物品栏、背包。另外，重新开始的时候要删除存下的动态数据

        if (lastSceneType == MyEnums.SceneType.Menu
            && sceneToLoadSO.sceneType == MyEnums.SceneType.Location)
        {
            Vector3 savePosition = pos == Vector3.zero ? sceneToLoadSO.initialPosition : pos;
            StatsManager.Instance.ResetToInitialStats();
            dataToSave = new Data();
            dataToSave.playerStatsData = StatsManager.Instance.GetStats();
            dataToSave.sceneIDAndPlayerPos = new(sceneToLoadSO.ID, savePosition);
            DynamicDataHandler.ClearDynamicData(dataToSave);

            // “新游戏”可能发生在同一次应用会话内。仅替换 Data 并不会清掉常驻场景中
            // Inventory/Quest/Skill/History 等管理器的旧运行态，因此立即把这份空白数据
            // 应用给所有已注册的常驻存档组件；目标场景对象会在加载完成后由 OnAutoLoad 处理。
            foreach (var saveable in saveables.ToList())
            {
                saveable.LoadData(dataToSave);
            }
        }
        else
        {
            dataToSave.playerStatsData = StatsManager.Instance.GetStats();
        }
        //上个场景是Menu则不存档
        if (lastSceneType != MyEnums.SceneType.Menu)
        {
            var sceneChanger = SceneChanger.Instance;
            // 切到新 Location 时沿用目标场景和入口点，退回 Menu 时则记录当前游玩场景。
            GameSceneSO saveScene = sceneToLoadSO.sceneType == MyEnums.SceneType.Location
                ? sceneToLoadSO
                : sceneChanger != null ? sceneChanger.GetCurrentGameScene() : null;
            Vector3 savePosition = sceneToLoadSO.sceneType == MyEnums.SceneType.Location
                ? sceneToLoadSO.initialPosition
                : PlayerController.Instance != null ? PlayerController.Instance.GetPosition() : Vector3.zero;

            if (saveScene != null)
            {
                dataToSave.sceneIDAndPlayerPos = new(saveScene.ID, savePosition);
                dataSavedEvent.RaiseDataSaveEvent(MyEnums.SaveType.SystemSave);
            }
        }
        lastSceneType = sceneToLoadSO.sceneType;
    }

    void OnAutoLoad()
    {
        foreach (var saveable in saveables.ToList())
        {
            saveable.LoadData(dataToSave);
        }
    }

    public void LoadFromData(Data data)//saveSystem调用
    {
        // 兜底防御，避免其他读取入口把空数据直接塞进运行态。
        if (data == null)
        {
            Debug.LogWarning("Skip load because save data is null.");
            return;
        }
        dataToSave = data;
        // 旧档缺少数值时先保留当前运行态，避免把 StatsManager 置空。
        if (data.playerStatsData != null)
        {
            StatsManager.Instance.LoadStats(data.playerStatsData);
        }

        // 技能树等持久组件在 LoadData 时会刷新依赖玩家数值的 UI，必须先恢复 Stats。
        foreach (var saveable in saveables.ToList())//拷贝一份再操作，防止耗时操作中发生时序竞态修改
        {
            saveable.LoadData(dataToSave);
        }

    }


}
