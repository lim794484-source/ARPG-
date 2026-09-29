using System.Collections.Generic;

public static class DynamicDataHandler
{
    public static void PrepareForNewGameLoad(Data data)
    {
        ClearDynamicData(data);
    }

    public static void ClearDynamicData(Data data)
    {
        if (data == null) return;
        data.lootsStatsDic = new Dictionary<string, LootStatus>();
        data.enemiesStatsDic = new Dictionary<string, EnemyStatus>();
    }
}
