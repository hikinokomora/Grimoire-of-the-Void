using UnityEngine;
using GrimoireOfTheVoid.Audio;

[RequireComponent(typeof(AudioSource))]
public class FootstepManager : MonoBehaviour
{
    [Header("Звуки")]
    public AudioClip[] footstepClips; // Перетащи 3-5 разных звуков шагов

    [Header("Настройки")]
    public CharacterController controller; // Если используешь CharacterController
    public float minInterval = 0.35f;      // Интервал при беге (быстро)
    public float maxInterval = 0.75f;      // Интервал при ходьбе (медленно)
    public float walkSpeed = 2f;           // Скорость, соответствующая maxInterval
    public float runSpeed = 5f;            // Скорость, соответствующая minInterval

    private AudioSource audioSource;
    private float stepTimer;
    private float currentInterval;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f; // 3D звук, чтобы громкость зависела от расстояния до камеры
    }

    void Update()
    {
        // Проверяем: на земле ли игрок и двигается ли он
        bool isMoving = controller != null ? controller.velocity.magnitude > 0.1f : false;
        bool isGrounded = controller != null ? controller.isGrounded : true;

        if (isGrounded && isMoving)
        {
            // Считаем фактор скорости (0 = идёт, 1 = бежит)
            float speedFactor = Mathf.InverseLerp(walkSpeed, runSpeed, controller.velocity.magnitude);
            // Чем быстрее бежит -> тем меньше интервал между шагами
            currentInterval = Mathf.Lerp(maxInterval, minInterval, speedFactor);

            stepTimer += Time.deltaTime;
            if (stepTimer >= currentInterval)
            {
                PlayStep();
                stepTimer = 0f;
            }
        }
        else
        {
            stepTimer = 0f; // Сбрасываем таймер, если стоит или в прыжке
        }
    }

    void PlayStep()
    {
        if (footstepClips.Length == 0) return;

        // Случайный клик из массива
        int randomIndex = Random.Range(0, footstepClips.Length);
        // Лёгкая рандомизация тона для естественности
        audioSource.pitch = Random.Range(0.92f, 1.08f);
        float sfxScale = AudioSettingsRuntime.SfxVolume01;
        audioSource.PlayOneShot(footstepClips[randomIndex], sfxScale);
    }
}