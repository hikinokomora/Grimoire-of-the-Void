using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PageTurnAudio : MonoBehaviour
{
    [Header("🔊 Звук")]
    public AudioClip pageTurnClip;
    [Range(0f, 1f)] public float volume = 0.5f;

    [Header("🎲 Естественность")]
    [Range(0.8f, 1.0f)] public float minPitch = 0.93f;
    [Range(1.0f, 1.2f)] public float maxPitch = 1.07f;

    [Header("⏱ Защита от спама")]
    public float cooldown = 0.25f;
    private float nextPlayTime;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        //  Для книг/свитков у камеры лучше 2D (не зависит от дистанции)
        // Если книга лежит в мире далеко от игрока → поставь 1f
        audioSource.spatialBlend = 0f;
    }

    // 🖱 Основной метод (вызывай из любого скрипта)
    public void PlayPageTurn()
    {
        if (Time.time < nextPlayTime) return;

        if (pageTurnClip == null)
        {
            Debug.LogWarning($"📖 Нет AudioClip на объекте: {name}");
            return;
        }

        audioSource.volume = volume;
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(pageTurnClip);

        nextPlayTime = Time.time + cooldown;
    }

    // 🎞 Для Animation Event (в момент перелистывания)
    public void PlayOnAnimEvent() => PlayPageTurn();

    // 🖱 Для клика мышкой (если объект кликабельный)
    void OnMouseDown() => PlayPageTurn();
}