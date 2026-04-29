using UnityEngine;
using UnityEngine.InputSystem;

namespace GrimoireOfTheVoid.UI
{
    [DisallowMultipleComponent]
    public sealed class InGameSettingsMenu : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject settingsCanvasRoot;

        [Header("Pause")]
        [SerializeField] private bool pauseTimeWhileOpen = true;

        [Header("Cursor")]
        [SerializeField] private bool unlockCursorWhileOpen = true;

        [Header("Player control")]
        [SerializeField] private bool disablePlayerMovementWhileOpen = true;

        private bool _isOpen;
        private float _prevTimeScale = 1f;
        private BasicMovement[] _cachedMovements;
        private bool[] _cachedEnabled;
        private bool[] _cachedCanMove;

        private void Awake()
        {
            if (settingsCanvasRoot != null)
            {
                settingsCanvasRoot.SetActive(false);
            }
        }

        private void CacheMovements()
        {
            if (!disablePlayerMovementWhileOpen)
            {
                return;
            }

            _cachedMovements = UnityEngine.Object.FindObjectsByType<BasicMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _cachedEnabled = _cachedMovements != null ? new bool[_cachedMovements.Length] : null;
            _cachedCanMove = _cachedMovements != null ? new bool[_cachedMovements.Length] : null;
            if (_cachedMovements == null) return;

            for (int i = 0; i < _cachedMovements.Length; i++)
            {
                var m = _cachedMovements[i];
                if (m == null) continue;
                _cachedEnabled[i] = m.enabled;
                _cachedCanMove[i] = m.canMove;
            }
        }

        private void ApplyMovementState(bool enabled)
        {
            if (!disablePlayerMovementWhileOpen)
            {
                return;
            }

            if (_cachedMovements == null || _cachedMovements.Length == 0)
            {
                CacheMovements();
            }

            if (_cachedMovements == null) return;

            for (int i = 0; i < _cachedMovements.Length; i++)
            {
                var m = _cachedMovements[i];
                if (m == null) continue;

                if (enabled)
                {
                    // Restore.
                    m.canMove = _cachedCanMove != null && i < _cachedCanMove.Length ? _cachedCanMove[i] : true;
                    m.ExitStationView();
                    m.enabled = _cachedEnabled != null && i < _cachedEnabled.Length ? _cachedEnabled[i] : true;
                }
                else
                {
                    // Disable camera rotation + movement.
                    m.EnterStationView();
                    m.canMove = false;
                    m.enabled = false;
                }
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            // If the crafting table view is active, ESC is reserved for exiting that mode.
            if (!_isOpen && Crafting.CraftingViewController.IsInCraftingView)
            {
                return;
            }

            Toggle();
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            if (settingsCanvasRoot == null) return;

            _isOpen = true;
            settingsCanvasRoot.SetActive(true);

            // Sync sliders to the latest saved values (menu <-> in-game).
            var controllers = settingsCanvasRoot.GetComponentsInChildren<SettingsMenuController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null)
                {
                    controllers[i].RefreshFromPrefs();
                }
            }

            CacheMovements();
            ApplyMovementState(false);

            if (pauseTimeWhileOpen)
            {
                _prevTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (unlockCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            if (settingsCanvasRoot != null) settingsCanvasRoot.SetActive(false);

            if (pauseTimeWhileOpen)
            {
                Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;
            }

            if (unlockCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            ApplyMovementState(true);
        }
    }
}

