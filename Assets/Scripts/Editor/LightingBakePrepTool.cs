#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Safe scene prep helpers for baked GI + reflection probes.
/// Does not start baking automatically; it prepares scene data and logs a summary.
/// </summary>
public static class LightingBakePrepTool
{
    [MenuItem("Grimoire/🛠 Lighting/Analyze Bake Readiness (Current Scene)")]
    public static void AnalyzeBakeReadiness()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[LightingPrep] Нет активной загруженной сцены.");
            return;
        }

        MeshRenderer[] meshRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        ReflectionProbe[] probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        LightProbeGroup[] probeGroups = Object.FindObjectsByType<LightProbeGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int contributeGi = 0;
        int staticLightmap = 0;
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            MeshRenderer mr = meshRenderers[i];
            if (mr == null) continue;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(mr.gameObject);
            if ((flags & StaticEditorFlags.ContributeGI) != 0) contributeGi++;
            if ((flags & StaticEditorFlags.BatchingStatic) != 0 || (flags & StaticEditorFlags.ContributeGI) != 0) staticLightmap++;
        }

        Debug.Log(
            $"[LightingPrep] Scene: {scene.name}\n" +
            $"- MeshRenderers: {meshRenderers.Length}\n" +
            $"- Lights: {lights.Length}\n" +
            $"- ReflectionProbes: {probes.Length}\n" +
            $"- LightProbeGroups: {probeGroups.Length}\n" +
            $"- Renderers with ContributeGI: {contributeGi}\n" +
            $"- Renderers with static-related flags: {staticLightmap}");
    }

    [MenuItem("Grimoire/🛠 Lighting/Prepare Current Scene For Bake")]
    public static void PrepareCurrentSceneForBake()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[LightingPrep] Нет активной загруженной сцены.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Prepare Scene For Light Baking");

        PrepareLightingSettings();
        int markedStatic = MarkStaticContributeGiObjects();
        int configuredLights = ConfigureLightsForBake();
        int configuredReflection = ConfigureReflectionProbes();
        bool createdProbeGroup = EnsureLightProbeGroup();

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

        Debug.Log(
            $"[LightingPrep] Готово для сцены '{scene.name}'.\n" +
            $"- Объектов помечено ContributeGI: {markedStatic}\n" +
            $"- Светов настроено: {configuredLights}\n" +
            $"- ReflectionProbe настроено: {configuredReflection}\n" +
            $"- LightProbeGroup создан: {(createdProbeGroup ? "да" : "нет")}\n" +
            "- Далее открой Window > Rendering > Lighting, проверь параметры и нажми Generate Lighting.");
    }

    private static void PrepareLightingSettings()
    {
        Lightmapping.realtimeGI = false;
        Lightmapping.bakedGI = true;
        LightmapEditorSettings.maxAtlasSize = 2048;
        LightmapEditorSettings.textureCompression = true;
        LightmapEditorSettings.enableAmbientOcclusion = true;
        LightmapEditorSettings.aoMaxDistance = 1.2f;
        LightmapEditorSettings.aoExponentIndirect = 1f;
        LightmapEditorSettings.aoExponentDirect = 0f;
    }

    private static int MarkStaticContributeGiObjects()
    {
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int changed = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer mr = renderers[i];
            if (mr == null) continue;
            GameObject go = mr.gameObject;
            if (ShouldSkipAsDynamic(go)) continue;

            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            StaticEditorFlags desired = flags | StaticEditorFlags.ContributeGI;
            if (desired == flags) continue;

            Undo.RecordObject(go, "Set Contribute GI");
            GameObjectUtility.SetStaticEditorFlags(go, desired);
            changed++;
        }

        return changed;
    }

    private static int ConfigureLightsForBake()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int changed = 0;
        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null) continue;

            // Keep directional light mixed (for nice realtime shadows),
            // set other static decorative lights to baked by default.
            LightmapBakeType target = l.type == LightType.Directional ? LightmapBakeType.Mixed : LightmapBakeType.Baked;
            if (l.lightmapBakeType == target) continue;

            Undo.RecordObject(l, "Configure Light Bake Type");
            l.lightmapBakeType = target;
            changed++;
        }
        return changed;
    }

    private static int ConfigureReflectionProbes()
    {
        ReflectionProbe[] probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int changed = 0;
        for (int i = 0; i < probes.Length; i++)
        {
            ReflectionProbe p = probes[i];
            if (p == null) continue;
            bool touched = false;
            var modeProp = typeof(ReflectionProbe).GetProperty("mode");
            if (modeProp != null)
            {
                object currentMode = modeProp.GetValue(p, null);
                if (currentMode != null)
                {
                    object bakedMode = System.Enum.Parse(currentMode.GetType(), "Baked");
                    if (!Equals(currentMode, bakedMode))
                    {
                        Undo.RecordObject(p, "Set Reflection Probe Baked");
                        modeProp.SetValue(p, bakedMode, null);
                        touched = true;
                    }
                }
            }
            var refreshProp = typeof(ReflectionProbe).GetProperty("refreshMode");
            if (refreshProp != null)
            {
                object currentRefresh = refreshProp.GetValue(p, null);
                if (currentRefresh != null)
                {
                    object viaScripting = System.Enum.Parse(currentRefresh.GetType(), "ViaScripting");
                    if (!Equals(currentRefresh, viaScripting))
                    {
                        Undo.RecordObject(p, "Set Reflection Probe Refresh");
                        refreshProp.SetValue(p, viaScripting, null);
                        touched = true;
                    }
                }
            }
            if (p.resolution < 256)
            {
                Undo.RecordObject(p, "Set Reflection Probe Resolution");
                p.resolution = 256;
                touched = true;
            }
            if (touched) changed++;
        }
        return changed;
    }

    private static bool EnsureLightProbeGroup()
    {
        LightProbeGroup existing = Object.FindFirstObjectByType<LightProbeGroup>();
        if (existing != null) return false;

        Bounds b = CalculateSceneBounds();
        if (b.size.sqrMagnitude <= 0.001f)
        {
            b = new Bounds(Vector3.zero, new Vector3(12f, 4f, 12f));
        }

        GameObject go = new GameObject("AutoLightProbeGroup");
        Undo.RegisterCreatedObjectUndo(go, "Create Light Probe Group");
        LightProbeGroup group = go.AddComponent<LightProbeGroup>();
        group.probePositions = BuildProbeGrid(b, 4, 2, 4);
        return true;
    }

    private static Bounds CalculateSceneBounds()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        bool has = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            if (!has)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return bounds;
    }

    private static Vector3[] BuildProbeGrid(Bounds b, int xCount, int yCount, int zCount)
    {
        List<Vector3> points = new List<Vector3>(xCount * yCount * zCount);
        Vector3 min = b.min;
        Vector3 max = b.max;
        float xStep = xCount > 1 ? (max.x - min.x) / (xCount - 1) : 0f;
        float yStep = yCount > 1 ? (max.y - min.y) / (yCount - 1) : 0f;
        float zStep = zCount > 1 ? (max.z - min.z) / (zCount - 1) : 0f;

        for (int x = 0; x < xCount; x++)
        {
            for (int y = 0; y < yCount; y++)
            {
                for (int z = 0; z < zCount; z++)
                {
                    points.Add(new Vector3(min.x + x * xStep, min.y + y * yStep, min.z + z * zStep));
                }
            }
        }
        return points.ToArray();
    }

    private static bool ShouldSkipAsDynamic(GameObject go)
    {
        if (go == null) return true;
        if (go.GetComponent<Rigidbody>() != null) return true;
        if (go.GetComponent<Animator>() != null) return true;
        if (go.CompareTag("Player")) return true;

        // Skip common gameplay/runtime movers by type name without hard assembly coupling.
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c == null) continue;
            string typeName = c.GetType().Name;
            if (typeName == "AspectObject" ||
                typeName == "BasicMovement" ||
                typeName == "CraftingInteractor" ||
                typeName == "CraftingViewController" ||
                typeName == "CauldronLever")
            {
                return true;
            }
        }

        return false;
    }

}
#endif
