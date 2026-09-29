using UnityEngine.UI;
using UnityEngine;

public class ContinueButton : MonoBehaviour
{
    // public DataSaveEventSO loadDataForContinue;

    [SerializeField] private Button continueButton;
    private void OnEnable()
    {
        continueButton.onClick.AddListener(() =>
        {
            string savePath = SaveSystem.Instance.GetLatestLoadableSavePath(MyEnums.SaveType.SystemSave);
            if (string.IsNullOrEmpty(savePath) || !SaveSystem.Instance.LoadSave(MyEnums.SaveType.SystemSave, savePath))
            {
                //TODO加载失败处理
            }
        }
        );
    }
    private void OnDisable()
    {
        continueButton.onClick.RemoveAllListeners();
    }
}
