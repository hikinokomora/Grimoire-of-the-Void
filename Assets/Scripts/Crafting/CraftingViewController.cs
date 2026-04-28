using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Режим «у стола»: плавно ведёт pivot камеры к anchor, отключает FP-движение, включает CraftingInteractor.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftingViewController : MonoBehaviour
    {
        public static CraftingViewController Instance { get; private set; }
        public static bool IsInCraftingView { get; private set; }

        [Header("References")]
        [SerializeField] private BasicMovement movement;
        [Tooltip("Сценовый объект с CraftingInteractor; выключен в инспекторе, пока не активен режим стола.")]
        [SerializeField] private CraftingInteractor craftingInteractor;

        [Header("Camera transition")]
        [SerializeField] [Min(0.01f)] private float enterTransitionDuration = 0.45f;
        [SerializeField] [Min(0.01f)] private float exitTransitionDuration = 0.35f;

        private bool _active;
        private bool _isExiting;
        private Transform _currentViewAnchor;
        private Transform _savedCameraParent;
        private Vector3 _savedCameraLocalPosition;
        private Quaternion _savedCameraLocalRotation;
        private Coroutine _transitionRoutine;
        private Coroutine _enableInteractorRoutine;

        private void Awake()
        {
            Instance = this;
            if (movement == null)
            {
                movement = GetComponent<BasicMovement>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsInCraftingView = false;
            }
        }

        private void Update()
        {
            if (!IsInCraftingView || _isExiting)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Exit();
            }
        }

        public void Enter(Transform viewAnchor)
        {
            if (viewAnchor == null || _active || _isExiting)
            {
                return;
            }
            if (movement == null)
            {
                return;
            }

            Transform cameraPivot = movement.CameraPivot;
            if (cameraPivot == null)
            {
                Debug.LogError("[CraftingViewController] Camera pivot missing.");
                return;
            }

            StopTransitionCoroutines();

            _active = true;
            IsInCraftingView = true;
            _currentViewAnchor = viewAnchor;

            _savedCameraParent = cameraPivot.parent;
            _savedCameraLocalPosition = cameraPivot.localPosition;
            _savedCameraLocalRotation = cameraPivot.localRotation;

            movement.EnterStationView();
            movement.ForceDropHeld();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _transitionRoutine = StartCoroutine(EnterCameraTransition(viewAnchor, cameraPivot));
        }

        /// <summary>Камера для D&amp;D и луча из курсора в режиме стола: дочерняя у <see cref="BasicMovement.CameraPivot"/>, иначе Main.</summary>
        public Camera GetViewCamera()
        {
            if (movement == null) return Camera.main;
            if (movement.CameraPivot == null) return Camera.main;
            Transform p = movement.CameraPivot;
            var onPivot = p.GetComponent<Camera>();
            if (onPivot != null) return onPivot;
            var inChildren = p.GetComponentInChildren<Camera>(true);
            return inChildren != null ? inChildren : Camera.main;
        }

        public void Exit()
        {
            if (!_active || _isExiting)
            {
                return;
            }

            if (craftingInteractor != null)
            {
                craftingInteractor.enabled = false;
            }

            StopEnableInteractorRoutine();

            Transform cameraPivot = movement != null ? movement.CameraPivot : null;
            if (cameraPivot == null)
            {
                _active = false;
                IsInCraftingView = false;
                _currentViewAnchor = null;
                _isExiting = false;
                movement?.ExitStationView();
                ApplyLockedCursor();
                return;
            }

            StopTransitionCoroutines();
            _isExiting = true;
            _transitionRoutine = StartCoroutine(ExitCameraTransition(cameraPivot));
        }

        private void StopTransitionCoroutines()
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }
        }

        private void StopEnableInteractorRoutine()
        {
            if (_enableInteractorRoutine != null)
            {
                StopCoroutine(_enableInteractorRoutine);
                _enableInteractorRoutine = null;
            }
        }

        private IEnumerator EnterCameraTransition(Transform viewAnchor, Transform cameraPivot)
        {
            _isExiting = false;
            if (craftingInteractor != null)
            {
                craftingInteractor.enabled = false;
            }

            Vector3 startPos = cameraPivot.position;
            Quaternion startRot = cameraPivot.rotation;
            Vector3 endPos = viewAnchor.position;
            Quaternion endRot = viewAnchor.rotation;

            float d = Mathf.Max(enterTransitionDuration, 0.01f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / d;
                float s = Mathf.SmoothStep(0f, 1f, t);
                cameraPivot.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPos, endPos, s),
                    Quaternion.SlerpUnclamped(startRot, endRot, s));
                yield return null;
            }

            cameraPivot.SetPositionAndRotation(endPos, endRot);
            cameraPivot.SetParent(viewAnchor, false);
            cameraPivot.localPosition = Vector3.zero;
            cameraPivot.localRotation = Quaternion.identity;

            if (craftingInteractor != null)
            {
                _enableInteractorRoutine = StartCoroutine(EnableCraftingInteractorAfterFrame());
            }
            _transitionRoutine = null;
        }

        private IEnumerator ExitCameraTransition(Transform cameraPivot)
        {
            var parent = _savedCameraParent != null ? _savedCameraParent : movement.transform;
            Vector3 targetWorldPos = parent.TransformPoint(_savedCameraLocalPosition);
            Quaternion targetWorldRot = parent.rotation * _savedCameraLocalRotation;

            Vector3 startPos = cameraPivot.position;
            Quaternion startRot = cameraPivot.rotation;
            if (craftingInteractor != null)
            {
                craftingInteractor.enabled = false;
            }

            float d = Mathf.Max(exitTransitionDuration, 0.01f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / d;
                float s = Mathf.SmoothStep(0f, 1f, t);
                cameraPivot.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPos, targetWorldPos, s),
                    Quaternion.SlerpUnclamped(startRot, targetWorldRot, s));
                yield return null;
            }

            if (_savedCameraParent != null)
            {
                cameraPivot.SetParent(_savedCameraParent, false);
            }
            else
            {
                cameraPivot.SetParent(movement.transform, false);
            }
            cameraPivot.localPosition = _savedCameraLocalPosition;
            cameraPivot.localRotation = _savedCameraLocalRotation;

            movement?.ExitStationView();
            _active = false;
            IsInCraftingView = false;
            _currentViewAnchor = null;
            _isExiting = false;
            _transitionRoutine = null;

            ApplyLockedCursor();
        }

        private void ApplyLockedCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private IEnumerator EnableCraftingInteractorAfterFrame()
        {
            if (craftingInteractor == null)
            {
                yield break;
            }

            yield return new WaitForEndOfFrame();
            craftingInteractor.RequestSuppressNextInput();
            craftingInteractor.enabled = true;
            _enableInteractorRoutine = null;
        }
    }
}