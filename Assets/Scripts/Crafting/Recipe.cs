using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Рецепт, определяющий комбинацию аспектов для получения нового аспекта.
    /// </summary>
    [CreateAssetMenu(fileName = "New Recipe", menuName = "Grimoire/Crafting/Recipe")]
    public class Recipe : ScriptableObject
    {
        [Tooltip("Список входных аспектов (обычно 2 или 3). ВАЖНО: Порядок ингредиентов ИМЕЕТ значение!")]
        public List<OccultAspect> inputs = new List<OccultAspect>();

        [Tooltip("Результат успешного крафта")]
        public OccultAspect output;

        /// <summary>
        /// Проверяет, совпадает ли переданный список ингредиентов с требуемым для рецепта.
        /// ВАЖНО: Порядок ингредиентов ИМЕЕТ значение!
        /// </summary>
        /// <param name="ingredients">Текущие ингредиенты в котле</param>
        /// <returns>True, если рецепт совпадает</returns>
        public bool Matches(List<OccultAspect> ingredients)
        {
            // Быстрая проверка на совпадение количества и null
            if (ingredients == null || inputs == null || ingredients.Count != inputs.Count) 
                return false;

            // Извлекаем ID в изначальном порядке, приводя к нижнему регистру и убирая лишние пробелы.
            // Это спасет от ошибок, если в Unity вы случайно написали "Bone " вместо "Bone"
            var expectedIDs = inputs
                .Where(x => x != null)
                .Select(x => x.ID.Trim().ToLower());

            var actualIDs = ingredients
                .Where(x => x != null)
                .Select(x => x.ID.Trim().ToLower());

            // Метод SequenceEqual проверит точное совпадение не только самих элементов, но и строго их порядка
            return expectedIDs.SequenceEqual(actualIDs);
        }
    }
}
