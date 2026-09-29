#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Creates and repairs the dynamic Chinese TextMesh Pro fallback font.
/// The previous hand-written .asset had no atlas texture, which caused
/// TMP_FontAsset.TryAddCharacterInternal to throw at runtime.
/// </summary>
public static class ChineseFontSetup
{
    private const string FontPath = "Assets/Sprites/UI/Fonts/simhei.ttf";
    private const string FontAssetPath = "Assets/Sprites/UI/Fonts/ChineseSDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string RepairSessionKey = "ChineseFontSetup.RepairChecked.V2";

    // Every Han character currently used by scenes, prefabs, ScriptableObjects and the guide.
    private const string SmokeTestCharacters =
        "一下不与且丢个为么之乎买了于互些亡交什他以们件任份会伞伤但位作你使保信做儿关兴具内冒几出击刃分切则删到制前剑力功加务动助励勃包化升单南及发取受可右同名后吗吧含味命和咻品哇售商嗯嘿器回围在场城堡士外多大天头奖好如始存它定实害容对导射将尽展属左币帮并店建开弃式弓引弹强当待很得心志怎怒性息情想意感憾戏成我或战所手打扬找技把报抱拒拖拜择括指挥换接推提收数文斗斩新旅无日时是显景暂更有木未本机材束条来板标栏样档桥槽模次歉武死每比汤法消游点焦然煮物现理生用留的目真知砍硬示祝称移程稍站等管箭米系紫红级经结给绝统继续置美老者耐耗聚肉背能自色范菇菜蘑血衣袍被要角解触认让设访试话详说请读谈谢购走趣载达过运近返这进远退送选道遗那部都采里重量金锁错键闭防附除险集需面顶项题验魔鲍黄默";

    [InitializeOnLoadMethod]
    private static void QueueAutomaticRepair()
    {
        if (SessionState.GetBool(RepairSessionKey, false))
            return;

        SessionState.SetBool(RepairSessionKey, true);
        EditorApplication.delayCall += RepairIfNeeded;
    }

    [MenuItem("Tools/中文字体配置/修复并配置中文字体")]
    public static void RepairChineseFont()
    {
        RebuildFontAsset(force: true);
    }

    [MenuItem("Tools/中文字体配置/检查当前配置")]
    public static void CheckConfiguration()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        if (!IsUsable(fontAsset, sourceFont))
        {
            Debug.LogError("中文字体资产无效，请运行 Tools/中文字体配置/修复并配置中文字体。");
            return;
        }

        bool canRender = fontAsset.HasCharacters(SmokeTestCharacters, out List<char> missingCharacters);
        if (!canRender)
        {
            Debug.LogError("中文字体缺少字符: " + new string(missingCharacters.ToArray()));
            return;
        }

        Debug.Log("中文字体配置正常，常用中文字符渲染测试通过。");
    }

    private static void RepairIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RepairIfNeeded;
            return;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        if (IsUsable(fontAsset, sourceFont))
        {
            ConfigureGlobalFallback(fontAsset, null);
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += RepairIfNeeded;
            return;
        }

        Debug.LogWarning("检测到损坏的 ChineseSDF 字体资产，正在自动修复。");
        RebuildFontAsset(force: true);
    }

    private static bool IsUsable(TMP_FontAsset fontAsset, Font sourceFont)
    {
        return sourceFont != null
            && fontAsset != null
            && fontAsset.sourceFontFile == sourceFont
            && fontAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic
            && fontAsset.atlasTextures != null
            && fontAsset.atlasTextures.Length > 0
            && fontAsset.atlasTextures[0] != null
            && fontAsset.material != null;
    }

    private static void RebuildFontAsset(bool force)
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (sourceFont == null)
        {
            Debug.LogError("找不到中文字体文件: " + FontPath);
            return;
        }

        TMP_FontAsset oldFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (!force && IsUsable(oldFontAsset, sourceFont))
        {
            ConfigureGlobalFallback(oldFontAsset, null);
            return;
        }

        try
        {
            RemoveOldFallbackReferences(oldFontAsset);

            if (AssetDatabase.LoadMainAssetAtPath(FontAssetPath) != null
                && !AssetDatabase.DeleteAsset(FontAssetPath))
            {
                throw new InvalidOperationException("无法删除损坏的字体资产: " + FontAssetPath);
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
                throw new InvalidOperationException("TMP 无法从 simhei.ttf 创建字体资产。");

            fontAsset.name = "ChineseSDF";
            fontAsset.atlasTextures[0].name = "ChineseSDF Atlas";
            fontAsset.material.name = "ChineseSDF Atlas Material";

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            ConfigureGlobalFallback(fontAsset, oldFontAsset);

            bool canRender = fontAsset.TryAddCharacters(
                SmokeTestCharacters,
                out string missingCharacters);

            EditorUtility.SetDirty(fontAsset);
            EditorUtility.SetDirty(fontAsset.atlasTextures[0]);
            EditorUtility.SetDirty(fontAsset.material);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

            if (!canRender)
                Debug.LogWarning("中文字体已修复，但以下测试字符不在字体中: " + missingCharacters);
            else
                Debug.Log("中文字体已修复并设为 TMP 全局 fallback；中文渲染测试通过。");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void RemoveOldFallbackReferences(TMP_FontAsset oldFontAsset)
    {
        if (oldFontAsset == null)
            return;

        string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (string guid in fontGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset == null || fontAsset == oldFontAsset)
                continue;

            SerializedObject serializedFont = new SerializedObject(fontAsset);
            bool changed = RemoveReference(serializedFont.FindProperty("fallbackFontAssets"), oldFontAsset);
            changed |= RemoveReference(serializedFont.FindProperty("m_FallbackFontAssetTable"), oldFontAsset);

            if (changed)
            {
                serializedFont.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fontAsset);
            }
        }

        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings != null)
        {
            SerializedObject serializedSettings = new SerializedObject(settings);
            if (RemoveReference(serializedSettings.FindProperty("m_fallbackFontAssets"), oldFontAsset))
            {
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }
        }

        AssetDatabase.SaveAssets();
    }

    private static void ConfigureGlobalFallback(TMP_FontAsset fontAsset, TMP_FontAsset oldFontAsset)
    {
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings == null)
        {
            Debug.LogError("找不到 TMP Settings 资产: " + TmpSettingsPath);
            return;
        }

        SerializedObject serializedSettings = new SerializedObject(settings);
        SerializedProperty fallbacks = serializedSettings.FindProperty("m_fallbackFontAssets");
        if (fallbacks == null)
        {
            Debug.LogError("TMP Settings 中找不到 m_fallbackFontAssets 属性。");
            return;
        }

        if (oldFontAsset != null)
            RemoveReference(fallbacks, oldFontAsset);

        for (int i = fallbacks.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty item = fallbacks.GetArrayElementAtIndex(i);
            if (item.objectReferenceValue == null)
                DeleteArrayElement(fallbacks, i);
        }

        bool alreadyPresent = false;
        for (int i = 0; i < fallbacks.arraySize; i++)
        {
            if (fallbacks.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
            {
                alreadyPresent = true;
                break;
            }
        }

        if (!alreadyPresent)
        {
            int index = fallbacks.arraySize;
            fallbacks.InsertArrayElementAtIndex(index);
            fallbacks.GetArrayElementAtIndex(index).objectReferenceValue = fontAsset;
        }

        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static bool RemoveReference(SerializedProperty array, UnityEngine.Object target)
    {
        if (array == null || !array.isArray)
            return false;

        bool changed = false;
        for (int i = array.arraySize - 1; i >= 0; i--)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == target)
            {
                DeleteArrayElement(array, i);
                changed = true;
            }
        }

        return changed;
    }

    private static void DeleteArrayElement(SerializedProperty array, int index)
    {
        int oldSize = array.arraySize;
        array.DeleteArrayElementAtIndex(index);
        if (array.arraySize == oldSize)
            array.DeleteArrayElementAtIndex(index);
    }
}
#endif
