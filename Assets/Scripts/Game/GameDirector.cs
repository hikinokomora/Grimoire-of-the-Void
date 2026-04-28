using System;
using System.Collections.Generic;
using GrimoireOfTheVoid.Crafting;
using UnityEngine;

namespace GrimoireOfTheVoid.Game
{
    /// <summary>
    /// Режиссёр партии: цели по тирам, таймер на каждую цель, публичное API для Таро (бафы/дебафы).
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Header("Goal progression")]
        [Tooltip("Максимальный тир цели (1..maxTier). Аспекты с tier=0 не попадают в пул.")]
        [SerializeField] [Min(1)] private int maxTier = 4;

        [Tooltip("Шанс при выборе цели взять аспект на один тир выше (если в том тире ещё есть цели).")]
        [SerializeField] [Range(0f, 1f)] private float promoteChance = 0.15f;

        [Header("Timer")]
        [Tooltip("Базовое время на цель по тиру аспекта (индекс 0 = тир 1, …).")]
        [SerializeField] private float[] baseTimePerTier = { 60f, 90f, 120f, 180f };

        [SerializeField] private bool autoStartOnAwake = true;

        private readonly Dictionary<int, List<OccultAspect>> _remainingByTier = new Dictionary<int, List<OccultAspect>>();
        private int _phaseTier = 1;

        public OccultAspect CurrentTarget { get; private set; }

        /// <summary>Тир текущей цели (из аспекта).</summary>
        public int CurrentTier => CurrentTarget != null ? CurrentTarget.tier : 0;

        public float TimeRemaining { get; private set; }
        public float MaxTime { get; private set; }
        public bool IsRunning { get; private set; }

        /// <summary>Пауза тика таймера (баф «остановить часы»).</summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Множитель скорости таймера: &gt;1 — время убывает быстрее (дебаф), &lt;1 — медленнее (баф).
        /// </summary>
        public float TimeRateMultiplier { get; set; } = 1f;

        public event Action<OccultAspect> OnGoalChanged;
        public event Action<float, float> OnTimerChanged;
        public event Action<OccultAspect> OnGoalCompleted;
        public event Action OnGameOver;
        public event Action OnVictory;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (autoStartOnAwake)
            {
                RestartRun();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsRunning || IsPaused || CurrentTarget == null)
            {
                return;
            }

            TimeRemaining -= Time.deltaTime * TimeRateMultiplier;
            OnTimerChanged?.Invoke(TimeRemaining, MaxTime);

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                TriggerGameOver();
            }
        }

        /// <summary>Полная пересборка пула и старт с первой целью.</summary>
        public void RestartRun()
        {
            IsPaused = false;
            TimeRateMultiplier = 1f;
            CurrentTarget = null;
            IsRunning = false;

            BuildPoolsFromRegistry();

            if (!TryBeginFirstGoal())
            {
                Debug.LogError("[GameDirector] Нет аспектов с tier >= 1 в каталоге. Заполни Tier у OccultAspect и/или каталог в AspectManager.");
            }
        }

        /// <summary>Старт партии без полной пересборки пула (если autoStartOnAwake выключен).</summary>
        public void StartRunFromExistingPool()
        {
            TryBeginFirstGoal();
        }

        public void AddTime(float seconds)
        {
            TimeRemaining += seconds;
            if (TimeRemaining < 0f)
            {
                TimeRemaining = 0f;
            }

            OnTimerChanged?.Invoke(TimeRemaining, MaxTime);
        }

        public void SetTimeRemaining(float seconds)
        {
            TimeRemaining = Mathf.Max(0f, seconds);
            OnTimerChanged?.Invoke(TimeRemaining, MaxTime);

            if (IsRunning && TimeRemaining <= 0f)
            {
                TriggerGameOver();
            }
        }

        public void SetMaxTimeForCurrentGoal(float seconds, bool refillNow)
        {
            MaxTime = Mathf.Max(0.01f, seconds);
            if (refillNow)
            {
                TimeRemaining = MaxTime;
            }
            else
            {
                TimeRemaining = Mathf.Min(TimeRemaining, MaxTime);
            }

            OnTimerChanged?.Invoke(TimeRemaining, MaxTime);
        }

        /// <summary>
        /// Вызывается из зоны доставки: игрок положил скрафченный аспект в триггер.
        /// </summary>
        public bool NotifyAspectDelivered(OccultAspect aspect)
        {
            if (!IsRunning || CurrentTarget == null || aspect == null)
            {
                return false;
            }

            if (!IdsMatch(aspect, CurrentTarget))
            {
                return false;
            }

            OccultAspect completed = CurrentTarget;
            OnGoalCompleted?.Invoke(completed);

            PickNextGoalOrVictory();
            return true;
        }

        private void BuildPoolsFromRegistry()
        {
            foreach (List<OccultAspect> list in _remainingByTier.Values)
            {
                list.Clear();
            }

            OccultAspectRegistry.EnsureDefaultFromResources();

            IReadOnlyList<OccultAspect> all = OccultAspectRegistry.AllOrdered;
            for (int i = 0; i < all.Count; i++)
            {
                OccultAspect a = all[i];
                if (a == null)
                {
                    continue;
                }

                int t = a.tier;
                if (t < 1 || t > maxTier)
                {
                    continue;
                }

                if (!_remainingByTier.TryGetValue(t, out List<OccultAspect> bucket))
                {
                    bucket = new List<OccultAspect>();
                    _remainingByTier[t] = bucket;
                }

                OccultAspect canonical = OccultAspectRegistry.GetCanonical(a) ?? a;
                if (!ContainsById(bucket, canonical.ID))
                {
                    bucket.Add(canonical);
                }
            }
        }

        private static bool ContainsById(List<OccultAspect> bucket, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i] != null && string.Equals(bucket[i].ID, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryBeginFirstGoal()
        {
            _phaseTier = MinTierWithItems();
            if (_phaseTier < 0)
            {
                return false;
            }

            PickNextGoalOrVictory();
            return CurrentTarget != null;
        }

        private void PickNextGoalOrVictory()
        {
            int nextTier = MinTierWithItems();
            if (nextTier < 0)
            {
                CurrentTarget = null;
                IsRunning = false;
                OnVictory?.Invoke();
                return;
            }

            _phaseTier = nextTier;

            int t = _phaseTier;
            if (UnityEngine.Random.value < promoteChance && t < maxTier && HasAny(t + 1))
            {
                t++;
            }

            if (!HasAny(t))
            {
                t = FirstNonEmptyTierFrom(_phaseTier);
            }

            if (t < 0)
            {
                CurrentTarget = null;
                IsRunning = false;
                OnVictory?.Invoke();
                return;
            }

            OccultAspect goal = PopRandom(t);
            if (goal == null)
            {
                PickNextGoalOrVictory();
                return;
            }

            float duration = GetBaseTimeForAspectTier(goal.tier);
            SetCurrentGoal(goal, duration);
        }

        private void SetCurrentGoal(OccultAspect goal, float durationSeconds)
        {
            CurrentTarget = goal;
            MaxTime = Mathf.Max(0.01f, durationSeconds);
            TimeRemaining = MaxTime;
            IsRunning = true;

            OnGoalChanged?.Invoke(CurrentTarget);
            OnTimerChanged?.Invoke(TimeRemaining, MaxTime);
        }

        private void TriggerGameOver()
        {
            IsRunning = false;
            OnGameOver?.Invoke();
        }

        private float GetBaseTimeForAspectTier(int aspectTier)
        {
            if (baseTimePerTier == null || baseTimePerTier.Length == 0)
            {
                return 60f;
            }

            int idx = Mathf.Clamp(aspectTier - 1, 0, baseTimePerTier.Length - 1);
            return baseTimePerTier[idx];
        }

        private bool HasAny(int tier)
        {
            if (!_remainingByTier.TryGetValue(tier, out List<OccultAspect> list) || list == null)
            {
                return false;
            }

            return list.Count > 0;
        }

        private int MinTierWithItems()
        {
            for (int tier = 1; tier <= maxTier; tier++)
            {
                if (HasAny(tier))
                {
                    return tier;
                }
            }

            return -1;
        }

        private int FirstNonEmptyTierFrom(int startTier)
        {
            for (int tier = Mathf.Max(1, startTier); tier <= maxTier; tier++)
            {
                if (HasAny(tier))
                {
                    return tier;
                }
            }

            return -1;
        }

        private OccultAspect PopRandom(int tier)
        {
            if (!_remainingByTier.TryGetValue(tier, out List<OccultAspect> list) || list == null || list.Count == 0)
            {
                return null;
            }

            int index = UnityEngine.Random.Range(0, list.Count);
            OccultAspect picked = list[index];
            list.RemoveAt(index);
            return picked;
        }

        private static bool IdsMatch(OccultAspect a, OccultAspect b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            string idA = a.ID != null ? a.ID.Trim() : string.Empty;
            string idB = b.ID != null ? b.ID.Trim() : string.Empty;
            return string.Equals(idA, idB, StringComparison.OrdinalIgnoreCase);
        }
    }
}
