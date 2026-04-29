using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;
using GrimoireOfTheVoid.Loading;

public class IntroManager : MonoBehaviour
{
    private const string NextSceneOverrideKey = "menu_nextSceneName";

    [Header("Video")]
    public VideoPlayer videoPlayer;

    [Header("Audio (опционально)")]
    public AudioSource audioSource;
    public float musicVolume = 1f;

    [Header("Сцены")]
    public string nextSceneName = "Scene";

    [Header("Loading overlay (optional)")]
    [Tooltip("Optional overlay to hide 'empty' scene while async loading / warmup runs.")]
    [SerializeField] private LoadingOverlay loadingOverlay;
    [Tooltip("If enabled, temporarily increases async texture upload budget during intro for smoother HDD transitions.")]
    [SerializeField] private bool boostAsyncUploadDuringIntro = true;
    [SerializeField] [Min(1)] private int boostedAsyncUploadTimeSliceMs = 6;
    [SerializeField] [Min(4)] private int boostedAsyncUploadBufferSizeMb = 32;

    private bool isSkipped = false;
    private AsyncOperation _loadOp;
    private bool _isTransitioning;

    private void Start()
    {
        // Проверка VideoPlayer
        if (videoPlayer == null)
        {
            Debug.LogError("❌ VideoPlayer не назначен!");
            return;
        }

        // Allow menu to override the target game scene.
        string overrideName = PlayerPrefs.GetString(NextSceneOverrideKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(overrideName))
        {
            nextSceneName = overrideName;
        }

        // Настройка Audio Source
        if (audioSource == null)
        {
            // Пытаемся найти AudioSource на этом же объекте
            audioSource = GetComponent<AudioSource>();

            // Если нет, создаём новый
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log("✅ Создан новый AudioSource");
            }
        }

        // Настройки AudioSource
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = musicVolume;

        // Привязываем AudioSource к VideoPlayer
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);

        // Запуск видео
        Debug.Log("▶️ Запуск видео...");
        videoPlayer.Play();

        BeginAsyncPreload();

        videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void BeginAsyncPreload()
    {
        if (_loadOp != null || string.IsNullOrWhiteSpace(nextSceneName))
        {
            return;
        }

        // Prioritize background loading during intro (helps HDD).
        Application.backgroundLoadingPriority = ThreadPriority.High;
        if (boostAsyncUploadDuringIntro)
        {
            QualitySettings.asyncUploadTimeSlice = boostedAsyncUploadTimeSliceMs;
            QualitySettings.asyncUploadBufferSize = boostedAsyncUploadBufferSizeMb;
        }

        _loadOp = SceneManager.LoadSceneAsync(nextSceneName);
        if (_loadOp != null)
        {
            _loadOp.allowSceneActivation = false;
        }
    }

    private void Update()
    {
        // Проверяем явные действия: клик, пробел, Enter, Esc
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool enter = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (click || space || enter || escape)
        {
            Debug.Log("⏭ Попытка пропуска инициирована!");
            SkipIntro();
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (!isSkipped) StartSceneTransition();
    }

    public void SkipIntro()
    {
        Debug.Log("⏭ Пропуск интро");
        isSkipped = true;

        if (audioSource != null) audioSource.Stop();
        videoPlayer.loopPointReached -= OnVideoFinished;
        videoPlayer.Stop();

        StartSceneTransition();
    }

    private void StartSceneTransition()
    {
        if (_isTransitioning)
        {
            return;
        }
        _isTransitioning = true;
        StartCoroutine(CoFinishLoadAndActivate());
    }

    private IEnumerator CoFinishLoadAndActivate()
    {
        BeginAsyncPreload();

        if (loadingOverlay != null)
        {
            loadingOverlay.Show();
        }

        // Wait for async load to reach activation-ready state.
        if (_loadOp != null)
        {
            while (_loadOp.progress < 0.9f)
            {
                yield return null;
            }
            _loadOp.allowSceneActivation = true;
        }
        else
        {
            SceneManager.LoadScene(nextSceneName);
            yield break;
        }

        // After activation we warm up and then hide overlay.
        yield return SceneWarmup.WarmupAfterNextSceneLoad();

        if (loadingOverlay != null)
        {
            loadingOverlay.Hide();
        }
    }
}