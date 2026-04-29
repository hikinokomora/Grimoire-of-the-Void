using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

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

    private bool isSkipped = false;

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

        videoPlayer.loopPointReached += OnVideoFinished;
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
        if (!isSkipped) LoadNextScene();
    }

    public void SkipIntro()
    {
        Debug.Log("⏭ Пропуск интро");
        isSkipped = true;

        if (audioSource != null) audioSource.Stop();
        videoPlayer.loopPointReached -= OnVideoFinished;
        videoPlayer.Stop();

        LoadNextScene();
    }

    private void LoadNextScene() => SceneManager.LoadScene(nextSceneName);
}