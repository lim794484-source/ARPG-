using UnityEngine;

public class BackpackCanvasManager : MonoBehaviour, ICanvasManager
{
    [SerializeField] private CanvasGroup currentCanvas;
    [SerializeField] private ToggleCanvasEventSO toggleBackpackCanvasEventSO;

    public ToggleCanvasEventSO ToggleCanvasEvent => toggleBackpackCanvasEventSO;
    private Canvas canvas;

    private void Awake()
    {
        canvas = currentCanvas.GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        toggleBackpackCanvasEventSO.toggleCanvasEvent += OnToggleBackpack;
        toggleBackpackCanvasEventSO.focusEvent += OnFocus;
    }

    private void OnDisable()
    {
        toggleBackpackCanvasEventSO.toggleCanvasEvent -= OnToggleBackpack;
        toggleBackpackCanvasEventSO.focusEvent -= OnFocus;
    }

    private void OnToggleBackpack(bool state)
    {
        ((ICanvasManager)this).ToggleCanvas(currentCanvas, canvas, MyEnums.CanvasToToggle.Backpack, state);
    }

    private void OnFocus()
    {
        ((ICanvasManager)this).RefreshCanvaOrder(canvas, MyEnums.CanvasToToggle.Backpack, true);
    }
}
