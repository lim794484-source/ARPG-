using System;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "DataSaveEventSO", menuName = "Events/DataSaveEventSO", order = 0)]

public class DataSaveEventSO : ScriptableObject
{
    public event Action<MyEnums.SaveType> DataSaveEvent;
    public void RaiseDataSaveEvent(MyEnums.SaveType saveType)
    {
        DataSaveEvent?.Invoke(saveType);
    }
}
