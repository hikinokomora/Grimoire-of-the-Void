using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Перетаскивание аспектов на столе (простое и предсказуемое).
    /// - Взятие ЛКМ по коллайдеру, принадлежащему <see cref="AspectObject"/> (по parent тоже).
    /// - Перемещение в плоскости столешницы: луч мыши пересекает коллайдеры с <see cref="CraftingTableSurface"/>.
    /// - Отпускание: если под предметом зона котла (<see cref="CauldronDropZone"/>) — кладём в котёл, иначе оставляем на столе.
    /// </summary>
    public class CraftingInteractor : MonoBehaviour
    {
        [Header("Сцена")]
        [SerializeField] private CauldronController cauldron;

        [Tooltip("Если задана — все лучи (D&D) из неё. Иначе GetViewCamera() в режиме стола, иначе Main.")]
        [SerializeField] private Camera viewCameraOverride;

        [Header("Перетаскивание")]
        [Tooltip("Макс. длина луча от камеры при пересечении с плоскостью (м).")]
        [SerializeField] private float maxRayCastDistance = 30f;

        [Tooltip("Если включено — во время drag применяется AlongNormalOffset (предмет будет визуально 'утоплен' в стол уже при перетаскивании). Обычно удобнее выключить и применять offset только при отпускании.")]
        [SerializeField] private bool applyAlongNormalOffsetWhileDragging = false;

        private Camera mainCamera;

        private AspectObject _dragged;
        private bool _dragWasCloned;
        private Vector3 _dragOriginalPos;
        private Vector2 _dragGrabOffsetXz;
        private float _dragHeightOffset;

        private Rigidbody _dragRb;
        private bool _dragStoredKinematic;
        private bool _dragStoredGravity;

        private Collider[] _dragCols;
        private bool[] _dragColsPrevEnabled;

        private bool _suppressThisUpdate;
        private static readonly RaycastHit[] _hits = new RaycastHit[64];

        private void Awake()
        {
            TryRefreshMainCamera();
        }

        private void OnDisable()
        {
            EndDrag(false);
        }

        private void TryRefreshMainCamera()
        {
            if (viewCameraOverride != null)
            {
                mainCamera = viewCameraOverride;
                return;
            }
            if (CraftingViewController.Instance != null && CraftingViewController.IsInCraftingView)
            {
                Camera v = CraftingViewController.Instance.GetViewCamera();
                if (v != null)
                {
                    mainCamera = v;
                    return;
                }
            }
            mainCamera = Camera.main;
        }

        public void RequestSuppressNextInput()
        {
            _suppressThisUpdate = true;
        }

        private void Update()
        {
            if (_suppressThisUpdate)
            {
                _suppressThisUpdate = false;
                return;
            }
            if (Mouse.current == null)
            {
                return;
            }

            TryRefreshMainCamera();
            var mousePos = GetPointerScreenPosition();
            var left = Mouse.current.leftButton;

            if (left.wasPressedThisFrame)
            {
                TryBeginDrag(mousePos);
            }

            if (_dragged != null)
            {
                if (left.isPressed)
                {
                    UpdateDrag(mousePos);
                }
                if (left.wasReleasedThisFrame)
                {
                    TryDropAtCurrentPosition();
                }
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                TryClickCauldronForCrafting(mousePos);
            }
        }

        private void TryBeginDrag(Vector2 screenPosition)
        {
            TryRefreshMainCamera();
            if (mainCamera == null)
            {
                Debug.LogError("[DragAndDrop] Камера не задана (view override / GetViewCamera / MainCamera).");
                return;
            }

            Ray ray = ScreenPointToRayOnViewCamera(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, _hits, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (count <= 0) return;

            SortHitsByDistance(_hits, count);
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _hits[i];
                if (ShouldSkipForCraftingDrag(h)) continue;
                if (h.collider == null) continue;

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

                AspectObject aspect = h.collider.GetComponentInParent<AspectObject>();
                if (aspect == null) continue;

                BeginDrag(aspect, h.point);
                return;
            }
        }

        private static Vector2 GetPointerScreenPosition()
        {
            return Mouse.current != null ? Mouse.current.position.ReadValue() : default;
        }

        private Ray ScreenPointToRayOnViewCamera(Vector2 screenPosition)
        {
            if (mainCamera == null) return default;
            return mainCamera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
        }

        private void BeginDrag(AspectObject aspect, Vector3 hitPoint)
        {
            EndDrag(false);

            if (aspect.isInfiniteSource)
            {
                _dragged = Instantiate(aspect, aspect.transform.position, aspect.transform.rotation);
                _dragged.isInfiniteSource = false;
                _dragWasCloned = true;
            }
            else
            {
                _dragged = aspect;
                _dragWasCloned = false;
            }

            _dragOriginalPos = _dragged.transform.position;
            // Сохраняем оффсет только по XZ, чтобы предмет не «улетал» вверх/вниз из‑за точки клика на верхушке коллайдера.
            Vector3 o = _dragged.transform.position;
            _dragGrabOffsetXz = new Vector2(hitPoint.x - o.x, hitPoint.z - o.z);

            // Высота относительно поверхности стола (если нашли поверхность под курсором при взятии).
            _dragHeightOffset = 0f;
            if (mainCamera != null)
            {
                Ray ray = ScreenPointToRayOnViewCamera(GetPointerScreenPosition());
                if (TryRaycastTable(ray, out Vector3 tablePoint, out CraftingTableSurface surface, out Vector3 tableNormal))
                {
                    float baseY = tablePoint.y + (applyAlongNormalOffsetWhileDragging ? tableNormal.y * surface.AlongNormalOffset : 0f);
                    _dragHeightOffset = o.y - baseY;
                }
            }

            DisableDraggedColliders();
            BeginDragRigidbody();
        }

        private void BeginDragRigidbody()
        {
            _dragRb = null;
            if (_dragged == null) return;
            if (!_dragged.TryGetComponent(out _dragRb)) return;
            _dragStoredKinematic = _dragRb.isKinematic;
            _dragStoredGravity = _dragRb.useGravity;

            if (!_dragRb.isKinematic)
            {
                _dragRb.linearVelocity = Vector3.zero;
                _dragRb.angularVelocity = Vector3.zero;
            }
            _dragRb.useGravity = false;
            _dragRb.isKinematic = true;
        }

        private void RestoreDragRigidbody()
        {
            if (_dragRb == null) return;
            _dragRb.isKinematic = _dragStoredKinematic;
            _dragRb.useGravity = _dragStoredGravity;
            _dragRb = null;
        }

        private void UpdateDrag(Vector2 screenPosition)
        {
            if (_dragged == null || mainCamera == null) return;
            Ray ray = ScreenPointToRayOnViewCamera(screenPosition);

            if (TryRaycastTable(ray, out Vector3 tablePoint, out CraftingTableSurface surface, out Vector3 tableNormal))
            {
                float baseY = tablePoint.y + (applyAlongNormalOffsetWhileDragging ? tableNormal.y * surface.AlongNormalOffset : 0f);
                float targetY = baseY + _dragHeightOffset;

                // Ключевой момент: XZ берём не из точки на столе, а из пересечения луча мыши с горизонтальной плоскостью на высоте объекта.
                // Тогда «плоскость курсора» совпадает с высотой предмета и не создаёт ощущения «на ниточке».
                Vector3 pOnDragPlane = tablePoint;
                if (TryIntersectHorizontalPlane(ray, targetY, out Vector3 planePoint))
                {
                    pOnDragPlane = planePoint;
                }

                Vector3 target = new Vector3(
                    pOnDragPlane.x - _dragGrabOffsetXz.x,
                    targetY,
                    pOnDragPlane.z - _dragGrabOffsetXz.y);
                MoveDraggedTo(target);
                return;
            }

            // Фолбэк: горизонтальная плоскость на текущей высоте.
            float y = _dragged.transform.position.y;
            if (TryIntersectHorizontalPlane(ray, y, out Vector3 p))
            {
                Vector3 target = new Vector3(
                    p.x - _dragGrabOffsetXz.x,
                    y,
                    p.z - _dragGrabOffsetXz.y);
                MoveDraggedTo(target);
            }
        }

        private void MoveDraggedTo(Vector3 worldPos)
        {
            if (_dragged == null) return;
            if (_dragRb != null)
            {
                _dragRb.position = worldPos;
            }
            else
            {
                _dragged.transform.position = worldPos;
            }
        }

        private bool TryIntersectHorizontalPlane(Ray ray, float y, out Vector3 p)
        {
            p = default;
            var planeH = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            if (!planeH.Raycast(ray, out float e) || e < 0.01f || e > maxRayCastDistance)
            {
                return false;
            }
            p = ray.GetPoint(e);
            return true;
        }

        private bool TryRaycastTable(Ray ray, out Vector3 tablePoint, out CraftingTableSurface surface, out Vector3 tableNormal)
        {
            tablePoint = default;
            surface = null;
            tableNormal = Vector3.up;
            int n = Physics.RaycastNonAlloc(ray, _hits, maxRayCastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (n <= 0) return false;
            SortHitsByDistance(_hits, n);

            Transform dragRoot = _dragged != null ? _dragged.transform : null;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null) continue;
                if (dragRoot != null && (h.collider.transform == dragRoot || h.collider.transform.IsChildOf(dragRoot))) continue;
                surface = h.collider.GetComponentInParent<CraftingTableSurface>();
                if (surface == null) continue;
                tablePoint = h.point;
                tableNormal = h.normal;
                return true;
            }

            return false;
        }

        private void TryDropAtCurrentPosition()
        {
            if (_dragged == null) return;

            RestoreDraggedColliders();

            Transform dragRoot = _dragged.transform;
            RestoreDragRigidbody();

            Ray dropRay = new Ray(_dragged.transform.position + Vector3.up * 5f, Vector3.down);
            int n = Physics.RaycastNonAlloc(dropRay, _hits, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (n <= 0)
            {
                EndDrag(false);
                return;
            }

            SortHitsByDistance(_hits, n);

            // 1) Котёл (явная зона) / фолбэк (любой котёл)
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform == dragRoot || h.collider.transform.IsChildOf(dragRoot)) continue;

                CauldronDropZone dz = h.collider.GetComponentInParent<CauldronDropZone>();
                CauldronController pot = dz != null ? dz.GetCauldron() : h.collider.GetComponentInParent<CauldronController>();
                if (pot == null) continue;
                // Если в сцене несколько котлов, можно закрепить конкретный через CauldronDropZone.
                pot.AddIngredient(_dragged);
                _dragged = null;
                return;
            }

            // 2) Столешница
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform == dragRoot || h.collider.transform.IsChildOf(dragRoot)) continue;
                CraftingTableSurface surface = h.collider.GetComponentInParent<CraftingTableSurface>();
                if (surface == null) continue;
                _dragged.transform.position = h.point + h.normal * surface.AlongNormalOffset;
                _dragged = null;
                return;
            }

            EndDrag(false);
        }

        private void DisableDraggedColliders()
        {
            RestoreDraggedColliders();
            if (_dragged == null) return;
            _dragCols = _dragged.GetComponentsInChildren<Collider>(true);
            if (_dragCols == null || _dragCols.Length == 0) return;
            _dragColsPrevEnabled = new bool[_dragCols.Length];
            for (int i = 0; i < _dragCols.Length; i++)
            {
                Collider c = _dragCols[i];
                if (c == null) continue;
                _dragColsPrevEnabled[i] = c.enabled;
                c.enabled = false;
            }
        }

        private void RestoreDraggedColliders()
        {
            if (_dragCols == null || _dragColsPrevEnabled == null) return;
            int n = Mathf.Min(_dragCols.Length, _dragColsPrevEnabled.Length);
            for (int i = 0; i < n; i++)
            {
                Collider c = _dragCols[i];
                if (c == null) continue;
                c.enabled = _dragColsPrevEnabled[i];
            }
            _dragCols = null;
            _dragColsPrevEnabled = null;
        }

        private void EndDrag(bool keepIfNotPlaced)
        {
            if (_dragged == null)
            {
                RestoreDraggedColliders();
                RestoreDragRigidbody();
                return;
            }

            RestoreDraggedColliders();
            RestoreDragRigidbody();

            if (!keepIfNotPlaced)
            {
                if (_dragWasCloned)
                {
                    Destroy(_dragged.gameObject);
                }
                else
                {
                    _dragged.transform.position = _dragOriginalPos;
                }
            }

            _dragged = null;
            _dragWasCloned = false;
        }

        private void TryClickCauldronForCrafting(Vector2 screenPosition)
        {
            TryRefreshMainCamera();
            if (mainCamera == null) return;
            Ray ray = ScreenPointToRayOnViewCamera(screenPosition);
            int count = Physics.RaycastNonAlloc(ray, _hits, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (count == 0) return;
            SortHitsByDistance(_hits, count);
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _hits[i];
                if (ShouldSkipForCraftingDrag(h)) continue;
                if (h.collider == null) continue;
                CauldronController hitCauldron = h.collider.GetComponentInParent<CauldronController>();
                if (hitCauldron != null)
                {
                    hitCauldron.TryCraft();
                    return;
                }
            }
        }

        private static void SortHitsByDistance(RaycastHit[] buffer, int count)
        {
            for (int a = 0; a < count - 1; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    if (buffer[b].distance < buffer[a].distance)
                    {
                        (buffer[a], buffer[b]) = (buffer[b], buffer[a]);
                    }
                }
            }
        }

        private static bool ShouldSkipForCraftingDrag(RaycastHit h)
        {
            Collider c = h.collider;
            if (c == null) return true;
            if (c.GetComponent<CraftingTableEntryObstacle>() != null) return true;
            if (c.GetComponent<CraftingTableStation>() != null) return true;
            return false;
        }
    }
}
