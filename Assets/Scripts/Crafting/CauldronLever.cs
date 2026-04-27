using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Физический рычаг на сцене для принудительного сброса содержимого котла.
    /// </summary>
    public class CauldronLever : MonoBehaviour
    {
        [Tooltip("Ссылка на контроллер котла, который нужно очищать.")]
        [SerializeField] private CauldronController cauldron;

        [Tooltip("Необязательно: Аниматор рычага (если у вас есть анимация дергания).")]
        [SerializeField] private Animator animator;

        /// <summary>
        /// Вызывается системой при клике на рычаг мышкой.
        /// </summary>
        public void Pull()
        {
            Debug.Log("[Lever] Игрок дёрнул рычаг!");
            
            if (animator != null)
            {
                // Если захотите добавить анимацию — заведите в Animator'е триггер "Pull"
                animator.SetTrigger("Pull");
            }

            if (cauldron != null)
            {
                cauldron.ResetCauldron();
            }
        }
    }
}
