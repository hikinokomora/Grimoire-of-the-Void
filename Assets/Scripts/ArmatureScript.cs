using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class LeverAudio : MonoBehaviour
{
    [Header("🔊 Звук")]
    public AudioClip leverSound;
    [Range(0f, 1f)] public float volume = 0.7f;

    [Header("️ Защита от спама")]
    public float cooldown = 0.8f; // Секунды между повторными активациями
    private float nextPlayTime;
    private bool isReady => Time.time >= nextPlayTime;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D звук (громкость зависит от дистанции)
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 10f;
    }

    // 🖱 Вариант 1: Простой клик мышкой (нужен Collider на рычаге)
    void OnMouseDown()
    {
        if (isReady) PlaySound();
    }

    //  Вариант 2: Вызов из системы взаимодействия игрока
    public void Interact()
    {
        if (isReady) PlaySound();
    }

    private void PlaySound()
    {
        if (leverSound == null)
        {
            Debug.LogWarning($"🔊 Не назначен AudioClip на рычаге: {name}");
            return;
        }

        audioSource.volume = volume;
        // Лёгкий рандом питча, чтобы каждый клик звучал чуть иначе
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(leverSound);

        nextPlayTime = Time.time + cooldown;
    }
}