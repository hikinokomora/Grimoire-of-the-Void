using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }
    private AudioSource _audioSource;

    private void Awake()
    {
        // Защита от дублей при перезагрузке сцен
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 🔑 Не уничтожать при смене сцен

        _audioSource = GetComponent<AudioSource>();
    }

    /// Громкость от 0.0 до 1.0
    public void SetVolume(float value)
    {
        if (_audioSource != null)
            _audioSource.volume = Mathf.Clamp01(value);
    }

    public void ToggleMute(bool muted) => SetVolume(muted ? 0f : 1f);
    public void Pause() => _audioSource?.Pause();
    public void Resume() => _audioSource?.UnPause();
}