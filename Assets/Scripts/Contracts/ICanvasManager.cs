using UnityEngine;

public interface ICanvasManager
{
    ToggleCanvasEventSO ToggleCanvasEvent { get; }
    public void ToggleCanvas(
        CanvasGroup canvasGroup,
        Canvas canvas,
        MyEnums.CanvasToToggle canvasToToggle,
        bool state)//统一调用两个函数的接口函数，用于回调画布状态和真正设置优先级 
    {
        SetCanvaState(canvasGroup, canvasToToggle, state);
        RefreshCanvaOrder(canvas, canvasToToggle, state);
        // Debug.Log("toggle&order");
    }
    void SetCanvaState(
          CanvasGroup canva,
          MyEnums.CanvasToToggle canvasToToggle,
          bool state)//如果操作成功了，那就告知manager
    {
        canva.alpha = state ? 1 : 0;
        canva.blocksRaycasts = state;
        canva.interactable = state;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ReportCanvasState(canvasToToggle, state);
        }
    }

    void RefreshCanvaOrder(
       Canvas canvas,
       MyEnums.CanvasToToggle canvasToToggle,
       bool state)
    {
        int order = UIManager.Instance != null
            //按 open-order 链表顺序分配降序排序优先级，顶层（链表末尾）最高
            ? UIManager.Instance.GetCanvasOrder(canvasToToggle, state)
            : UIManager.DefaultOrder;
        if (canvas == null)
        {
            return;
        }

        canvas.sortingOrder = order;
    }



}
