using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Повесь на крупный trigger/коллайдер «войти в режим стола», чтобы луч D&amp;D шёл к колбам, а не застревал на нём
    /// (ищите <see cref="CraftingInteractor"/>: RaycastAll, пропускаются коллайдеры с этой меткой).
    /// </summary>
    public class CraftingTableEntryObstacle : MonoBehaviour
    {
    }
}
