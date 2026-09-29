using System;
using System.Collections.Generic;
using MyEnums;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class UIManager : YSingleton<UIManager>
{
    // 常量保留转发，避免破坏 ICanvasManager.RefreshCanvaOrder 对 UIManager.DefaultOrder 的引用。
    public const int FocusOrder = CanvasFocusStack.FocusOrder;
    public const int DefaultOrder = CanvasFocusStack.DefaultOrder;

    [Header("Events")] [SerializeField] private SceneLoadEventSO loadEventSO;
    [SerializeField] private List<ToggleCanvasEventSO> toggleCanvasEvents;

    [Header("Input Bindings")] [SerializeField]
    private List<CanvasInputBinding> inputBindings;

    // 画布焦点栈：纯 C# 逻辑（open-order 链表、focus、sortingOrder 计算）。
    private readonly CanvasFocusStack stack = new();

    private CanvasToToggle canvasToToggle = CanvasToToggle.Default;

    // 外部（代码/按键）输入合并到这里
    private readonly Dictionary<CanvasToToggle, bool> inputState = new();

    protected override void OnSingletonInitialized()
    {
        foreach (CanvasToToggle canvas in
                 Enum.GetValues(typeof(CanvasToToggle)))
        {
            inputState[canvas] = false;
        }

        // 接线：纯逻辑栈通过回调驱动 SO 事件，保持自身不依赖 ToggleCanvasEventSO。
        stack.OnCanvasToggleRequested = RaiseCanvasEvent;
        stack.OnFocusRefreshRequested = RaiseFocusEvent;
    }

    private void OnEnable()
    {
        loadEventSO.LoadRequestEvent += OnLoadScene;

        foreach (var binding in inputBindings)
        {
            if (binding.action != null) binding.action.action.Enable();
        }
    }

    private void OnDisable()
    {
        loadEventSO.LoadRequestEvent -= OnLoadScene;

        foreach (var binding in inputBindings)
        {
            if (binding.action != null) binding.action.action.Disable();
        }
    }

    private void OnLoadScene(GameSceneSO arg0, Vector3 arg1, bool arg2)
        //UIManager作为跨场景持久单例，不会随场景卸载而disable，因此需要订阅场景加载事件来主动重置画布状态。
        //LoadRequestEvent是同步委托，UIManager的ExecutionOrder(-100)早于SceneChanger，所以OnLoadScene
        //会在SceneChanger开始异步卸载/加载流程之前同步执行，确保所有UI面板在场景过渡动画和旧场景卸载前被关闭。
        //另外也有异步等待操作能为这里争取时间，但是还是要注意可能会导致冲突的时序问题
    {
        ResetCanvas();
    }

    private void Update()
    {
        ToggleCanvas();
    }

    public void HandleFocus(CanvasToToggle canvas) //拖拽脚本的输入，用于完成focus调整
    {
        stack.HandleFocus(canvas);
    }

    // 用于外部切换请求的画布/默认状态。
    public void RequestCanvasToggle(CanvasToToggle canvas)
    {
        if (!inputState.ContainsKey(canvas))
        {
            return;
        }

        inputState[canvas] = true;
    }

    public void RequestCanvasClose(CanvasToToggle canvas)
    {
        stack.RequestClose(canvas);
    }

    // 状态回调，画布报告的真实开启/关闭状态。
    public void ReportCanvasState(CanvasToToggle canvas, bool state)
    {
        stack.ReportState(canvas, state);
    }

    public bool IsCanvasFocused(CanvasToToggle canvas)
    {
        return stack.IsFocused(canvas);
    }

    // 根据 open-order 链表计算排序优先级（转发给焦点栈）。被 ICanvasManager.RefreshCanvaOrder 调用。
    public int GetCanvasOrder(CanvasToToggle canvas, bool state)
    {
        return stack.GetCanvasOrder(canvas, state);
    }

    private void ToggleCanvas()
    {
        EnsureInputStateInitialized();

        foreach (var binding in inputBindings)
        {
            bool pressed = binding.action != null && binding.action.action.WasPressedThisFrame();
            inputState[binding.canvas] = inputState[binding.canvas] || pressed;
        }

        if (inputState[CanvasToToggle.ESC])
        {
            stack.HandleESCOrCloseTop();
            ResetInputState();
            return;
        }

        if (stack.LastOpenCanvas == CanvasToToggle.ESC)
        {
            ResetInputState();
            return;
        }

        canvasToToggle = CanvasToToggle.Default;
        foreach (var binding in inputBindings)
        {
            if (binding.canvas == CanvasToToggle.ESC)
            {
                continue;
            }

            if (inputState[binding.canvas])
            {
                canvasToToggle = binding.canvas;
                break;
            }
        }

        if (canvasToToggle != CanvasToToggle.Default)
        {
            stack.ApplyFocusChange(canvasToToggle);
        }

        ResetInputState();
    }

    private void EnsureInputStateInitialized()
    {
        foreach (CanvasToToggle canvas in Enum.GetValues(typeof(CanvasToToggle)))
        {
            if (!inputState.ContainsKey(canvas))
            {
                inputState[canvas] = false;
            }
        }
    }

    private void RaiseCanvasEvent(CanvasToToggle target, bool state)
    {
        foreach (var eventSO in toggleCanvasEvents)
        {
            if (eventSO.canvasToToggle != target)
            {
                continue;
            }

            eventSO.RaiseToggleCanvasEvent(state);
            return;
        }
    }

    private void RaiseFocusEvent(CanvasToToggle target)
    {
        foreach (var eventSO in toggleCanvasEvents)
        {
            if (eventSO.canvasToToggle == target)
            {
                eventSO.RaiseFocusEvent();
                return;
            }

        }
    }

    private void ResetInputState()
    {
        var keys = new List<CanvasToToggle>(inputState.Keys);
        foreach (var key in keys)
        {
            inputState[key] = false;
        }
    }

    private void ResetCanvas()
    {
        stack.Clear();

        foreach (var eventSO in toggleCanvasEvents)
        {
            eventSO.RaiseToggleCanvasEvent(false);
        }

        ResetInputState();
    }
}

[Serializable]
public class CanvasInputBinding
{
    public CanvasToToggle canvas;
    public InputActionReference action;
}
