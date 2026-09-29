using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveCanvasPanelManager : MonoBehaviour, ICanvasManager
{
    [Header("Events To Receive")]
    [SerializeField] private ToggleCanvasEventSO toggleSaveLoadCanvasEvent;
    public ToggleCanvasEventSO ToggleCanvasEvent => toggleSaveLoadCanvasEvent;

    [Header("UI")]
    [SerializeField] private CanvasGroup saveCanvasGroup;
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject buttonsContent;


    private bool isPanelOpen = false;
    private MyEnums.SaveType saveType = MyEnums.SaveType.PlayerSave;
    private List<SaveLoadButtonGroup> saveLoadButtonGroups = new();
    public class SaveLoadButtonGroup
    {
        public SaveInfo saveInfo;
        public TMP_Text saveInfoText;
        public Button saveButton;
        public TMP_Text saveButtonText;
        public Button loadButton;
        public SaveLoadButtonGroup() { }

    }
    public class SaveInfo

    {
        public string savePath;
        public MyEnums.SaveType saveType;
        public SaveInfo() { }
        public SaveInfo(string savePath, MyEnums.SaveType saveType)
        {
            this.savePath = savePath;
            this.saveType = saveType;
        }

    }
    private void OnEnable()
    {
        toggleSaveLoadCanvasEvent.toggleCanvasEvent += OnToggleCanvas;
        toggleSaveLoadCanvasEvent.focusEvent += OnFocus;
        if (saveLoadButtonGroups.Count == 0)
        {
            LoadButtons();
        }
        LoadInfoToSaveList();
        RefreshSaveButtonState();

    }

    private void OnDisable()
    {
        toggleSaveLoadCanvasEvent.toggleCanvasEvent -= OnToggleCanvas;
        toggleSaveLoadCanvasEvent.focusEvent -= OnFocus;
    }


    private void OnToggleCanvas(bool state)
    {
        if (state)
        {
            LoadInfoToSaveList();
            RefreshSaveButtonState();

            if (!isPanelOpen)
            {
                OpenPanel();
            }
        }
        else
        {
            ClosePanel();
        }
    }

    private void OnFocus()
    {
        if (!isPanelOpen) return;
        ((ICanvasManager)this).RefreshCanvaOrder(canvas, MyEnums.CanvasToToggle.SaveLoad, true);
    }
    string[] files;
    private void LoadInfoToSaveList()
    {
        files = SaveSystem.Instance.GetSavesPath(saveType);

        int i = 0;
        foreach (var group in saveLoadButtonGroups)
        {
            if (i >= files.Length)
            {
                group.saveInfo = new(null,saveType);
                group.saveInfoText.text = "";
                continue;
            }
            else if (files != null)
            {
                group.saveInfo = new(files[i], saveType);
                group.saveInfoText.text = "存档信息\n" + group.saveInfo.savePath;
            }

            i++;
        }
        //按顺序加载到List里
    }

    public void OnClickSave(SaveLoadButtonGroup group)
    //按钮订阅的时候持有自己所在对象的引用，这样可以拿到文本引用
    {
        TMP_Text infoText = group.saveInfoText;
        if (IsMenuScene())
        {
            if (SaveSystem.Instance.DeleteSave(group.saveInfo?.savePath))
            {
                group.saveInfo = new(null, saveType);
                infoText.text = "";
                LoadInfoToSaveList();
                RefreshSaveButtonState();
            }
            return;
        }
        if (group.saveInfo == null)
        {
            group.saveInfo = new(null, saveType);
        }
        if (DataManager.Instance == null || !DataManager.Instance.PrepareManualSaveData())
        {
            Debug.LogWarning("Manual save data is not ready.");
            return;
        }
        string path = SaveSystem.Instance.WriteSave(group.saveInfo.saveType);
        group.saveInfo.savePath=path;
        infoText.text =  "存档信息\n" + path;

    }
    public void OnClickLoad(SaveLoadButtonGroup group)
    {
        SaveSystem.Instance.LoadSave(group.saveInfo.saveType, group.saveInfo.savePath);

    }
    private void LoadButtons()
    {
        saveLoadButtonGroups = new List<SaveLoadButtonGroup>();

        // 遍历 content 下的每个 button 组
        foreach (Transform groupTransform in buttonsContent.transform)
        {
            var buttonsParent = groupTransform.GetChild(1);
            var group = new SaveLoadButtonGroup
            {
                saveInfoText = groupTransform.GetChild(0).GetComponent<TMP_Text>(),
                saveButton = buttonsParent.GetChild(0).GetComponent<Button>(),
                saveButtonText = buttonsParent.GetChild(0).GetComponentInChildren<TMP_Text>(),
                loadButton = buttonsParent.GetChild(1).GetComponent<Button>()
            };
            if (group.saveInfoText == null || group.saveButton == null || group.saveButtonText == null || group.loadButton == null)
            {
                Debug.LogWarning($"SaveLoadButtonGroup 初始化失败: {groupTransform.name}");
                continue;
            }

            saveLoadButtonGroups.Add(group);
            group.saveButton.onClick.AddListener(() => OnClickSave(group));
            group.loadButton.onClick.AddListener(() => OnClickLoad(group));

        }
    }
    private void RefreshSaveButtonState()
    {
        bool isMenuScene = IsMenuScene();
        foreach (var group in saveLoadButtonGroups)
        {
            if (group.saveButtonText != null)
            {
                group.saveButtonText.text = isMenuScene ? "删除" : "保存";
            }
            if (group.saveButton != null)
            {
                group.saveButton.interactable = !isMenuScene || !string.IsNullOrEmpty(group.saveInfo?.savePath);
            }
        }
    }
    private bool IsMenuScene()
    {
        GameSceneSO currentScene = SceneChanger.Instance != null ? SceneChanger.Instance.GetCurrentGameScene() : null;
        return currentScene != null && currentScene.sceneType == MyEnums.SceneType.Menu;
    }
    public void OpenPanel()
    {
        isPanelOpen = true;
        ((ICanvasManager)this).ToggleCanvas(saveCanvasGroup, canvas, MyEnums.CanvasToToggle.SaveLoad, true);
        UIManager.Instance.HandleFocus(MyEnums.CanvasToToggle.SaveLoad);//非键盘按键唤起的画布，直接手动focus
    }

    public void ClosePanel()
    {
        isPanelOpen = false;
        ((ICanvasManager)this).ToggleCanvas(saveCanvasGroup, canvas, MyEnums.CanvasToToggle.SaveLoad, false);
    }
}
