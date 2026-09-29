public interface ISaveable
{
    
    DataDefinition GetDataID();
    void RegisterSaveable()
    {
        DataManager.Instance.RegisterSaveableData(this);
    }
    void UnRegisterSaveable()
    {
        DataManager.Instance.UnRegisterSaveableData(this);
    }

    void SaveData(Data data);
    void LoadData(Data data);
}
