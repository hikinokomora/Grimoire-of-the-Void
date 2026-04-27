using UnityEngine;
using UnityEngine.InputSystem;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Контроллер взаимодействия игрока с объектами для крафта.
    /// Обрабатывает Drag-and-Drop (перетаскивание колб) в котел.
    /// </summary>
    public class CraftingInteractor : MonoBehaviour
    {
        [Tooltip("Ссылка на контроллер котла на сцене")]
        [SerializeField] private CauldronController cauldron;

        [Tooltip("Резерв: пересечение с плоскостью на этом расстоянии вдоль взгляда, если луч не попал в горизонтальную плоскость (X-Z)")]
        [SerializeField] private float dragDistance = 3f;

        private Camera mainCamera;
        
        private AspectObject draggedObject;
        private Vector3 originalPosition;
        private bool wasCloned;
        private Plane dragPlane; // Горизонтальная плоскость для перетаскивания
        private bool suppressThisUpdate;

        private void Awake()
        {
            TryRefreshMainCamera();
        }

        private void TryRefreshMainCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        /// <summary>Игнорирует один кадр ввода после активации (тот же клик, что открыл режим стола).</summary>
        public void RequestSuppressNextInput()
        {
            suppressThisUpdate = true;
        }

        private void Update()
        {
            if (suppressThisUpdate)
            {
                suppressThisUpdate = false;
                return;
            }
            if (Mouse.current == null)
            {
                return;
            }

            TryRefreshMainCamera();
            Vector2 mousePos = Mouse.current.position.ReadValue();
            var left = Mouse.current.leftButton;

            // wasPressed / isPressed / wasReleased в одной цепи if-else: после suppress удержанная ЛКМ
            // не даёт wasPressed, и взятие с первого кадра не срабатывало.
            if (left.wasPressedThisFrame)
            {
                TryPickUp(mousePos);
            }

            if (draggedObject != null)
            {
                if (left.isPressed)
                {
                    Drag(mousePos);
                }
                if (left.wasReleasedThisFrame)
                {
                    TryDrop(mousePos);
                }
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                TryClickCauldronForCrafting(mousePos);
            }
        }

        private void TryPickUp(Vector2 screenPosition)
        {
            TryRefreshMainCamera();
            if (mainCamera == null)
            {
                Debug.LogError("[DragAndDrop] ОШИБКА: Не найдена MainCamera! Убедитесь, что на вашей камере стоит тег 'MainCamera'.");
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            TryProcessPickRay(ray);
        }

        private void TryProcessPickRay(Ray ray)
        {
            int layerMask = Physics.DefaultRaycastLayers;
            int count = Physics.RaycastNonAlloc(ray, RaycastBuffer, 1000f, layerMask, QueryTriggerInteraction.Collide);
            if (count == 0) return;

            SortRaycastBufferByDistance(count);
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = RaycastBuffer[i];
                if (ShouldSkipForCraftingDrag(h)) continue;

                if (h.collider.TryGetComponent<CauldronLever>(out CauldronLever lever))
                {
                    lever.Pull();
                    return;
                }
                if (h.collider.TryGetComponent<PhysicalBookButton>(out PhysicalBookButton bookButton))
                {
                    bookButton.ForceClick();
                    return;
                }
                if (h.collider.TryGetComponent<AspectObject>(out AspectObject aspect))
                {
                    if (aspect.isInfiniteSource)
                    {
                        draggedObject = Instantiate(aspect, aspect.transform.position, aspect.transform.rotation);
                        draggedObject.isInfiniteSource = false;
                        wasCloned = true;
                    }
                    else
                    {
                        draggedObject = aspect;
                        originalPosition = aspect.transform.position;
                        wasCloned = false;
                    }
                    if (draggedObject.TryGetComponent<Collider>(out Collider col))
                    {
                        col.enabled = false;
                    }
                    dragPlane = new Plane(Vector3.up, draggedObject.transform.position);
                    return;
                }
            }
        }

        private static void SortRaycastBufferByDistance(int count)
        {
            for (int a = 0; a < count - 1; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    if (RaycastBuffer[b].distance < RaycastBuffer[a].distance)
                    {
                        (RaycastBuffer[a], RaycastBuffer[b]) = (RaycastBuffer[b], RaycastBuffer[a]);
                    }
                }
            }
        }

        private static bool ShouldSkipForCraftingDrag(RaycastHit h)
        {
            Collider c = h.collider;
            if (c == null) return true;
            if (c.GetComponent<CraftingTableEntryObstacle>() != null)
            {
                return true;
            }
            // Тот же GameObject, что и CraftingTableStation — зона клика «сесть к столу», а не лут
            if (c.GetComponent<CraftingTableStation>() != null)
            {
                return true;
            }
            return false;
        }

        private static readonly RaycastHit[] RaycastBuffer = new RaycastHit[32];

        private void Drag(Vector2 screenPosition)
        {
            if (draggedObject == null || mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (dragPlane.Raycast(ray, out float distance))
            {
                draggedObject.transform.position = ray.GetPoint(distance);
                return;
            }

            Vector3 planePoint = mainCamera.transform.position + mainCamera.transform.forward * dragDistance;
            var depthPlane = new Plane(-mainCamera.transform.forward, planePoint);
            if (depthPlane.Raycast(ray, out float d2))
            {
                draggedObject.transform.position = ray.GetPoint(d2);
            }
        }

        private void TryDrop(Vector2 screenPosition)
        {
            if (draggedObject == null || mainCamera == null) return;

            // Включаем коллайдер обратно
            if (draggedObject.TryGetComponent<Collider>(out Collider col))
            {
                col.enabled = true;
            }

            bool droppedInCauldron = false;

            // Пускаем луч по вертикальной оси ВНИЗ от позиции объекта
            // Начинаем луч немного выше объекта (например, на 5 юнитов), чтобы гарантированно задеть коллайдер котла
            Ray dropRay = new Ray(draggedObject.transform.position + Vector3.up * 5f, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(dropRay, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            foreach (var hit in hits)
            {
                CauldronController hitCauldron = hit.collider.GetComponentInParent<CauldronController>();
                if (hitCauldron != null)
                {
                    Debug.Log("[DragAndDrop] Брошено в котел (попадание по вертикальной оси)!");
                    hitCauldron.AddIngredient(draggedObject);
                    droppedInCauldron = true;
                    break;
                }
            }

            if (!droppedInCauldron)
            {
                Debug.Log("[DragAndDrop] Брошено мимо.");
                if (wasCloned)
                {
                    // Если это была копия бесконечного источника - просто уничтожаем
                    Destroy(draggedObject.gameObject);
                }
                else
                {
                    // Если это был уникальный результат - возвращаем на место
                    draggedObject.transform.position = originalPosition;
                }
            }

            draggedObject = null;
        }

        private void TryClickCauldronForCrafting(Vector2 screenPosition)
        {
            TryRefreshMainCamera();
            if (mainCamera == null) return;
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, RaycastBuffer, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (count == 0) return;
            SortRaycastBufferByDistance(count);
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = RaycastBuffer[i];
                if (ShouldSkipForCraftingDrag(h)) continue;
                CauldronController hitCauldron = h.collider.GetComponentInParent<CauldronController>();
                if (hitCauldron != null)
                {
                    hitCauldron.TryCraft();
                    return;
                }
            }
        }
    }
}
