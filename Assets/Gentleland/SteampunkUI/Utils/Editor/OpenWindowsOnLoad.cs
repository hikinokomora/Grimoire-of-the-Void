using UnityEditor;
using UnityEngine;
using System.IO;

// if you want to delete this file delete all Gentleland "Utils" folder 
// you can then delete GentlelandSettings folder too
namespace Gentleland.Utils.SteampunkUI
{
    [InitializeOnLoad]
    public static class OpenWindowsOnLoad
    {
        static OpenWindowsOnLoad()
        {
            // #region agent log
            AppendDebugLog("run-build", "H1", "OpenWindowsOnLoad.cs:15", "OpenWindowsOnLoad.cctor.entry", "editor assembly loaded");
            // #endregion
            PackageSettings settings = AssetDatabase.LoadAssetAtPath<PackageSettings>(PackageSettings.PackageSettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(PackageSettings.PackageSettingsFolderPath))
                {
                    AssetDatabase.CreateFolder("Assets",PackageSettings.PackageSettingsFolder);
                }
                settings = ScriptableObject.CreateInstance<PackageSettings>();
                AssetDatabase.CreateAsset(settings, PackageSettings.PackageSettingsPath);
            }
            if (settings.isFirstTimeUsingTheAsset)
            {
                EditorApplication.delayCall += WelcomeWindow.OpenWindow;
                // #region agent log
                AppendDebugLog("run-build", "H3", "OpenWindowsOnLoad.cs:33", "Schedule welcome window", "isFirstTimeUsingTheAsset=true");
                // #endregion
            }
        }

        private static void AppendDebugLog(string runId, string hypothesisId, string location, string message, string data)
        {
            string safeData = (data ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeMessage = message.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeLocation = location.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string payload =
                "{\"sessionId\":\"54daca\",\"runId\":\"" + runId +
                "\",\"hypothesisId\":\"" + hypothesisId +
                "\",\"location\":\"" + safeLocation +
                "\",\"message\":\"" + safeMessage +
                "\",\"data\":{\"note\":\"" + safeData +
                "\"},\"timestamp\":" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
            File.AppendAllText("debug-54daca.log", payload + "\n");
        }
    }
}
