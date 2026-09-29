using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class DataDefinition : MonoBehaviour
{
    public MyEnums.PersistentType persistentType;
    //一个标记字段，用来决定这个 GameObject 是否参与存档
    public string ID;
    private void OnValidate()
    {
        if (persistentType == MyEnums.PersistentType.ReadWrite)
        {
            if (string.IsNullOrEmpty(ID))
            {
                ID = System.Guid.NewGuid().ToString();
            }
        }
        else
        {
            ID = string.Empty;
        }
    }
}
