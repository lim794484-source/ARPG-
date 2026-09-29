using UnityEngine;

public class ButtonSceneToggler : MonoBehaviour
{
    [SerializeField] private SceneLoadEventSO loadEventSO;
    [SerializeField] private GameSceneSO sceneToLoad;
    [SerializeField] private CanvasGroup ButtonCanvas;
    [SerializeField] private Vector3 newPosition;
    [SerializeField] private bool isToFade = true;

    public void HandleSceneToggle()//editor内由button组件绑定
    {
        ButtonCanvas.alpha = 0;
        ButtonCanvas.interactable = false;
        ButtonCanvas.blocksRaycasts = false;

        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, newPosition, isToFade);
    }
}
