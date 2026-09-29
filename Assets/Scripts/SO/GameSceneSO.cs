using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using MyEnums;
[CreateAssetMenu(fileName = "GameSceneSO", menuName = "GameSceneSO/SceneSO", order = 0)]
public class GameSceneSO : ScriptableObject {
    // 旧资源使用 sceneName 字段；保留迁移标记，避免每次重新导入时生成新的随机 ID，
    // 从而让已经写入存档的场景标识在编辑器重启后失效。
    [FormerlySerializedAs("sceneName")]
    public string ID;
    public  AssetReference sceneReference;
    public SceneType sceneType;
    public Vector3 initialPosition;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(ID))
            ID = System.Guid.NewGuid().ToString();
    }
}
