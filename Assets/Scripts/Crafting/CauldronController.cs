using System.Collections.Generic;
using UnityEngine;
using GrimoireOfTheVoid.Game;
namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Контроллер котла (или алтаря) для объединения аспектов по рецептам.
    /// </summary>
    public class CauldronController : MonoBehaviour
    {
        [Header("База данных рецептов")]
        [Tooltip("Список всех доступных рецептов. Назначьте ScriptableObject рецептов в инспекторе.")]
        [SerializeField] private List<Recipe> availableRecipes = new List<Recipe>();

        [Header("Рандомизация рецептов (на забег)")]
        [Tooltip("Если включено — перед началом игры рецепты перемешиваются и (опционально) берётся только подмножество.")]
        [SerializeField] private bool randomizeRecipesPerRun = false;

        [Tooltip("Сколько рецептов активно в забеге. 0 = все. Работает только при включённом Randomize Recipes Per Run.")]
        [SerializeField] [Min(0)] private int activeRecipesPerRun = 0;

        [Header("Авто-сброс несовместимой очередности")]
        [Tooltip("Если включено — когда текущая последовательность ингредиентов уже не может совпасть НИ С ОДНИМ рецептом (даже если добавить ещё), котёл автоматически очищается.")]
        [SerializeField] private bool autoClearOnImpossibleSequence = true;

        [Header("Текущее состояние")]
        [Tooltip("Список физических объектов аспектов, которые сейчас находятся в котле.")]
        [SerializeField] private List<AspectObject> currentIngredients = new List<AspectObject>();

        [Header("Настройки спавна результата")]
        [Tooltip("Точка, в которой появится новый предмет (например, над котлом).")]
        [SerializeField] private Transform spawnPoint;

        [Header("Эффекты")]
        [Tooltip("Эффект дыма (Particle System), который сработает при сбросе котла или неудачном крафте.")]
        [SerializeField] private ParticleSystem resetSmokeEffect;

        [Tooltip("Доп. партиклы «вылет из котла» при дёргании рычага (сброс); проигрываются вместе с дымом, даже если котёл пуст.")]
        [SerializeField] private ParticleSystem[] clearBurstEffects;

        [Tooltip("Опциональная точка спавна VFX (если пусто — используем Spawn Point или позицию котла).")]
        [SerializeField] private Transform vfxAnchor;

        [Tooltip("Масштаб VFX относительно якоря (1 = как в префабе).")]
        [SerializeField] [Min(0.001f)] private float vfxScaleMultiplier = 1f;

        [Tooltip("Локальный оффсет поворота VFX (в градусах) относительно якоря. Используй, если префаб изначально 'лежит' (например -90 по X).")]
        [SerializeField] private Vector3 vfxLocalEulerOffset = Vector3.zero;

        // TODO: Переменные для расширения под таймер
        // [SerializeField] private float defaultCraftTime = 2f;
        // private float currentCraftTimer = 0f;
        // private bool isCrafting = false;

        private readonly List<Recipe> _runtimeRecipes = new List<Recipe>();
        private readonly Dictionary<int, ParticleSystem> _vfxPrefabToInstance = new Dictionary<int, ParticleSystem>(16);

        private void Awake()
        {
            BuildRuntimeRecipes();
        }

        private void BuildRuntimeRecipes()
        {
            _runtimeRecipes.Clear();
            if (availableRecipes == null || availableRecipes.Count == 0)
            {
                return;
            }

            _runtimeRecipes.AddRange(availableRecipes);

            if (!randomizeRecipesPerRun)
            {
                return;
            }

            // Fisher–Yates shuffle
            for (int i = _runtimeRecipes.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_runtimeRecipes[i], _runtimeRecipes[j]) = (_runtimeRecipes[j], _runtimeRecipes[i]);
            }

            if (activeRecipesPerRun > 0 && activeRecipesPerRun < _runtimeRecipes.Count)
            {
                _runtimeRecipes.RemoveRange(activeRecipesPerRun, _runtimeRecipes.Count - activeRecipesPerRun);
            }
        }

        /// <summary>
        /// Добавляет физический аспект в текущий список для крафта.
        /// </summary>
        /// <param name="ingredientItem">Добавляемый объект аспекта</param>
        public void AddIngredient(AspectObject ingredientItem)
        {
            if (ingredientItem == null || ingredientItem.aspectData == null) 
            {
                Debug.LogWarning("[Cauldron] Попытка добавить пустой аспект или объект без данных.");
                return;
            }

            if (ingredientItem.isInfiniteSource)
            {
                // Если это 3D-модель бесконечного источника на столе, создаем его скрытую копию в котле.
                // Так оригинальная колба останется на месте и ее можно будет кликать бесконечно.
                AspectObject virtualCopy = Instantiate(ingredientItem);
                virtualCopy.isInfiniteSource = false; // внутри котла копия уже смертна
                virtualCopy.gameObject.SetActive(false);
                currentIngredients.Add(virtualCopy);
                Debug.Log($"[Cauldron] Добавлена виртуальная копия: {ingredientItem.aspectData.DisplayName}. Всего ингредиентов: {currentIngredients.Count}");
            }
            else
            {
                // Скрываем единичный объект и отбираем его со стола
                currentIngredients.Add(ingredientItem);
                ingredientItem.gameObject.SetActive(false); 
                Debug.Log($"[Cauldron] Добавлен ингредиент: {ingredientItem.aspectData.DisplayName}. Всего ингредиентов: {currentIngredients.Count}");
            }

            // Запускаем проверку один раз после добавления предмета
            CheckAutoCraft();
        }

        /// <summary>
        /// Проверяет, собрался ли подходящий рецепт, и автоматически крафтит результат.
        /// </summary>
        private void CheckAutoCraft()
        {
            if (currentIngredients.Count == 0) return;

            // Извлекаем чистые данные (ScriptableObject) из физических предметов
            List<OccultAspect> aspectDatas = new List<OccultAspect>();
            List<string> debugIDs = new List<string>(); // Для отладки текущих ID
            
            foreach (var item in currentIngredients)
            {
                if (item != null && item.aspectData != null)
                {
                    aspectDatas.Add(item.aspectData);
                    debugIDs.Add(item.aspectData.ID);
                }
            }

            string currentContents = string.Join(" + ", debugIDs);
            Debug.Log($"[Cauldron] Проверка рецептов... В котле лежит: [{currentContents}]");

            if (_runtimeRecipes.Count == 0)
            {
                Debug.LogWarning("[Cauldron] ОШИБКА: База Рецептов пуста! Вы забыли добавить рецепты в котел (поле Available Recipes).");
                return;
            }

            if (autoClearOnImpossibleSequence && !IsCompatiblePrefix(aspectDatas, _runtimeRecipes))
            {
                Debug.Log($"[Cauldron] ✗ Несовместимая последовательность: [{currentContents}]. Котёл очищен автоматически.");
                ClearIngredients();
                PlayResetVisualEffects();
                return;
            }

            // Ищем первый рецепт, который совпадает с текущим набором ингредиентов
            foreach (var recipe in _runtimeRecipes)
            {
                if (recipe == null) continue;

                // Для отладки собираем ID элементов из самого рецепта
                List<string> recipeInputs = new List<string>();
                foreach (var a in recipe.inputs) if (a != null) recipeInputs.Add(a.ID);
                string expectedContents = string.Join(" + ", recipeInputs);

                if (recipe.Matches(aspectDatas))
                {
                    Debug.Log($"[Cauldron] ✓ УСПЕХ! Найден рецепт. Крафтим: {recipe.output.DisplayName}!");
                    SpawnResult(recipe.output);
                    
                    // Разблокировка в реестре и обновление книг (без обязательного AspectManager)
                    OccultAspectRegistry.UnlockAndNotifyUI(recipe.output);

                    // Правила победы / цели на крафт
                    if (GameDirector.Instance != null)
                    {
                        GameDirector.Instance.NotifyAspectCrafted(recipe.output);
                    }
                    
                    // После успешного крафта очищаем котел
                    ClearIngredients();
                    return; // прерываем цикл, так как котел уже очищен
                }
                else if (recipe.inputs.Count <= aspectDatas.Count) // Логируем только если количество уже равно
                {
                    // Если не совпало - пишем точную причину в лог, чтобы игрок/девелопер увидел в чем косяк
                    Debug.Log($"[Cauldron] ✗ Рецепт '{recipe.output?.DisplayName}' не сработал. " +
                              $"Ожидалось строго: [{expectedContents}], а получено: [{currentContents}].");
                }
            }
        }

        private static bool IsCompatiblePrefix(List<OccultAspect> current, List<Recipe> recipes)
        {
            if (current == null || current.Count == 0 || recipes == null || recipes.Count == 0)
            {
                return true;
            }

            for (int r = 0; r < recipes.Count; r++)
            {
                Recipe recipe = recipes[r];
                if (recipe == null || recipe.inputs == null)
                {
                    continue;
                }

                if (recipe.inputs.Count < current.Count)
                {
                    continue; // рецепт короче, уже не может совпасть
                }

                bool prefixOk = true;
                for (int i = 0; i < current.Count; i++)
                {
                    OccultAspect a = current[i];
                    OccultAspect expected = recipe.inputs[i];

                    string idA = a != null && a.ID != null ? a.ID.Trim() : string.Empty;
                    string idE = expected != null && expected.ID != null ? expected.ID.Trim() : string.Empty;

                    if (!string.Equals(idA, idE, System.StringComparison.OrdinalIgnoreCase))
                    {
                        prefixOk = false;
                        break;
                    }
                }

                if (prefixOk)
                {
                    return true; // есть хотя бы один рецепт, который ещё потенциально достижим
                }
            }

            return false;
        }

        /// <summary>
        /// Запускает процесс проверки рецептов вручную (например, по клику), 
        /// очищая котел даже в случае неудачи.
        /// </summary>
        /// <returns>Результирующий аспект, либо null, если комбинация неверна.</returns>
        public OccultAspect TryCraft()
        {
            if (currentIngredients.Count == 0)
            {
                Debug.Log("[Cauldron] Попытка крафта с пустым котлом.");
                return null;
            }

            // Извлекаем чистые данные (ScriptableObject) из физических предметов
            List<OccultAspect> aspectDatas = new List<OccultAspect>();
            foreach(var item in currentIngredients)
            {
                if(item != null && item.aspectData != null)
                    aspectDatas.Add(item.aspectData);
            }

            OccultAspect resultData = null;

            // Ищем первый рецепт, который совпадает с текущим набором ингредиентов
            foreach (var recipe in _runtimeRecipes.Count > 0 ? _runtimeRecipes : availableRecipes)
            {
                if (recipe != null && recipe.Matches(aspectDatas))
                {
                    resultData = recipe.output;
                    break;
                }
            }

            // Логируем результат крафта и спавним новый объект
            if (resultData != null)
            {
                Debug.Log($"[Cauldron] Крафт успешен! Получен результат: {resultData.DisplayName}");
                SpawnResult(resultData);
                
                OccultAspectRegistry.UnlockAndNotifyUI(resultData);

                if (GameDirector.Instance != null)
                {
                    GameDirector.Instance.NotifyAspectCrafted(resultData);
                }
            }
            else
            {
                Debug.Log("[Cauldron] Крафт провален. Подходящий рецепт не найден.");
            }

            // Всегда очищаем котел (уничтожаем старые предметы) после попытки объединения
            ClearIngredients();

            return resultData;
        }

        /// <summary>
        /// Создает новый физический предмет по результатам крафта.
        /// </summary>
        private void SpawnResult(OccultAspect resultData)
        {
            if (spawnPoint == null)
            {
                Debug.LogWarning("[Cauldron] Не настроена точка спавна (Spawn Point) для результата крафта!");
                return;
            }

            if (resultData == null)
            {
                Debug.LogError("[Cauldron] SpawnResult вызван с null resultData.");
                return;
            }

            if (resultData.prefab != null)
            {
                AspectObject newObject = Instantiate(resultData.prefab, spawnPoint.position, spawnPoint.rotation);
                newObject.aspectData = resultData;
                return;
            }

            // Fallback: allow prefabs in Resources/AspectPrefabs/<ID>.prefab
            if (!string.IsNullOrEmpty(resultData.ID))
            {
                GameObject fallback = Resources.Load<GameObject>($"AspectPrefabs/{resultData.ID}");
                if (fallback != null)
                {
                    GameObject go = Instantiate(fallback, spawnPoint.position, spawnPoint.rotation);
                    AspectObject ao = go.GetComponent<AspectObject>() ?? go.GetComponentInChildren<AspectObject>(true);
                    if (ao != null)
                    {
                        ao.aspectData = resultData;
                    }
                    else
                    {
                        Debug.LogWarning($"[Cauldron] Заспавнен prefab из Resources/AspectPrefabs/{resultData.ID}, но на нём нет AspectObject. Крафт продолжен без привязки aspectData.");
                    }
                    return;
                }
            }

            Debug.LogError($"[Cauldron] Ошибка! У аспекта '{resultData.DisplayName}' ({resultData.ID}) не назначен 3D Префаб (поле Prefab в ScriptableObject) и нет Resources/AspectPrefabs/{resultData.ID}.prefab!");
        }

        /// <summary>
        /// Сбрасывает содержимое котла принудительно (вызывается рычагом) и проигрывает дым.
        /// </summary>
        public void ResetCauldron()
        {
            if (currentIngredients.Count > 0)
            {
                ClearIngredients();
            }

            PlayResetVisualEffects();

            Debug.Log("[Cauldron] Котел принудительно сброшен. Пуфф!");
        }

        private void PlayResetVisualEffects()
        {
            PlayOneShotParticles(GetVfxInstance(resetSmokeEffect));

            if (clearBurstEffects == null) return;
            for (int i = 0; i < clearBurstEffects.Length; i++)
            {
                PlayOneShotParticles(GetVfxInstance(clearBurstEffects[i]));
            }
        }

        private ParticleSystem GetVfxInstance(ParticleSystem ps)
        {
            if (ps == null) return null;

            // If it's already a scene instance, use it directly.
            if (ps.gameObject.scene.IsValid())
            {
                return ps;
            }

            // It's a prefab asset reference — instantiate and reuse.
            int key = ps.GetInstanceID();
            if (_vfxPrefabToInstance.TryGetValue(key, out ParticleSystem inst) && inst != null)
            {
                return inst;
            }

            Transform anchor = vfxAnchor != null ? vfxAnchor : (spawnPoint != null ? spawnPoint : transform);
            inst = Instantiate(ps, anchor.position, anchor.rotation);
            inst.name = $"{ps.name}_Instance";
            // Hard-bind to anchor: position/rotation/scale come from the anchor (no world-space drift).
            inst.transform.SetParent(anchor, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(vfxLocalEulerOffset);
            inst.transform.localScale = Vector3.one * vfxScaleMultiplier;
            _vfxPrefabToInstance[key] = inst;
            return inst;
        }

        private static void PlayOneShotParticles(ParticleSystem ps)
        {
            if (ps == null) return;

            // If the effect is disabled in hierarchy, it won't play.
            if (!ps.gameObject.activeInHierarchy)
            {
                ps.gameObject.SetActive(true);
            }

            // Restart reliably even for non-looping systems.
            // Also force unscaled time so it still plays when Time.timeScale == 0 (menus / pauses).
            var main = ps.main;
            main.useUnscaledTime = true;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            // Simulate() is safe for scene instances; prefab assets are filtered out by GetVfxInstance.
            ps.Simulate(0f, true, true, true);
            ps.Play(true);

            if (!ps.isPlaying)
            {
                Debug.LogWarning($"[Cauldron] Reset VFX did not start: {ps.name}. Check Renderer, Emission, and that it's not disabled by Stop Action.", ps);
            }
        }

        /// <summary>
        /// Очищает текущие ингредиенты, уничтожая их физические объекты.
        /// </summary>
        private void ClearIngredients()
        {
            foreach(var item in currentIngredients)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
            currentIngredients.Clear();
            Debug.Log("[Cauldron] Котел очищен. Ингредиенты уничтожены.");
        }
    }
}
