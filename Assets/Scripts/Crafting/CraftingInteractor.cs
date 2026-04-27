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

        [Tooltip("Дистанция от камеры до плоскости, по которой перемещается объект")]
        [SerializeField] private float dragDistance = 3f;

        private Camera mainCamera;
        
        private AspectObject draggedObject;
        private Vector3 originalPosition;
        private bool wasCloned;
        private Plane dragPlane; // Горизонтальная плоскость для перетаскивания

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();

            // ЛКМ Нажата - пытаемся взять объект
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Debug.Log($"[DragAndDrop] Клик ЛКМ по координатам экрана: {mousePos}");
                TryPickUp(mousePos);
            }
            // ЛКМ Удерживается - тащим объект
            else if (Mouse.current.leftButton.isPressed && draggedObject != null)
            {
                Drag(mousePos);
            }
            // ЛКМ Отпущена - бросаем объект
            else if (Mouse.current.leftButton.wasReleasedThisFrame && draggedObject != null)
            {
                TryDrop(mousePos);
            }

            // ПКМ (Правая кнопка) - командуем котлу варить
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Debug.Log($"[DragAndDrop] Клик ПКМ по координатам экрана: {mousePos}");
                TryClickCauldronForCrafting(mousePos);
            }
        }

        private void TryPickUp(Vector2 screenPosition)
        {
            if (mainCamera == null)
            {
                Debug.LogError("[DragAndDrop] ОШИБКА: Не найдена MainCamera! Убедитесь, что на вашей камере стоит тег 'MainCamera'.");
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            // Увеличиваем дистанцию луча с 20f до 1000f на случай, если камера далеко
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                Debug.Log($"[DragAndDrop] Луч столкнулся с объектом: {hit.collider.gameObject.name}");
                
                if (hit.collider.TryGetComponent<AspectObject>(out AspectObject aspect))
                {
                    Debug.Log($"[DragAndDrop] Взят объект: {(aspect.aspectData != null ? aspect.aspectData.DisplayName : "Без данных")}");
                    
                    if (aspect.isInfiniteSource)
                    {
                        // Создаем временную копию для перетаскивания
                        draggedObject = Instantiate(aspect, aspect.transform.position, aspect.transform.rotation);
                        draggedObject.isInfiniteSource = false; // копия смертна
                        wasCloned = true;
                    }
                    else
                    {
                        draggedObject = aspect;
                        originalPosition = aspect.transform.position;
                        wasCloned = false;
                    }

                    // Отключаем коллайдер, чтобы он не перекрывалRaycast при поиске котла при дропе
                    if (draggedObject.TryGetComponent<Collider>(out Collider col))
                    {
                        col.enabled = false;
                    }
                    
                    // Создаем горизонтальную плоскость на высоте взятого объекта
                    dragPlane = new Plane(Vector3.up, draggedObject.transform.position);
                }
            }
        }

        private void Drag(Vector2 screenPosition)
        {
            if (draggedObject == null || mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            // Пересекаем луч из камеры с математической горизонтальной плоскостью (X-Z)
            if (dragPlane.Raycast(ray, out float distance))
            {
                draggedObject.transform.position = ray.GetPoint(distance);
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
            RaycastHit[] hits = Physics.RaycastAll(dropRay, 20f);

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
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                CauldronController hitCauldron = hit.collider.GetComponentInParent<CauldronController>();
                if (hitCauldron != null)
                {
                    Debug.Log("[DragAndDrop] Клик по котлу (попытка крафта)!");
                    hitCauldron.TryCraft();
                }
            }
        }
    }
}
