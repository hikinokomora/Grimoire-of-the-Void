using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class IntroManager : MonoBehaviour
{
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
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
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