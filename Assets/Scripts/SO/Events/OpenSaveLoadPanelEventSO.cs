using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OpenSaveLoadPanelEventSO", menuName = "Events/OpenSaveLoadPanelEventSO", order = 0)]
public class OpenSaveLoadPanelEventSO : ScriptableObject
{
    public event Action<MyEnums.SaveLoadPanelType> OpenSaveLoadPanelEvent;

    public void RaiseOpenSaveLoadPanelEvent(MyEnums.SaveLoadPanelType panelType)
    {
        OpenSaveLoadPanelEvent?.Invoke(panelType);
    }
}
