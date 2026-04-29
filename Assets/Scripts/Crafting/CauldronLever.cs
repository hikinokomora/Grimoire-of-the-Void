using System.Collections;
using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Физический рычаг на сцене для принудительного сброса содержимого котла.
    /// На рычаге должен быть <see cref="Animator"/> (на том же объекте или на дочернем).
    /// </summary>
    public class CauldronLever : MonoBehaviour
    {
        [Header("Cauldron")]
        [Tooltip("Ссылка на контроллер котла, который нужно очищать.")]
        [SerializeField] private CauldronController cauldron;

        [Header("Audio")]
        [Tooltip("AudioSource для звука рычага. Если пусто — возьмётся с этого объекта или добавится автоматически.")]
        [SerializeField] private AudioSource pullAudioSource;
        [SerializeField] private AudioClip pullAudioClip;
        [SerializeField] [Range(0f, 1f)] private float pullAudioVolume = 0.8f;
        [SerializeField] private bool randomizePullPitch = true;
        [SerializeField] [Min(0.01f)] private float minPullPitch = 0.95f;
        [SerializeField] [Min(0.01f)] private float maxPullPitch = 1.05f;

        [Header("Анимация (один клип дёргания — idle не нужен)")]
        [Tooltip("Animator на рычаге. Можно оставить пустым: возьмётся с этого объекта или с дочерних.")]
        [SerializeField] private Animator leverAnimator;

        [Tooltip("Имя state в Controller (как на оранжевой/любой ноде, например Pull). Обязательно, если нет отдельного Idle. Пусто = клик только через триггер «Pull» в графе.")]
        [SerializeField] private string pullStateName;

        [Tooltip("Только если есть отдельный state покоя. Если нет отдельной idle-анимации — оставь ПУСТЫМ: тогда в начале кадр 0 с speed=0, по клику — проигрывание.")]
        [SerializeField] private string idleStateName;

        [Tooltip("После окончания дёргания снова заморозить на кадре 0 (нужен для зацикленного клипа).")]
        [SerializeField] private bool refreezeAtRestAfterPull = true;

        private const string PullTriggerName = "Pull";

        private void Reset()
        {
            if (pullAudioSource == null)
            {
                pullAudioSource = GetComponent<AudioSource>();
            }
            if (leverAnimator == null)
            {
                leverAnimator = GetComponent<Animator>();
                if (leverAnimator == null)
                {
                    leverAnimator = GetComponentInChildren<Animator>();
                }
            }
        }

        private void Awake()
        {
            if (pullAudioSource == null)
            {
                pullAudioSource = GetComponent<AudioSource>();
            }
            if (pullAudioSource == null)
            {
                pullAudioSource = gameObject.AddComponent<AudioSource>();
                pullAudioSource.playOnAwake = false;
            }

            if (leverAnimator == null)
            {
                leverAnimator = GetComponent<Animator>();
                if (leverAnimator == null)
                {
                    leverAnimator = GetComponentInChildren<Animator>();
                }
            }
        }

        private void Start()
        {
            if (leverAnimator == null) return;

            if (!string.IsNullOrEmpty(idleStateName))
            {
                int idleHash = Animator.StringToHash(idleStateName);
                if (leverAnimator.HasState(0, idleHash))
                {
                    leverAnimator.speed = 1f;
                    leverAnimator.Play(idleStateName, 0, 0f);
                    leverAnimator.Update(0f);
                }
                else
                {
                    Debug.LogWarning($"[CauldronLever] Нет state «{idleStateName}». Проверь имя в Controller.", this);
                }
                return;
            }

            if (string.IsNullOrEmpty(pullStateName)) return;
            StartCoroutine(CoSetupSingleClipNoIdle());
        }

        /// <summary>Один клип, без idle: ждём кадр, чтобы не перебить авто-вход в графе, потом кадр 0 + speed 0.</summary>
        private IEnumerator CoSetupSingleClipNoIdle()
        {
            yield return null;
            if (leverAnimator == null) yield break;
            if (!leverAnimator.HasState(0, Animator.StringToHash(pullStateName)))
            {
                Debug.LogWarning($"[CauldronLever] Нет state «{pullStateName}» на Base Layer. Имя должно совпадать с нодой в Controller.", this);
                yield break;
            }
            leverAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            ApplyFrozenFirstFrame();
        }

        private void ApplyFrozenFirstFrame()
        {
            leverAnimator.speed = 1f;
            leverAnimator.Play(pullStateName, 0, 0f);
            leverAnimator.Update(0f);
            leverAnimator.speed = 0f;
        }

        public void Pull()
        {
            Debug.Log("[Lever] Игрок дёрнул рычаг!");

            if (pullAudioSource != null && pullAudioClip != null)
            {
                if (randomizePullPitch)
                {
                    float a = Mathf.Min(minPullPitch, maxPullPitch);
                    float b = Mathf.Max(minPullPitch, maxPullPitch);
                    pullAudioSource.pitch = Random.Range(a, b);
                }
                else
                {
                    pullAudioSource.pitch = 1f;
                }
                pullAudioSource.PlayOneShot(pullAudioClip, Mathf.Clamp01(pullAudioVolume));
            }

            if (leverAnimator != null)
            {
                if (!string.IsNullOrEmpty(pullStateName))
                {
                    leverAnimator.speed = 1f;
                    leverAnimator.Play(pullStateName, 0, 0f);
                    if (refreezeAtRestAfterPull)
                    {
                        StopAllCoroutines();
                        StartCoroutine(RefreezeAfterPull());
                    }
                }
                else
                {
                    leverAnimator.ResetTrigger(PullTriggerName);
                    leverAnimator.SetTrigger(PullTriggerName);
                }
            }

            if (cauldron != null)
            {
                cauldron.ResetCauldron();
            }
        }

        private IEnumerator RefreezeAfterPull()
        {
            yield return null;
            yield return null;
            float len = leverAnimator.GetCurrentAnimatorStateInfo(0).length;
            if (len < 0.02f) len = 0.4f;
            yield return new WaitForSeconds(len);
            ApplyFrozenFirstFrame();
        }
    }
}
