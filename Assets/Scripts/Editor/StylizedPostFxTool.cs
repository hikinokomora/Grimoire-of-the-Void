#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// HDRP-oriented one-click setup for moody post-processing + volumetric fog feel.
/// Uses reflection for component fields so it stays resilient across HDRP minor API changes.
/// </summary>
public static class StylizedPostFxTool
{
    private const string ProfileAssetPath = "Assets/Settings/Volumes/Grimoire_Style_GlobalVolumeProfile.asset";

    [MenuItem("Grimoire/🛠 Visual/Setup Stylized PostFX + Volumetrics")]
    public static void SetupStylizedPostFxAndVolumetrics()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[Grimoire Visual] Нет активной загруженной сцены.");
            return;
        }

        EnsureFolder("Assets/Settings");
        EnsureFolder("Assets/Settings/Volumes");

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfileAssetPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfileAssetPath);
            AssetDatabase.SaveAssets();
        }

        Volume volume = FindOrCreateGlobalVolume();
        Undo.RecordObject(volume, "Setup Stylized PostFX Volume");
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 1f;
        volume.sharedProfile = profile;

        List<string> report = new List<string>();
        ConfigurePostFx(profile, report);
        ConfigureVolumetrics(profile, report);

        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(volume);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Grimoire Visual] Готово.\n- " + string.Join("\n- ", report));
    }

    [MenuItem("Grimoire/🛠 Visual/Setup Stylized PostFX + Volumetrics", true)]
    private static bool ValidateSetupStylizedPostFxAndVolumetrics()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static Volume FindOrCreateGlobalVolume()
    {
        Volume existing = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(v => v != null && v.isGlobal);
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject("GlobalVolume_GrimoireStyle");
        Undo.RegisterCreatedObjectUndo(go, "Create Global Volume");
        return go.AddComponent<Volume>();
    }

    private static void ConfigurePostFx(VolumeProfile profile, List<string> report)
    {
        VolumeComponent exposure = GetOrAddComponent(profile, "Exposure");
        if (exposure != null)
        {
            SetEnum(exposure, "mode", "Fixed");
            SetFloat(exposure, "fixedExposure", -0.25f);
            report.Add("Exposure: Fixed -0.25");
        }

        VolumeComponent tonemapping = GetOrAddComponent(profile, "Tonemapping");
        if (tonemapping != null)
        {
            SetEnum(tonemapping, "mode", "ACES");
            report.Add("Tonemapping: ACES");
        }

        VolumeComponent colorAdjustments = GetOrAddComponent(profile, "ColorAdjustments");
        if (colorAdjustments != null)
        {
            SetFloat(colorAdjustments, "postExposure", 0.05f);
            SetFloat(colorAdjustments, "contrast", 8f);
            SetFloat(colorAdjustments, "saturation", -12f);
            report.Add("ColorAdjustments: contrast +8, saturation -12");
        }

        VolumeComponent bloom = GetOrAddComponent(profile, "Bloom");
        if (bloom != null)
        {
            SetFloat(bloom, "threshold", 1.15f);
            SetFloat(bloom, "intensity", 0.2f);
            SetFloat(bloom, "scatter", 0.72f);
            report.Add("Bloom: subtle threshold 1.15");
        }

        VolumeComponent vignette = GetOrAddComponent(profile, "Vignette");
        if (vignette != null)
        {
            SetFloat(vignette, "intensity", 0.17f);
            SetFloat(vignette, "smoothness", 0.6f);
            SetBool(vignette, "rounded", true);
            report.Add("Vignette: intensity 0.17");
        }

        VolumeComponent chromatic = GetOrAddComponent(profile, "ChromaticAberration");
        if (chromatic != null)
        {
            SetFloat(chromatic, "intensity", 0.02f);
            report.Add("ChromaticAberration: 0.02");
        }
    }

    private static void ConfigureVolumetrics(VolumeProfile profile, List<string> report)
    {
        VolumeComponent visualEnvironment = GetOrAddComponent(profile, "VisualEnvironment");
        if (visualEnvironment != null)
        {
            // HDRP enum names can differ, try modern first then fallback.
            if (!SetEnum(visualEnvironment, "fogType", "Volumetric"))
            {
                SetEnum(visualEnvironment, "fogType", "Exponential");
            }
            report.Add("VisualEnvironment: fog enabled");
        }

        VolumeComponent fog = GetOrAddComponent(profile, "Fog");
        if (fog != null)
        {
            SetBool(fog, "enabled", true);
            SetFloat(fog, "meanFreePath", 85f);
            SetFloat(fog, "baseHeight", 0f);
            SetFloat(fog, "maximumHeight", 7f);
            SetFloat(fog, "anisotropy", 0.25f);
            report.Add("Fog: meanFreePath 85, maxHeight 7");
        }
    }

    private static VolumeComponent GetOrAddComponent(VolumeProfile profile, string typeName)
    {
        Type type = TypeCache.GetTypesDerivedFrom<VolumeComponent>().FirstOrDefault(t => t.Name == typeName);
        if (type == null) return null;

        for (int i = 0; i < profile.components.Count; i++)
        {
            VolumeComponent c = profile.components[i];
            if (c != null && c.GetType() == type) return c;
        }

        return profile.Add(type, true);
    }

    private static bool SetFloat(VolumeComponent component, string fieldName, float value)
    {
        FieldInfo f = component.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return false;
        object param = f.GetValue(component);
        if (param == null) return false;

        PropertyInfo valueProp = param.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo overrideProp = param.GetType().GetProperty("overrideState", BindingFlags.Public | BindingFlags.Instance);
        if (valueProp == null) return false;

        valueProp.SetValue(param, value);
        overrideProp?.SetValue(param, true);
        return true;
    }

    private static bool SetBool(VolumeComponent component, string fieldName, bool value)
    {
        FieldInfo f = component.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return false;
        object param = f.GetValue(component);
        if (param == null) return false;

        PropertyInfo valueProp = param.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo overrideProp = param.GetType().GetProperty("overrideState", BindingFlags.Public | BindingFlags.Instance);
        if (valueProp == null) return false;

        valueProp.SetValue(param, value);
        overrideProp?.SetValue(param, true);
        return true;
    }

    private static bool SetEnum(VolumeComponent component, string fieldName, string enumName)
    {
        FieldInfo f = component.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return false;
        object param = f.GetValue(component);
        if (param == null) return false;

        PropertyInfo valueProp = param.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo overrideProp = param.GetType().GetProperty("overrideState", BindingFlags.Public | BindingFlags.Instance);
        if (valueProp == null) return false;

        Type enumType = valueProp.PropertyType;
        if (!enumType.IsEnum) return false;
        object parsed;
        try
        {
            parsed = Enum.Parse(enumType, enumName);
        }
        catch
        {
            return false;
        }

        valueProp.SetValue(param, parsed);
        overrideProp?.SetValue(param, true);
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int slash = path.LastIndexOf('/');
        if (slash <= 0) return;
        string parent = path.Substring(0, slash);
        string child = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
