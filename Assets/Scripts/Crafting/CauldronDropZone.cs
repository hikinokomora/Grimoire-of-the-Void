using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Маркер для конкретного коллайдера котла: только коллайдеры с этим компонентом
    /// принимают дропнутые аспекты в котёл (иначе большой коллайдер котла может перекрывать стол).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CauldronDropZone : MonoBehaviour
    {
        [Tooltip("Если задано — дроп отправится именно в этот котёл. Иначе будет взят CauldronController в родителях.")]
        [SerializeField] private CauldronController cauldronOverride;

        public CauldronController GetCauldron()
        {
            return cauldronOverride != null ? cauldronOverride : GetComponentInParent<CauldronController>();
        }
    }
}

