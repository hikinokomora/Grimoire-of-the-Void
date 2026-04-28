using System.Globalization;
using GrimoireOfTheVoid.Crafting;
using GrimoireOfTheVoid.Game;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GrimoireOfTheVoid.Crafting;

namespace GrimoireOfTheVoid.UI
{
    /// <summary>
    /// HUD цели и таймера; подписывается на события <see cref="GameDirector"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoalHUD : MonoBehaviour
    {
        [Header("Goal")]
        [SerializeField] private TextMeshProUGUI goalLabelText;
        [SerializeField] private TextMeshProUGUI goalNameText;
        [SerializeField] private Image goalIcon;

        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image timerFill;
        [SerializeField] [Min(0f)] private float criticalSeconds = 10f;
        [SerializeField] private Color normalTimerColor = Color.white;
        [SerializeField] private Color criticalTimerColor = new Color(1f, 0.35f, 0.35f);
        [SerializeField] private bool forceTimerFillImageToFilled = true;
        [Tooltip("Если true — полоска уменьшается через RectTransform (anchorMax.x). Работает всегда, даже если fillAmount не влияет (Image Type = Simple и т.п.).")]
        [SerializeField] private bool driveTimerBarByRectTransform = true;

        [Header("Timer bar colors")]
        [SerializeField] private bool tintTimerBarByRemaining = true;
        [SerializeField] private Color barFullColor = new Color(0.25f, 0.95f, 0.45f, 0.85f);
        [SerializeField] private Color barMidColor = new Color(0.98f, 0.85f, 0.25f, 0.85f);
        [SerializeField] private Color barLowColor = new Color(1f, 0.28f, 0.28f, 0.85f);

        [Header("End screens")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private bool pauseTimeOnGameOver = true;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button victoryRestartButton;
        [SerializeField] private Button quitButton;

        private GameDirector _subscribedDirector;
        private Vector2 _timerFillAnchorMin;
        private Vector2 _timerFillAnchorMax;
        private Vector2 _timerFillOffsetMin;
        private Vector2 _timerFillOffsetMax;
        private BasicMovement[] _cachedMovements;

        private void OnEnable()
        {
            SubscribeDirectorEvents();

            if (timerFill != null && forceTimerFillImageToFilled)
            {
                // If Image type is Simple, fillAmount won't visually change.
                timerFill.type = Image.Type.Filled;
                timerFill.fillMethod = Image.FillMethod.Horizontal;
                timerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            CacheTimerFillRectDefaults();

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(RestartScene);
            }

            if (victoryRestartButton != null)
            {
                victoryRestartButton.onClick.AddListener(RestartScene);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }

            HidePanels();

            if (GameDirector.Instance != null)
            {
                ApplyGoal(GameDirector.Instance.CurrentTarget);
                OnDirectorTimerChanged(GameDirector.Instance.TimeRemaining, GameDirector.Instance.MaxTime);
            }

            CacheMovementIfNeeded();
        }

        private void OnDisable()
        {
            UnsubscribeDirectorEvents();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(RestartScene);
            }

            if (victoryRestartButton != null)
            {
                victoryRestartButton.onClick.RemoveListener(RestartScene);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
            }
        }

        private void SubscribeDirectorEvents()
        {
            GameDirector d = GameDirector.Instance;
            if (d == null || _subscribedDirector != null)
            {
                return;
            }

            _subscribedDirector = d;
            _subscribedDirector.OnGoalChanged += OnDirectorGoalChanged;
            _subscribedDirector.OnTimerChanged += OnDirectorTimerChanged;
            _subscribedDirector.OnGameOver += OnDirectorGameOver;
            _subscribedDirector.OnVictory += OnDirectorVictory;
        }

        private void UnsubscribeDirectorEvents()
        {
            if (_subscribedDirector == null)
            {
                return;
            }

            _subscribedDirector.OnGoalChanged -= OnDirectorGoalChanged;
            _subscribedDirector.OnTimerChanged -= OnDirectorTimerChanged;
            _subscribedDirector.OnGameOver -= OnDirectorGameOver;
            _subscribedDirector.OnVictory -= OnDirectorVictory;
            _subscribedDirector = null;
        }

        private void OnDirectorGoalChanged(OccultAspect aspect)
        {
            ApplyGoal(aspect);
        }

        private void ApplyGoal(OccultAspect aspect)
        {
            if (goalNameText != null)
            {
                goalNameText.text = aspect != null ? aspect.DisplayName : "—";
            }

            if (goalLabelText != null)
            {
                goalLabelText.text = aspect != null ? "Цель:" : "";
            }

            if (goalIcon != null)
            {
                bool show = aspect != null && aspect.aspectIcon != null;
                goalIcon.gameObject.SetActive(show);
                if (show)
                {
                    goalIcon.sprite = aspect.aspectIcon;
                }
            }
        }

        private void OnDirectorTimerChanged(float remaining, float max)
        {
            if (timerText != null)
            {
                timerText.text = FormatTime(remaining);
                bool critical = max > 0f && remaining <= criticalSeconds;
                timerText.color = critical ? criticalTimerColor : normalTimerColor;
            }

            if (timerFill != null && max > 0f)
            {
                float t = Mathf.Clamp01(remaining / max);
                timerFill.fillAmount = t;

                if (driveTimerBarByRectTransform)
                {
                    ApplyTimerFillByRect(t);
                }

                if (tintTimerBarByRemaining)
                {
                    // 1..0 => green -> yellow -> red
                    if (t >= 0.5f)
                    {
                        float u = Mathf.InverseLerp(0.5f, 1f, t);
                        timerFill.color = Color.Lerp(barMidColor, barFullColor, u);
                    }
                    else
                    {
                        float u = Mathf.InverseLerp(0f, 0.5f, t);
                        timerFill.color = Color.Lerp(barLowColor, barMidColor, u);
                    }
                }
            }
        }

        private void CacheTimerFillRectDefaults()
        {
            if (timerFill == null) return;
            RectTransform rt = timerFill.rectTransform;
            _timerFillAnchorMin = rt.anchorMin;
            _timerFillAnchorMax = rt.anchorMax;
            _timerFillOffsetMin = rt.offsetMin;
            _timerFillOffsetMax = rt.offsetMax;
        }

        private void ApplyTimerFillByRect(float t)
        {
            if (timerFill == null) return;
            RectTransform rt = timerFill.rectTransform;

            // Assume the bar is laid out as a stretched rect inside a container (common for BG/Fill).
            // We shrink it by moving anchorMax.x towards anchorMin.x.
            Vector2 aMin = _timerFillAnchorMin;
            Vector2 aMax = _timerFillAnchorMax;
            float startX = aMin.x;
            float endX = aMax.x;
            if (endX < startX)
            {
                (startX, endX) = (endX, startX);
            }

            float x = Mathf.Lerp(startX, endX, t);
            rt.anchorMin = new Vector2(aMin.x, aMin.y);
            rt.anchorMax = new Vector2(x, aMax.y);
            rt.offsetMin = _timerFillOffsetMin;
            rt.offsetMax = _timerFillOffsetMax;
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f)
            {
                seconds = 0f;
            }

            int total = Mathf.FloorToInt(seconds);
            int m = total / 60;
            int s = total % 60;
            return string.Format(CultureInfo.InvariantCulture, "{0:0}:{1:00}", m, s);
        }

        private void OnDirectorGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }

            ApplyEndState();

            if (pauseTimeOnGameOver)
            {
                Time.timeScale = 0f;
            }
        }

        private void OnDirectorVictory()
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }

            ApplyEndState();

            if (pauseTimeOnGameOver)
            {
                Time.timeScale = 0f;
            }
        }

        private void HidePanels()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(false);
            }
        }

        private void RestartScene()
        {
            Time.timeScale = 1f;
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex);
        }

        private static void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void CacheMovementIfNeeded()
        {
            if (_cachedMovements != null && _cachedMovements.Length > 0) return;
            _cachedMovements = UnityEngine.Object.FindObjectsByType<BasicMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private void ApplyEndState()
        {
            // Unlock mouse for UI interaction.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Stop camera rotation / movement.
            CacheMovementIfNeeded();
            if (_cachedMovements != null)
            {
                for (int i = 0; i < _cachedMovements.Length; i++)
                {
                    BasicMovement m = _cachedMovements[i];
                    if (m == null) continue;
                    m.ForceDropHeld();
                    m.EnterStationView(); // RotateCamera() early-outs when InStationView == true
                    m.canMove = false;
                    m.enabled = false; // stops Update() entirely (mouse look still runs at timescale=0 otherwise)
                }
            }

            // If we are in crafting view, its transition coroutine can keep moving the camera pivot.
            // Disable it to prevent further camera motion during end screens.
            CraftingViewController cvc = CraftingViewController.Instance;
            if (cvc != null)
            {
                cvc.StopAllCoroutines();
                cvc.enabled = false;
            }
        }
    }
}
