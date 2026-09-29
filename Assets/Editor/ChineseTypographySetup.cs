#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies a consistent Chinese typography system to translated UI text.
/// Chinese text uses the real Chinese font directly instead of inheriting
/// sizing and material settings from decorative Latin fonts through fallback.
/// </summary>
public static class ChineseTypographySetup
{
    private const string FontAssetPath = "Assets/Sprites/UI/Fonts/ChineseSDF.asset";
    private const string BodyMaterialPath = "Assets/Sprites/UI/Fonts/ChineseSDF UI Body.mat";
    private const string ButtonMaterialPath = "Assets/Sprites/UI/Fonts/ChineseSDF UI Button.mat";
    private const string TitleMaterialPath = "Assets/Sprites/UI/Fonts/ChineseSDF UI Title.mat";

    private enum TextRole
    {
        Body,
        Compact,
        Button,
        Heading,
        Title
    }

    private sealed class TypographyMaterials
    {
        public Material Body;
        public Material Button;
        public Material Title;
    }

    [MenuItem("Tools/中文字体配置/美化全部中文 UI")]
    public static void ApplyTypography()
    {
        TMP_FontAsset chineseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (chineseFont == null || chineseFont.material == null)
        {
            Debug.LogError("找不到可用的 ChineseSDF 字体，请先修复中文字体。");
            return;
        }

        TuneFontAsset(chineseFont);
        TypographyMaterials materials = CreateOrUpdateMaterials(chineseFont);

        int changedPrefabs = ApplyToPrefabs(chineseFont, materials);
        int changedScenes = ApplyToScenes(chineseFont, materials);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"中文 UI 美化完成：已更新 {changedPrefabs} 个预制体、{changedScenes} 个场景。标题、按钮、正文现已使用独立字号、间距和描边层级。");
    }

    private static void TuneFontAsset(TMP_FontAsset fontAsset)
    {
        // SimHei is naturally heavy. These values keep small text crisp while
        // making simulated bold suitable for the hand-painted UI headings.
        fontAsset.normalStyle = -0.06f;
        fontAsset.normalSpacingOffset = 0;
        fontAsset.boldStyle = 0.30f;
        fontAsset.boldSpacing = 2;
        EditorUtility.SetDirty(fontAsset);
    }

    private static TypographyMaterials CreateOrUpdateMaterials(TMP_FontAsset fontAsset)
    {
        return new TypographyMaterials
        {
            Body = CreateOrUpdateMaterial(
                fontAsset,
                BodyMaterialPath,
                "ChineseSDF UI Body",
                faceDilate: -0.06f,
                outlineWidth: 0.025f,
                outlineSoftness: 0.035f,
                outlineColor: new Color(0.08f, 0.065f, 0.09f, 0.58f),
                useUnderlay: false,
                underlayColor: Color.clear,
                underlayOffset: Vector2.zero,
                underlaySoftness: 0),
            Button = CreateOrUpdateMaterial(
                fontAsset,
                ButtonMaterialPath,
                "ChineseSDF UI Button",
                faceDilate: -0.02f,
                outlineWidth: 0.045f,
                outlineSoftness: 0.025f,
                outlineColor: new Color(0.10f, 0.075f, 0.10f, 0.72f),
                useUnderlay: true,
                underlayColor: new Color(0.05f, 0.035f, 0.07f, 0.26f),
                underlayOffset: new Vector2(0.35f, -0.35f),
                underlaySoftness: 0.18f),
            Title = CreateOrUpdateMaterial(
                fontAsset,
                TitleMaterialPath,
                "ChineseSDF UI Title",
                faceDilate: 0,
                outlineWidth: 0.085f,
                outlineSoftness: 0.025f,
                outlineColor: new Color(0.075f, 0.055f, 0.11f, 0.88f),
                useUnderlay: true,
                underlayColor: new Color(0.035f, 0.025f, 0.065f, 0.42f),
                underlayOffset: new Vector2(0.50f, -0.50f),
                underlaySoftness: 0.15f)
        };
    }

    private static Material CreateOrUpdateMaterial(
        TMP_FontAsset fontAsset,
        string path,
        string materialName,
        float faceDilate,
        float outlineWidth,
        float outlineSoftness,
        Color outlineColor,
        bool useUnderlay,
        Color underlayColor,
        Vector2 underlayOffset,
        float underlaySoftness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(fontAsset.material);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = fontAsset.material.shader;
            material.CopyPropertiesFromMaterial(fontAsset.material);
        }

        material.name = materialName;
        material.SetTexture("_MainTex", fontAsset.atlasTexture);
        SetFloat(material, "_FaceDilate", faceDilate);
        SetFloat(material, "_OutlineWidth", outlineWidth);
        SetFloat(material, "_OutlineSoftness", outlineSoftness);
        SetFloat(material, "_Sharpness", 0.12f);
        SetFloat(material, "_WeightNormal", faceDilate);
        SetFloat(material, "_WeightBold", Mathf.Max(0.22f, faceDilate + 0.28f));
        SetColor(material, "_FaceColor", Color.white);
        SetColor(material, "_OutlineColor", outlineColor);
        material.EnableKeyword("OUTLINE_ON");

        if (useUnderlay)
        {
            material.EnableKeyword("UNDERLAY_ON");
            SetColor(material, "_UnderlayColor", underlayColor);
            SetFloat(material, "_UnderlayOffsetX", underlayOffset.x);
            SetFloat(material, "_UnderlayOffsetY", underlayOffset.y);
            SetFloat(material, "_UnderlayDilate", 0);
            SetFloat(material, "_UnderlaySoftness", underlaySoftness);
        }
        else
        {
            material.DisableKeyword("UNDERLAY_ON");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static void SetColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
            material.SetColor(property, value);
    }

    private static int ApplyToPrefabs(TMP_FontAsset fontAsset, TypographyMaterials materials)
    {
        int changedCount = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI" });

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                if (!ApplyToHierarchy(root, path, fontAsset, materials))
                    continue;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                changedCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changedCount;
    }

    private static int ApplyToScenes(TMP_FontAsset fontAsset, TypographyMaterials materials)
    {
        int changedCount = 0;
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });

        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            bool wasDirty = wasLoaded && scene.isDirty;

            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
                changed |= ApplyToHierarchy(root, path, fontAsset, materials);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);

                // Never silently save unrelated edits already present in an open scene.
                if (!wasLoaded || !wasDirty)
                    EditorSceneManager.SaveScene(scene);
                else
                    Debug.LogWarning($"场景 {path} 原本已有未保存改动；中文排版已应用到当前编辑器状态，请手动保存该场景。");

                changedCount++;
            }

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        return changedCount;
    }

    private static bool ApplyToHierarchy(
        GameObject root,
        string assetPath,
        TMP_FontAsset fontAsset,
        TypographyMaterials materials)
    {
        bool changed = false;
        TMP_Text[] textComponents = root.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in textComponents)
        {
            if (!ContainsHanCharacter(text.text))
                continue;

            TextRole role = Classify(text, assetPath);
            bool firstChineseAssignment = text.font != fontAsset;
            Material material = MaterialForRole(role, materials);

            text.font = fontAsset;
            text.fontSharedMaterial = material;
            text.extraPadding = true;
            text.characterSpacing = CharacterSpacingForRole(role);
            text.lineSpacing = role == TextRole.Body ? 7 : role == TextRole.Heading ? 2 : 0;
            text.paragraphSpacing = role == TextRole.Body ? 4 : 0;
            text.fontWeight = WeightForRole(role);
            text.fontStyle = UsesBold(role) ? FontStyles.Bold : FontStyles.Normal;

            if (firstChineseAssignment)
                ResizeForChinese(text, role);

            EditorUtility.SetDirty(text);
            changed = true;
        }

        return changed;
    }

    private static TextRole Classify(TMP_Text text, string assetPath)
    {
        string objectName = text.gameObject.name.ToLowerInvariant();
        string value = StripRichText(text.text).Trim();
        int hanCount = CountHanCharacters(value);

        Transform parent = text.transform.parent;
        bool isButton = assetPath.Replace('\\', '/').Contains("/Buttons/", StringComparison.OrdinalIgnoreCase)
            || text.GetComponent<Button>() != null
            || (parent != null && parent.GetComponent<Button>() != null)
            || objectName.Contains("option");

        if (isButton)
            return TextRole.Button;

        bool titleName = objectName.Contains("title")
            || objectName.Contains("questlog")
            || objectName.Contains("shoptext")
            || objectName.Contains("backpacktext");

        if (titleName || (text.fontSize >= 70 && hanCount <= 8))
            return TextRole.Title;

        bool headingName = objectName.Contains("name")
            || objectName.Contains("statement")
            || objectName.Contains("reward")
            || objectName.Contains("rewad");

        if (headingName || (text.fontSize >= 42 && hanCount <= 8))
            return TextRole.Heading;

        bool bodyName = objectName.Contains("description") || objectName.Contains("dialog");
        if (bodyName || hanCount >= 10)
            return TextRole.Body;

        return TextRole.Compact;
    }

    private static Material MaterialForRole(TextRole role, TypographyMaterials materials)
    {
        switch (role)
        {
            case TextRole.Title:
            case TextRole.Heading:
                return materials.Title;
            case TextRole.Button:
                return materials.Button;
            default:
                return materials.Body;
        }
    }

    private static float CharacterSpacingForRole(TextRole role)
    {
        switch (role)
        {
            case TextRole.Title:
                return 4;
            case TextRole.Heading:
            case TextRole.Button:
                return 2;
            case TextRole.Compact:
                return 0.8f;
            default:
                return 0.4f;
        }
    }

    private static FontWeight WeightForRole(TextRole role)
    {
        switch (role)
        {
            case TextRole.Title:
                return FontWeight.Bold;
            case TextRole.Heading:
            case TextRole.Button:
                return FontWeight.SemiBold;
            default:
                return FontWeight.Regular;
        }
    }

    private static bool UsesBold(TextRole role)
    {
        return role == TextRole.Title || role == TextRole.Heading || role == TextRole.Button;
    }

    private static void ResizeForChinese(TMP_Text text, TextRole role)
    {
        float original = text.fontSize;
        float target = original;

        switch (role)
        {
            case TextRole.Title when original >= 100:
                target = original * 0.72f;
                break;
            case TextRole.Title when original >= 70:
                target = original * 0.82f;
                break;
            case TextRole.Heading when original >= 60:
                target = original * 0.82f;
                break;
            case TextRole.Button when original >= 56:
                target = original * 0.78f;
                break;
            case TextRole.Body when original >= 36:
                target = original * 0.90f;
                break;
        }

        target = Mathf.Round(target * 10) / 10;
        text.fontSize = target;

        if (text.enableAutoSizing)
        {
            text.fontSizeMax = Mathf.Min(text.fontSizeMax, target);
            text.fontSizeMin = Mathf.Min(text.fontSizeMin, text.fontSizeMax);
        }
    }

    private static bool ContainsHanCharacter(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (char character in value)
        {
            if (character >= '\u3400' && character <= '\u9FFF')
                return true;
        }

        return false;
    }

    private static int CountHanCharacters(string value)
    {
        int count = 0;
        foreach (char character in value)
        {
            if (character >= '\u3400' && character <= '\u9FFF')
                count++;
        }

        return count;
    }

    private static string StripRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var result = new System.Text.StringBuilder(value.Length);
        bool insideTag = false;
        foreach (char character in value)
        {
            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (character == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
                result.Append(character);
        }

        return result.ToString();
    }
}
#endif
