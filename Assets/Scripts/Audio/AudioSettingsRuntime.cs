using System;
using UnityEngine;

namespace GrimoireOfTheVoid.Audio
{
    public static class AudioSettingsRuntime
    {
        public static event Action<float> SfxVolumeChanged;

        public static float SfxVolume01 { get; private set; } = 1f;

        public static void SetSfxVolume01(float value01)
        {
            float clamped = Mathf.Clamp01(value01);
            if (Mathf.Approximately(SfxVolume01, clamped))
            {
                return;
            }

            SfxVolume01 = clamped;
            SfxVolumeChanged?.Invoke(SfxVolume01);
        }
    }
}

