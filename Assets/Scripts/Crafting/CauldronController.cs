using System.Collections.Generic;
using UnityEngine;

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

        [Header("Текущее состояние")]
        [Tooltip("Список физических объектов аспектов, которые сейчас находятся в котле.")]
        [SerializeField] private List<AspectObject> currentIngredients = new List<AspectObject>();

        [Header("Настройки спавна результата")]
        [Tooltip("Точка, в которой появится новый предмет (например, над котлом).")]
        [SerializeField] private Transform spawnPoint;

        // TODO: Переменные для расширения под таймер
        // [SerializeField] private float defaultCraftTime = 2f;
        // private float currentCraftTimer = 0f;
        // private bool isCrafting = false;

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

            if (availableRecipes == null || availableRecipes.Count == 0)
            {
                Debug.LogWarning("[Cauldron] ОШИБКА: База Рецептов пуста! Вы забыли добавить рецепты в котел (поле Available Recipes).");
                return;
            }

            // Ищем первый рецепт, который совпадает с текущим набором ингредиентов
            foreach (var recipe in availableRecipes)
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
            foreach (var recipe in availableRecipes)
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

            // Загружаем уникальный префаб аспекта по его ID из папки Resources
            AspectObject loadedPrefab = Resources.Load<AspectObject>($"AspectPrefabs/{resultData.ID}");

            if (loadedPrefab != null)
            {
                AspectObject newObject = Instantiate(loadedPrefab, spawnPoint.position, spawnPoint.rotation);
                newObject.aspectData = resultData;
            }
            else
            {
                Debug.LogError($"[Cauldron] Ошибка! Не удалось найти префаб для ID '{resultData.ID}'. " +
                               $"Убедитесь, что префаб лежит ровно по пути: 'Assets/Resources/AspectPrefabs/{resultData.ID}.prefab'");
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
