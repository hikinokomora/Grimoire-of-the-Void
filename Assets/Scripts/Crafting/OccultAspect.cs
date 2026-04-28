using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Базовый элемент крафта (аспект).
    /// </summary>
    [CreateAssetMenu(fileName = "New Occult Aspect", menuName = "Grimoire/Crafting/Occult Aspect")]
    public class OccultAspect : ScriptableObject
    {
        [Tooltip("Уникальный идентификатор аспекта (используется для сравнения в рецептах)")]
        public string ID;

        [Tooltip("Отображаемое имя аспекта в UI")]
        public string DisplayName;

        [Tooltip("Префаб, который появится на сцене при успешном крафте данного аспекта")]
        public AspectObject prefab;

        [Header("Recipe Book Data")]
        [Tooltip("Описание аспекта для книги рецептов")]
        [TextArea(3, 10)]
        public string description;

        [Tooltip("Иконка для страницы в книге")]
        public Sprite aspectIcon;

        [Header("Recipe Book Ingredients")]
        [Tooltip("Состав для крафта (будет скрыт до того, как игрок скрафтит рецепт)")]
        [TextArea(2, 5)]
        public string ingredientsText;

        [Header("Game Goal")]
        [Tooltip("0 = базовый ингредиент (не цель Run). 1–4 = сложность целей для GameDirector.")]
        [Min(0)]
        public int tier = 0;

        [Tooltip("Открыт ли по умолчанию?")]
        public bool isUnlocked = false;
        
        // Текущее состояние открытия, которое живет только пока запущена игра (чтобы не перезаписывать isUnlocked)
        [System.NonSerialized]
        public bool sessionUnlocked = false;
    }
}
