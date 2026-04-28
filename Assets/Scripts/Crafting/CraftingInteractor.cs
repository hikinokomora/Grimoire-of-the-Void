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
        private static bool _copyNextClickedAspect;

        public static void RequestCopyNextClickedAspect()
        {
            _copyNextClickedAspect = true;
        }

        [Header("Сцена")]
        [SerializeField] private CauldronController cauldron;

        [Tooltip("Если задана — все лучи (D&D) из неё. Иначе GetViewCamera() в режиме стола, иначе Main.")]
        [SerializeField] private Camera viewCameraOverride;

        [Header("Перетаскивание")]
        [Tooltip("Макс. длина луча от камеры при пересечении с плоскостью (м).")]
        [SerializeField] private float maxRayCastDistance = 30f;

        [Tooltip("Если включено — во время drag применяется AlongNormalOffset (предмет будет визуально 'утоплен' в стол уже при перетаскивании). Обычно удобнее выключить и применять offset только при отпускании.")]
        [SerializeField] private bool applyAlongNormalOffsetWhileDragging = false;

        [Tooltip("Насколько приподнимать предмет в момент взятия (м), чтобы не выглядело как 'телепорт' при отпускании.")]
        [SerializeField] private float pickupLift = 0.03f;

        private Camera mainCamera;

        private AspectObject _dragged;
        private bool _dragWasCloned;
        private Vector3 _dragOriginalPos;
        private Vector2 _dragGrabOffsetXz;
        private float _dragHeightOffset;
        private float _dragFixedPlaneY;
        private Transform _dragRoot;

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

            if (_copyNextClickedAspect)
            {
                _copyNextClickedAspect = false;
                _dragged = Instantiate(aspect, aspect.transform.position, aspect.transform.rotation);
                _dragged.isInfiniteSource = false;
                _dragWasCloned = true;
                _dragged.ApplySpawnGrace(0.15f);
            }
            else if (aspect.isInfiniteSource)
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
            _dragFixedPlaneY = _dragOriginalPos.y + Mathf.Max(0f, pickupLift);
            // Use the true prefab root for raycast exclusion (colliders often live on root, while AspectObject may be on a child).
            _dragRoot = _dragged.transform.root != null ? _dragged.transform.root : _dragged.transform;
            // Сохраняем оффсет только по XZ, чтобы предмет не «улетал» вверх/вниз из‑за точки клика на верхушке коллайдера.
            Vector3 o = _dragged.transform.position;
            // Для trigger-коллайдеров точка попадания луча часто даёт "ощущение, что курсор под столом"
            // (особенно если коллайдер объёмный/смещён). В этом случае цепляем по центру bounds коллайдера.
            Collider anyCol = _dragged.GetComponentInChildren<Collider>(true);
            if (anyCol != null && anyCol.isTrigger)
            {
                Vector3 c = anyCol.bounds.center;
                _dragGrabOffsetXz = new Vector2(c.x - o.x, c.z - o.z);
            }
            else
            {
                _dragGrabOffsetXz = new Vector2(hitPoint.x - o.x, hitPoint.z - o.z);
            }

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

            // Visual lift on pickup (keeps drag strictly horizontal at the lifted height).
            _dragged.transform.position = new Vector3(_dragged.transform.position.x, _dragFixedPlaneY, _dragged.transform.position.z);

            // While dragging we disable colliders so the dragged item cannot push other aspects around.
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

            // Hard lock: dragged aspects move strictly in the horizontal plane where they were grabbed.
            float targetY = _dragFixedPlaneY;
            if (TryIntersectHorizontalPlane(ray, targetY, out Vector3 pOnDragPlane))
            {
                Vector3 target = new Vector3(
                    pOnDragPlane.x - _dragGrabOffsetXz.x,
                    targetY,
                    pOnDragPlane.z - _dragGrabOffsetXz.y);
                MoveDraggedTo(target);
            }
            return;

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

            Transform dragRoot = _dragged != null ? _dragRoot : null;
            // Prefer upward-facing hits to avoid selecting underside/side faces of the table collider.
            CraftingTableSurface firstAnySurface = null;
            Vector3 firstAnyPoint = default;
            Vector3 firstAnyNormal = Vector3.up;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null) continue;
                if (ShouldSkipForTablePlacement(h.collider)) continue;
                if (dragRoot != null && (h.collider.transform == dragRoot || h.collider.transform.IsChildOf(dragRoot))) continue;
                CraftingTableSurface s = h.collider.GetComponentInParent<CraftingTableSurface>();
                if (s == null) continue;

                if (firstAnySurface == null)
                {
                    firstAnySurface = s;
                    firstAnyPoint = h.point;
                    firstAnyNormal = h.normal;
                }

                // Accept only "top-ish" hits when possible.
                if (h.normal.y < 0.2f) continue;

                surface = s;
                tablePoint = h.point;
                tableNormal = h.normal;
                return true;
            }

            if (firstAnySurface != null)
            {
                surface = firstAnySurface;
                tablePoint = firstAnyPoint;
                tableNormal = firstAnyNormal;
                return true;
            }

            return false;
        }

        private static bool ShouldSkipForTablePlacement(Collider c)
        {
            if (c == null) return true;
            // Table station triggers are used to enter crafting view and should not affect dragging/placement.
            // Important: the table surface collider may live on the same object as CraftingTableStation,
            // so we only skip the *trigger* collider(s), not the physical surface collider.
            if (c.isTrigger && c.GetComponentInParent<CraftingTableStation>() != null) return true;
            if (c.GetComponentInParent<CraftingTableEntryObstacle>() != null) return true;
            return false;
        }

        private void TryDropAtCurrentPosition()
        {
            if (_dragged == null) return;

            Transform dragRoot = _dragRoot != null ? _dragRoot : _dragged.transform;
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
                if (ShouldSkipForTablePlacement(h.collider)) continue;
                if (h.collider.transform == dragRoot || h.collider.transform.IsChildOf(dragRoot)) continue;

                CauldronDropZone dz = h.collider.GetComponentInParent<CauldronDropZone>();
                CauldronController pot = dz != null ? dz.GetCauldron() : h.collider.GetComponentInParent<CauldronController>();
                if (pot == null) continue;
                // Если в сцене несколько котлов, можно закрепить конкретный через CauldronDropZone.
                pot.AddIngredient(_dragged);
                RestoreDraggedColliders();
                _dragged = null;
                _dragRoot = null;
                return;
            }

            // 2) Столешница
            if (TryPickTopTableHit(_hits, n, dragRoot, out RaycastHit tableHit, out CraftingTableSurface surface))
            {
                // Do NOT "snap" the object upward on release: it feels like a teleport.
                // Dragging is already vertically locked; on release we keep the current pose and let physics
                // settle it onto the table naturally.
                //
                // Safety: if the object somehow ended up below the table hit point, lift it to the surface.
                Vector3 p = _dragged.transform.position;
                if (p.y < tableHit.point.y)
                {
                    p.y = tableHit.point.y;
                    _dragged.transform.position = p;
                }
                // Re-enable colliders after final placement.
                RestoreDraggedColliders();
                _dragged = null;
                _dragRoot = null;
                return;
            }

            EndDrag(false);
        }

        private static bool TryPickTopTableHit(RaycastHit[] buffer, int count, Transform dragRoot, out RaycastHit bestHit, out CraftingTableSurface bestSurface)
        {
            bestHit = default;
            bestSurface = null;
            float bestDist = float.PositiveInfinity;

            // Prefer upward-facing surfaces to avoid underside hits (which would place objects under the table).
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = buffer[i];
                if (h.collider == null) continue;
                if (ShouldSkipForTablePlacement(h.collider)) continue;
                if (dragRoot != null && (h.collider.transform == dragRoot || h.collider.transform.IsChildOf(dragRoot))) continue;
                CraftingTableSurface s = h.collider.GetComponentInParent<CraftingTableSurface>();
                if (s == null) continue;
                if (h.normal.y < 0.2f) continue;
                if (h.distance >= bestDist) continue;
                bestDist = h.distance;
                bestHit = h;
                bestSurface = s;
            }

            return bestSurface != null;
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
            _dragRoot = null;
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
