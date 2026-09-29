using UnityEngine;

/// <summary>
/// GameOver 画布管理器，结构和 ESCMenuManager 一致。
/// 通过 ToggleCanvasEventSO(GameOver) 接收开关指令，
/// 由 UIManager 的画布调度系统统一触发，不依赖外部直接引用。
/// </summary>
public class GameOverCanvasManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup gameOverGroup;
    [SerializeField] private ToggleCanvasEventSO toggleGameOverEvent;

    private void OnEnable()
    {
        if (toggleGameOverEvent != null)
            toggleGameOverEvent.toggleCanvasEvent += OnGameOver;
    }

    private void OnDisable()
    {
        if (toggleGameOverEvent != null)
            toggleGameOverEvent.toggleCanvasEvent -= OnGameOver;
    }

    private void OnGameOver(bool state)
    {
        if (state)
        {
            TimeManager.Instance.PauseGame();
            gameOverGroup.alpha = 1;
            gameOverGroup.interactable = true;
            gameOverGroup.blocksRaycasts = true;
        }
        else
        {
            gameOverGroup.alpha = 0;
            gameOverGroup.interactable = false;
            gameOverGroup.blocksRaycasts = false;
        }

    }
}
