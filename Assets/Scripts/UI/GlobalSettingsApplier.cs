using GrimoireOfTheVoid.Audio;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace GrimoireOfTheVoid.UI
{
    /// <summary>
    /// Applies saved settings globally across all scenes.
    /// Keep it in a bootstrap scene (menu/intro) or any scene that loads first.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class GlobalSettingsApplier : MonoBehaviour
    {
        private const string BrightnessKey = "settings_brightness01";
        private const string MusicKey = "settings_music01";
        private const string SfxKey = "settings_sfx01";
        private const string SensitivityKey = "settings_sensitivity01";

        [Header("Brightness remap (postExposure)")]
        [SerializeField] private float minPostExposure = -2f;
        [SerializeField] private float maxPostExposure = 4f;

        [Header("Mouse sensitivity remap")]
        [SerializeField] private float minMouseSensitivity = 0.02f;
        [SerializeField] private float maxMouseSensitivity = 0.25f;

        private static GlobalSettingsApplier _instance;
        private Volume _brightnessVolume;
        private VolumeProfile _brightnessProfile;
        private ColorAdjustments _brightnessColorAdjustments;
        private readonly Dictionary<int, float> _sfxBaseVolumes = new Dictionary<int, float>(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject(nameof(GlobalSettingsApplier));
            _instance = go.AddComponent<GlobalSettingsApplier>();
            // Awake() will DontDestroyOnLoad and apply.
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureBrightnessVolume();
            ApplyAllSettings();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Some singletons / objects (MusicManager, player, volumes) can appear later in the frame.
            // Re-apply once after a frame to ensure everything gets the saved values.
            ApplyAllSettings();
            StartCoroutine(ApplyAfterFrame());
        }

        private IEnumerator ApplyAfterFrame()
        {
            yield return null;
            ApplyAllSettings();
        }

        public void ApplyAllSettings()
        {
            ApplyAudio();
            ApplySensitivity();
            ApplyBrightness();
        }

        private void ApplyAudio()
        {
            float music01 = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.5f));
            float sfx01 = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 0.5f));

            AudioSettingsRuntime.SetSfxVolume01(sfx01);
            MusicManager.Instance?.SetVolume(music01);

            ApplySfxToTaggedAudioSources(sfx01);
        }

        private void ApplySensitivity()
        {
            float sens01 = Mathf.Clamp01(PlayerPrefs.GetFloat(SensitivityKey, 0.5f));
            float sens = Mathf.Lerp(minMouseSensitivity, maxMouseSensitivity, sens01);

            var movements = UnityEngine.Object.FindObjectsByType<BasicMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < movements.Length; i++)
            {
                if (movements[i] != null)
                {
                    movements[i].SetMouseSensitivity(sens);
                }
            }
        }

        private void EnsureBrightnessVolume()
        {
            if (_brightnessVolume != null)
            {
                return;
            }

            var go = new GameObject("Global Brightness Volume");
            DontDestroyOnLoad(go);
            go.layer = 0; // Default

            _brightnessVolume = go.AddComponent<Volume>();
            _brightnessVolume.isGlobal = true;
            _brightnessVolume.priority = 49f; // < 50 as requested
            _brightnessVolume.blendDistance = 0f;
            _brightnessVolume.weight = 1f;

            _brightnessProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _brightnessColorAdjustments = _brightnessProfile.Add<ColorAdjustments>();
            _brightnessColorAdjustments.active = true;
            _brightnessColorAdjustments.postExposure.overrideState = true;
            _brightnessVolume.profile = _brightnessProfile;
        }

        private void ApplyBrightness()
        {
            EnsureBrightnessVolume();

            float b01 = Mathf.Clamp01(PlayerPrefs.GetFloat(BrightnessKey, 0.5f));
            float postExposure = Mathf.Lerp(minPostExposure, maxPostExposure, b01);

            if (_brightnessColorAdjustments != null)
            {
                _brightnessColorAdjustments.postExposure.Override(postExposure);
            }
        }

        private void ApplySfxToTaggedAudioSources(float sfx01)
        {
            // Optional workflow: user tags objects with "sfx" and we scale their AudioSources globally.
            GameObject[] tagged;
            try
            {
                tagged = GameObject.FindGameObjectsWithTag("sfx");
            }
            catch
            {
                // Tag not defined or other tag error.
                return;
            }

            for (int i = 0; i < tagged.Length; i++)
            {
                var go = tagged[i];
                if (go == null) continue;

                var sources = go.GetComponentsInChildren<AudioSource>(true);
                for (int s = 0; s < sources.Length; s++)
                {
                    var src = sources[s];
                    if (src == null) continue;

                    int id = src.GetInstanceID();
                    if (!_sfxBaseVolumes.TryGetValue(id, out float baseVol))
                    {
                        baseVol = src.volume;
                        _sfxBaseVolumes[id] = baseVol;
                    }

                    src.volume = baseVol * Mathf.Clamp01(sfx01);
                }
            }
        }
    }
}

