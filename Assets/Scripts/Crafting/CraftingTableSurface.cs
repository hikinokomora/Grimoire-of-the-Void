using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Повесь на коллайдер(ы) столешницы, чтобы при отпускании <see cref="AspectObject"/>
    /// вне котла предмет оставался на столе, а не уничтожался/откатывался.
    /// </summary>
    public class CraftingTableSurface : MonoBehaviour
    {
        [Tooltip("Сдвиг вдоль нормали от точки луча (снижает Z-fight со столом).")]
        [SerializeField] private float alongNormalOffset = 0.02f;

        public float AlongNormalOffset => alongNormalOffset;
    }
}
