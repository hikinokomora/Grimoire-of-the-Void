using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Представляет физический объект аспекта на сцене (например, колбу на столе), 
    /// который можно положить в котел.
    /// </summary>
    public class AspectObject : MonoBehaviour
    {
        [Tooltip("Данные аспекта, который представляет этот физический объект")]
        public OccultAspect aspectData;

        [Tooltip("Если true, объект не будет уничтожен при добавлении в котел (бесконечный базовый элемент).")]
        public bool isInfiniteSource = false;

        // Здесь в будущем можно добавить физику, скрипты для Drag & Drop, 
        // 3D-эффекты для HDRP, звуки или подсветку при наведении (Highlight).
    }
}
