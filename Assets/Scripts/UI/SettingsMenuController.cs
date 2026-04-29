using GrimoireOfTheVoid.Audio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace GrimoireOfTheVoid.UI
{
    [DisallowMultipleComponent]
    public sealed class SettingsMenuController : MonoBehaviour
    {
        private const string BrightnessKey = "settings_brightness01";
        private const string MusicKey = "settings_music01";
        private const string SfxKey = "settings_sfx01";
        private const string SensitivityKey = "settings_sensitivity01";

        [Header("UI")]
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider sensitivitySlider;

        [Header("Brightness (HDRP)")]
        [SerializeField] private Volume globalVolume;
        [SerializeField] private float minFixedExposure = -2f;
        [SerializeField] private float maxFixedExposure = 2f;

        [Header("Look")]
        [SerializeField] private float minMouseSensitivity = 0.02f;
        [SerializeField] private float maxMouseSensitivity = 0.25f;

        private Exposure _exposure;

        private void Awake()
        {
            EnsureVolumeOverrideReferences();
        }

        private void OnEnable()
        {
            HookSliders(true);
            // Always resync from PlayerPrefs when the settings UI is opened,
            // so menu settings and in-game settings stay consistent.
            LoadSettingsIntoUIAndApply();
        }

        private void OnDisable()
        {
            HookSliders(false);
        }

        private void HookSliders(bool hook)
        {
            if (brightnessSlider != null)
            {
                if (hook) brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
                else brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
            }

            if (musicSlider != null)
            {
                if (hook) musicSlider.onValueChanged.AddListener(OnMusicChanged);
                else musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            }

            if (sfxSlider != null)
            {
                if (hook) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
                else sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            }

            if (sensitivitySlider != null)
            {
                if (hook) sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
                else sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            }
        }

        private void LoadSettingsIntoUIAndApply()
        {
            float b = PlayerPrefs.GetFloat(BrightnessKey, brightnessSlider != null ? brightnessSlider.value : 0.5f);
            float m = PlayerPrefs.GetFloat(MusicKey, musicSlider != null ? musicSlider.value : 0.5f);
            float s = PlayerPrefs.GetFloat(SfxKey, sfxSlider != null ? sfxSlider.value : 0.5f);
            float sens = PlayerPrefs.GetFloat(SensitivityKey, sensitivitySlider != null ? sensitivitySlider.value : 0.5f);

            if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(Mathf.Clamp01(b));
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(Mathf.Clamp01(m));
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(s));
            if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(Mathf.Clamp01(sens));

            ApplyAllFromUI();
        }

        public void RefreshFromPrefs()
        {
            LoadSettingsIntoUIAndApply();
        }

        private void ApplyAllFromUI()
        {
            if (brightnessSlider != null) OnBrightnessChanged(brightnessSlider.value);
            if (musicSlider != null) OnMusicChanged(musicSlider.value);
            if (sfxSlider != null) OnSfxChanged(sfxSlider.value);
            if (sensitivitySlider != null) OnSensitivityChanged(sensitivitySlider.value);
        }

        private void OnBrightnessChanged(float value01)
        {
            PlayerPrefs.SetFloat(BrightnessKey, Mathf.Clamp01(value01));
            PlayerPrefs.Save();

            // Match GlobalSettingsApplier slider convention:
            // slider = 1 -> neutral (0), slider = 0 -> brightest (maxFixedExposure)
            float fixedExposure = Mathf.Lerp(maxFixedExposure, 0f, Mathf.Clamp01(value01));

            if (_exposure != null)
            {
                _exposure.mode.overrideState = true;
                _exposure.mode.Override(ExposureMode.Fixed);
                _exposure.fixedExposure.overrideState = true;
                _exposure.fixedExposure.Override(fixedExposure);
            }

            GlobalSettingsApplier.ApplyNow();
        }

        private void OnMusicChanged(float value01)
        {
            float v = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(MusicKey, v);
            PlayerPrefs.Save();

            MusicManager.Instance?.SetVolume(v);
            GlobalSettingsApplier.ApplyNow();
        }

        private void OnSfxChanged(float value01)
        {
            float v = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(SfxKey, v);
            PlayerPrefs.Save();

            AudioSettingsRuntime.SetSfxVolume01(v);
            GlobalSettingsApplier.ApplyNow();
        }

        private void OnSensitivityChanged(float value01)
        {
            float v = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(SensitivityKey, v);
            PlayerPrefs.Save();

            float sensitivity = Mathf.Lerp(minMouseSensitivity, maxMouseSensitivity, v);

            // BasicMovement currently uses DontDestroyOnLoad, so FindObjectsByType is OK in menu scene.
            var movements = UnityEngine.Object.FindObjectsByType<BasicMovement>(FindObjectsSortMode.None);
            foreach (var m in movements)
            {
                m.SetMouseSensitivity(sensitivity);
            }

            GlobalSettingsApplier.ApplyNow();
        }

        private void EnsureVolumeOverrideReferences()
        {
            if (globalVolume == null)
            {
                return;
            }

            // Use runtime instance (Volume.profile) to avoid mutating the asset (sharedProfile).
            VolumeProfile profile = globalVolume.profile;
            if (profile == null)
            {
                return;
            }

            if (!profile.TryGet(out _exposure) || _exposure == null)
            {
                _exposure = profile.Add<Exposure>();
            }

            _exposure.active = true;
            _exposure.mode.overrideState = true;
            _exposure.mode.Override(ExposureMode.Fixed);
            _exposure.fixedExposure.overrideState = true;
        }
    }
}

