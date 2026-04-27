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
    }
}
