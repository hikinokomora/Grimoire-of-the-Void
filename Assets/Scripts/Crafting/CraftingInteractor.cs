using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Перетаскивание на столе: плоскость = горизонт y = y pivot <b>в кадр взятия</b> (без «псевдостолешницы»).
    /// Позиция: луч(m) ∩ плоскость → P; pivot O = (P.xz - (hit.xz - O0.xz), O0.y) — точка клика следует курсору по XZ, высота зафиксирована.
    /// Камера: <see cref="CraftingViewController.GetViewCamera"/>, <see cref="viewCameraOverride"/> или Main.
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

        private Camera mainCamera;

        private AspectObject draggedObject;
        private Vector3 originalPosition;
        private bool wasCloned;
        private bool suppressThisUpdate;

        private Vector3 pickObjectPos;
        private Vector3 pickHitWorld;
        private float dragLockedY;

        private Rigidbody dragBody;
        private bool dragHadRigidbody;
        private bool storedKinematic;
        private bool storedUseGravity;
        private Vector3 targetDragPosition;

        private static readonly RaycastHit[] RaycastBuffer = new RaycastHit[32];
        private static readonly RaycastHit[] DragTableRayBuffer = new RaycastHit[48];

        private void Awake()
        {
            TryRefreshMainCamera();
        }

        private void OnDisable()
        {
            if (draggedObject != null)
            {
                if (draggedObject.TryGetComponent<Collider>(out Collider c))
                {
                    c.enabled = true;
                }
            }
            ReleaseDragPhysicsIfNeeded();
            draggedObject = null;
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
            var mousePos = GetPointerScreenPosition();
            var left = Mouse.current.leftButton;

            if (left.wasPressedThisFrame)
            {
                TryPickUp(mousePos);
            }

            if (draggedObject != null)
            {
                if (left.isPressed)
                {
                    UpdateDragTarget(mousePos);
                }
                if (left.wasReleasedThisFrame)
                {
                    TryDrop();
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
                Debug.LogError("[DragAndDrop] Камера не задана (view override / GetViewCamera / MainCamera).");
                return;
            }

            Ray ray = ScreenPointToRayOnViewCamera(screenPosition);
            TryProcessPickRay(ray);
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

        private void TryProcessPickRay(Ray pickRay)
        {
            int count = Physics.RaycastNonAlloc(pickRay, RaycastBuffer, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
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

                    Transform t = draggedObject.transform;
                    pickObjectPos = t.position;
                    pickHitWorld = h.point;
                    dragLockedY = t.position.y;

                    BeginDragPhysics();
                    targetDragPosition = t.position;
                    if (dragBody != null)
                    {
                        dragBody.position = t.position;
                    }
                    return;
                }
            }
        }

        private void BeginDragPhysics()
        {
            ReleaseDragPhysicsIfNeeded();
            if (draggedObject == null) return;
            if (!draggedObject.TryGetComponent(out dragBody)) return;
            dragHadRigidbody = true;
            storedKinematic = dragBody.isKinematic;
            storedUseGravity = dragBody.useGravity;
            dragBody.isKinematic = true;
            dragBody.useGravity = false;
            dragBody.linearVelocity = Vector3.zero;
            dragBody.angularVelocity = Vector3.zero;
        }

        private void ReleaseDragPhysicsIfNeeded()
        {
            if (!dragHadRigidbody || dragBody == null) return;
            dragBody.isKinematic = storedKinematic;
            dragBody.useGravity = storedUseGravity;
            dragBody = null;
            dragHadRigidbody = false;
        }

        private void UpdateDragTarget(Vector2 screenPosition)
        {
            if (draggedObject == null || mainCamera == null) return;
            RecomputeWorldFromMouseRay(ScreenPointToRayOnViewCamera(screenPosition));
        }

        private void RecomputeWorldFromMouseRay(Ray ray)
        {
            if (!TryIntersectHorizontalPlane(ray, dragLockedY, out Vector3 p))
            {
                if (TryGetFirstTableSurfaceXzOnRay(ray, draggedObject.transform, out p))
                {
                    p = new Vector3(p.x, dragLockedY, p.z);
                }
                else
                {
                    return;
                }
            }

            float gdx = pickHitWorld.x - pickObjectPos.x;
            float gdz = pickHitWorld.z - pickObjectPos.z;
            targetDragPosition = new Vector3(p.x - gdx, dragLockedY, p.z - gdz);
            if (dragBody == null)
            {
                draggedObject.transform.position = targetDragPosition;
            }
            else
            {
                dragBody.position = targetDragPosition;
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

        private bool TryGetFirstTableSurfaceXzOnRay(Ray ray, Transform dragRoot, out Vector3 worldPoint)
        {
            worldPoint = default;
            int n = Physics.RaycastNonAlloc(ray, DragTableRayBuffer, maxRayCastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (n == 0) return false;
            SortHitsByDistance(DragTableRayBuffer, n);
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = DragTableRayBuffer[i];
                if (h.collider == null) continue;
                if (IsUnderHierarchy(h.collider.transform, dragRoot)) continue;
                var surface = h.collider.GetComponentInParent<CraftingTableSurface>();
                if (surface == null) continue;
                worldPoint = h.point + h.normal * surface.AlongNormalOffset;
                return true;
            }
            return false;
        }

        private void TryDrop()
        {
            if (draggedObject == null) return;

            if (draggedObject.TryGetComponent<Collider>(out Collider col))
            {
                col.enabled = true;
            }

            Transform dragRoot = draggedObject.transform;
            ReleaseDragPhysicsIfNeeded();

            Ray dropRay = new Ray(draggedObject.transform.position + Vector3.up * 5f, Vector3.down);
            RaycastHit[] rawHits = Physics.RaycastAll(dropRay, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (!CollectSortedDropHits(rawHits, dragRoot, out List<RaycastHit> hits))
            {
                ApplyDropMiss();
                draggedObject = null;
                return;
            }

            foreach (RaycastHit hit in hits)
            {
                CauldronController hitCauldron = hit.collider.GetComponentInParent<CauldronController>();
                if (hitCauldron != null)
                {
                    hitCauldron.AddIngredient(draggedObject);
                    draggedObject = null;
                    return;
                }
            }

            foreach (RaycastHit hit in hits)
            {
                var surface = hit.collider.GetComponentInParent<CraftingTableSurface>();
                if (surface != null)
                {
                    draggedObject.transform.position = hit.point + hit.normal * surface.AlongNormalOffset;
                    draggedObject = null;
                    return;
                }
            }

            ApplyDropMiss();
            draggedObject = null;
        }

        private void ApplyDropMiss()
        {
            if (draggedObject == null) return;

            if (wasCloned)
            {
                Destroy(draggedObject.gameObject);
            }
            else
            {
                draggedObject.transform.position = originalPosition;
            }
        }

        private static bool IsUnderHierarchy(Transform node, Transform root)
        {
            if (node == null || root == null) return false;
            return node == root || node.IsChildOf(root);
        }

        private static bool IsColliderOnDragged(Transform dragRoot, Collider c)
        {
            if (c == null || dragRoot == null) return true;
            return c.transform == dragRoot || c.transform.IsChildOf(dragRoot);
        }

        private static bool CollectSortedDropHits(RaycastHit[] raw, Transform dragRoot, out List<RaycastHit> sorted)
        {
            sorted = new List<RaycastHit>();
            for (int i = 0; i < raw.Length; i++)
            {
                if (IsColliderOnDragged(dragRoot, raw[i].collider)) continue;
                sorted.Add(raw[i]);
            }
            if (sorted.Count == 0) return false;
            sorted.Sort((a, b) => a.distance.CompareTo(b.distance));
            return true;
        }

        private void TryClickCauldronForCrafting(Vector2 screenPosition)
        {
            TryRefreshMainCamera();
            if (mainCamera == null) return;
            Ray ray = ScreenPointToRayOnViewCamera(screenPosition);
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

        private static void SortRaycastBufferByDistance(int count)
        {
            SortHitsByDistance(RaycastBuffer, count);
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
