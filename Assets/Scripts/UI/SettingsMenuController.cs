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
        [SerializeField] private float minPostExposure = -2f;
        [SerializeField] private float maxPostExposure = 4f;

        [Header("Look")]
        [SerializeField] private float minMouseSensitivity = 0.02f;
        [SerializeField] private float maxMouseSensitivity = 0.25f;

        private ColorAdjustments _colorAdjustments;
        private bool _initialized;

        private void Awake()
        {
            EnsureVolumeOverrideReferences();
        }

        private void OnEnable()
        {
            HookSliders(true);

            if (!_initialized)
            {
                LoadSettingsIntoUIAndApply();
                _initialized = true;
            }
            else
            {
                ApplyAllFromUI();
            }
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

            if (_colorAdjustments == null)
            {
                return;
            }

            float postExposure = Mathf.Lerp(minPostExposure, maxPostExposure, Mathf.Clamp01(value01));
            _colorAdjustments.postExposure.Override(postExposure);
        }

        private void OnMusicChanged(float value01)
        {
            float v = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(MusicKey, v);

            MusicManager.Instance?.SetVolume(v);
        }

        private void OnSfxChanged(float value01)
        {
            float v = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(SfxKey, v);

            AudioSettingsRuntime.SetSfxVolume01(v);
        }

        private void OnSensitivityChanged(float value01)
        {
            float v = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(SensitivityKey, v);

            float sensitivity = Mathf.Lerp(minMouseSensitivity, maxMouseSensitivity, v);

            // BasicMovement currently uses DontDestroyOnLoad, so FindObjectsByType is OK in menu scene.
            var movements = UnityEngine.Object.FindObjectsByType<BasicMovement>(FindObjectsSortMode.None);
            foreach (var m in movements)
            {
                m.SetMouseSensitivity(sensitivity);
            }
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

            if (!profile.TryGet(out _colorAdjustments) || _colorAdjustments == null)
            {
                _colorAdjustments = profile.Add<ColorAdjustments>();
            }

            _colorAdjustments.active = true;
            _colorAdjustments.postExposure.overrideState = true;
        }
    }
}

