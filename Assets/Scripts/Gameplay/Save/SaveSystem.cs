using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
//TODO每次进入新场景需要自动备份上个场景的信息
//需要备份的信息包括 敌人状态（位置、血量）。物品状态（位置、数量），序列化到json中，下次进场景加载
//点击备份或者退出游戏的时候额外备份当前玩家所在场景、位置、血量
//点击加载的时候加载玩家的手动存档
//玩家可选加载手动存档进度、继续游戏（加载默认存档）、重新游戏（清空状态、直接加载场景，不依赖存档）


//目前存档和加载太频繁，需要批量处理 done


//DataManager做Data对象，此处做info并且打包、写文件进行存档

public class Save
{
    public SaveInfo saveInfo;
    public Data data;
    public Save(SaveInfo saveInfo, Data data)
    {
        this.saveInfo = saveInfo;
        this.data = data;
    }
}
public class SaveSystem : YSingleton<SaveSystem>
{
    public bool IsLoadingSaveRequest { get; private set; }

    [Header("Send")]

    [SerializeField] private SceneLoadEventSO loadEventSO;
    [Header("Receive")]

    [SerializeField] private DataSaveEventSO dataSavedEvent;
    private void OnEnable()
    {
        dataSavedEvent.DataSaveEvent += OnSaveEvent;
    }
    private void OnDisable()
    {
        dataSavedEvent.DataSaveEvent -= OnSaveEvent;
    }
    public void OnSaveEvent(MyEnums.SaveType saveType)//通过事件确认存档：以收到的Data保存完成事件为准（带存档类型）
    {
        WriteSave(saveType);
    }
    public string WriteSave(MyEnums.SaveType saveType)
    {
        string saveID = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        SaveInfo saveInfo = new(saveID, saveType);
        Save save = new(saveInfo, DataManager.Instance.GetData);

        string json = JsonConvert.SerializeObject(save, Formatting.Indented);
        string path=Application.persistentDataPath + $"/{saveType}_{saveID}.json";
        File.WriteAllText(path, json);

        Debug.Log("SaveRoute: " + path);

        return path;

    }
    public bool DeleteSave(string savePath)
    {
        if (string.IsNullOrEmpty(savePath))
        {
            Debug.LogWarning("Save path is empty for delete.");
            return false;
        }

        string fullSavePath = Path.GetFullPath(savePath);
        string fullPersistentPath = Path.GetFullPath(Application.persistentDataPath);
        if (!fullSavePath.StartsWith(fullPersistentPath, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"Save file is outside persistentDataPath: {savePath}");
            return false;
        }

        if (!File.Exists(fullSavePath))
        {
            Debug.LogWarning($"Save file not found: {savePath}");
            return false;
        }

        try
        {
            File.Delete(fullSavePath);
            Debug.Log("DeleteRoute: " + fullSavePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to delete save file {savePath}: {e.Message}");
            return false;
        }
    }


    public bool LoadSave(MyEnums.SaveType saveType, string savePath)
    {
        if (string.IsNullOrEmpty(savePath))
        {
            Debug.LogWarning($"Save path is empty for {saveType}.");
            return false;
        }

        if (!File.Exists(savePath))
        {
            Debug.LogWarning($"Save file not found: {savePath}");
            return false;
        }

        if (!IsLoadableSaveFile(savePath))
        {
            Debug.LogWarning($"Save file is not loadable: {savePath}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            Save save = JsonConvert.DeserializeObject<Save>(json);

            GameSceneSO gameScene = GetScene(save.data);
            if (gameScene != null)
            {
                // 某些旧档只缺位置时，仍允许用场景默认出生点继续游戏。
                Vector3 pos = save.data.sceneIDAndPlayerPos != null && save.data.sceneIDAndPlayerPos.position != null
                    ? save.data.sceneIDAndPlayerPos.position.ToVector3()//如果存档位置为空，会加载到场景的默认位置
                    : gameScene.initialPosition;
                IsLoadingSaveRequest = true;
                try
                {
                    DataManager.Instance.LoadFromData(save.data);
                    loadEventSO.RaiseLoadRequestEvent(gameScene, pos, true);
                }
                finally
                {
                    IsLoadingSaveRequest = false;
                }
            }
            else
            {
                Debug.Log("Failed Load SceneID");
                return false;
            }

            Debug.Log("LoadRoute: " + savePath);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load save file {savePath}: {e.Message}");
        }


        return false;
    }
    public string GetLatestLoadableSavePath(MyEnums.SaveType saveType)
    {
        string[] files = GetSavesPath(saveType);
        if (files.Length == 0)
        {
            Debug.LogWarning("No save files found.");
            return null;
        }

        //文件名格式: {saveType}_{yyyyMMdd_HHmmss_fff}.json，按文件名排序最后一个即最新
        Array.Sort(files);
        // 最新文件可能是坏档，这里回退到最近一个结构完整且能定位场景的档。
        string latestFile = files.LastOrDefault(IsLoadableSaveFile);
        if (string.IsNullOrEmpty(latestFile))
        {
            Debug.LogWarning($"No valid {saveType} save files found.");
            return null;
        }

        return latestFile;
    }
    public string[] GetSavesPath(MyEnums.SaveType saveType)
    {
        return Directory.GetFiles(Application.persistentDataPath, $"{saveType}_*.json");
    }
    private bool IsLoadableSaveFile(string saveFile)
    {
        try
        {
            // Continue 按钮只尝试读取至少包含场景和角色数据的系统档。
            string json = File.ReadAllText(saveFile);
            Save save = JsonConvert.DeserializeObject<Save>(json);
            return save?.data != null
                && save.data.playerStatsData != null
                && GetScene(save.data) != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Skip invalid save file {saveFile}: {e.Message}");
            return false;
        }
    }

    private GameSceneSO GetScene(Data data)
    {
        string sceneID = data?.sceneIDAndPlayerPos?.sceneID;
        if (string.IsNullOrEmpty(sceneID) || SceneDataForSave.Instance == null || SceneDataForSave.Instance.gameScenes == null)
        {
            return null;
        }

        foreach (var scene in SceneDataForSave.Instance.gameScenes.ToList())
        {
            if (scene != null && sceneID == scene.ID) return scene;
        }
        return null;
    }
}
