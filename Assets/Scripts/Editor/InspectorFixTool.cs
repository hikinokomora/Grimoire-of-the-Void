#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Small helper for Unity Editor when Inspector gets stuck with null targets.
/// </summary>
public static class InspectorFixTool
{
    [MenuItem("Grimoire/🧹 Fix Inspector Null Targets")]
    public static void FixInspectorNullTargets()
    {
        try
        {
            // Drop any dead references held by Selection/Inspector.
            Selection.objects = Array.Empty<UnityEngine.Object>();
            Selection.activeObject = null;
            Selection.activeGameObject = null;

            // Force all editor views to repaint/reload.
            InternalEditorUtility.RepaintAllViews();

            // Nudge focus away from Inspector.
            EditorApplication.delayCall += () =>
            {
                InternalEditorUtility.RepaintAllViews();
            };

            Debug.Log("<b>[Grimoire]</b> Selection cleared; views repainted. If errors persist: close/reopen Inspector tab or restart Unity.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Grimoire] Failed to fix Inspector targets: " + e.Message);
        }
    }
}
#endif

