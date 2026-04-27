using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// IInteractable на коллайдере стола: вход в режим фиксированной камеры (см. <see cref="CraftingViewController"/>).
    /// </summary>
    public class CraftingTableStation : MonoBehaviour, IInteractable
    {
        [Header("View")]
        [SerializeField] private Transform cameraAnchor;
        [Tooltip("Макс. дистанция от игрока до якоря камеры для входа.")]
        [SerializeField] private float maxRange = 4.5f;

        public void Interact()
        {
            if (cameraAnchor == null)
            {
                Debug.LogWarning("[CraftingTableStation] cameraAnchor is not set.", this);
                return;
            }

            var controller = CraftingViewController.Instance
                ? CraftingViewController.Instance
                : Object.FindFirstObjectByType<CraftingViewController>(FindObjectsInactive.Exclude);
            if (controller == null)
            {
                Debug.LogWarning("[CraftingTableStation] No CraftingViewController in scene.");
                return;
            }

            if (CraftingViewController.IsInCraftingView)
            {
                return;
            }

            var movement = controller.GetComponent<BasicMovement>();
            if (movement == null)
            {
                return;
            }
            if (Vector3.Distance(movement.transform.position, cameraAnchor.position) > maxRange)
            {
                return;
            }

            controller.Enter(cameraAnchor);
        }
    }
}
