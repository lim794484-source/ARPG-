using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

/// <summary>
/// 监听 GameInput.inputactions 被修改/重新导入，自动同步重建 ActionRefs/ 下的所有 InputActionReference 资源。
///
/// 工作流：你在 Input Actions 编辑器窗口里改完 .inputactions（改名、增删 action、改按键），
/// 保存后 Unity 会触发 AssetDatabase 导入 → 本处理器自动重建 ActionRefs/，
/// 场景/prefab 里拖的那些引用靠 GUID 保持不断，新增 action 会自动生成对应 ref 供你拖拽。
///
/// 仅编辑器生效，不会进打包。
/// </summary>
public class InputActionReferenceRebuilder : AssetPostprocessor
{
    private const string INPUT_ACTIONS_PATH = "Assets/Settings/Input/GameInput.inputactions";
    private const string REFS_FOLDER = "Assets/Settings/Input/ActionRefs";

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool needsRebuild = false;
        foreach (var path in importedAssets)
        {
            if (path == INPUT_ACTIONS_PATH) { needsRebuild = true; break; }
        }

        if (!needsRebuild) return;

        RebuildReferences();
    }

    /// <summary>
    /// 根据 GameInput.inputactions 当前的 action 列表，重建 ActionRefs/ 下所有 InputActionReference。
    /// 已存在的 ref（GUID 不变）会被保留并更新指向；新增 action 会生成新 ref；被删 action 的 ref 会被清理。
    /// </summary>
    public static void RebuildReferences()
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(INPUT_ACTIONS_PATH);
        if (asset == null)
        {
            Debug.LogWarning("[InputActionRefRebuilder] 找不到 " + INPUT_ACTIONS_PATH + "，跳过重建。");
            return;
        }

        if (!AssetDatabase.IsValidFolder(REFS_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Settings/Input", "ActionRefs");
        }

        var tRef = typeof(InputActionReference);
        var mCreate = tRef.GetMethod("Create", new System.Type[] { typeof(InputAction) });

        // 收集当前所有 action 的（map+name）→ action
        var desired = new System.Collections.Generic.Dictionary<string, InputAction>();
        foreach (var map in asset.actionMaps)
        {
            foreach (var action in map.actions)
            {
                string refName = map.name + "_" + action.name;
                desired[refName] = action;
            }
        }

        // 先清理：删除 ActionRefs 里不再需要的 ref（对应 action 已被删）
        var existingGuids = AssetDatabase.FindAssets("", new[] { REFS_FOLDER });
        int removed = 0;
        foreach (var guid in existingGuids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.EndsWith(".asset")) continue;

            string existingName = System.IO.Path.GetFileNameWithoutExtension(p);
            if (!desired.ContainsKey(existingName))
            {
                AssetDatabase.DeleteAsset(p);
                removed++;
            }
        }

        // 再重建：对每个当前 action，更新或创建 ref
        int created = 0;
        int updated = 0;
        foreach (var kvp in desired)
        {
            string refPath = REFS_FOLDER + "/" + kvp.Key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<InputActionReference>(refPath);

            if (existing != null)
            {
                existing.Set(kvp.Value);
                EditorUtility.SetDirty(existing);
                updated++;
            }
            else
            {
                var newRef = (InputActionReference)mCreate.Invoke(null, new object[] { kvp.Value });
                AssetDatabase.CreateAsset(newRef, refPath);
                created++;
            }
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[InputActionRefRebuilder] 重建完成：{desired.Count} 个 action，新建 {created} 个 ref，更新 {updated} 个，删除 {removed} 个无效 ref。");
    }
}
