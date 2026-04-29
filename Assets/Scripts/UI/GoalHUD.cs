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
        [Tooltip("Опциональная Image-рамка таймера (поверх/вокруг заливки).")]
        [SerializeField] private Image timerFrame;
        [SerializeField] private Image timerFill;
        [Tooltip("Спрайт рамки таймера (Type=Sliced). Назначь PNG как Sprite (2D and UI) и настрой Borders в Sprite Editor.")]
        [SerializeField] private Sprite timerFrameSprite;
        [Tooltip("Спрайт заливки таймера (Type=Filled Horizontal).")]
        [SerializeField] private Sprite timerFillSprite;
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
        private RectTransform _timerFillMaskRect;
        private bool _timerFillMaskReady;
        private float _timerFillMaskBaseWidth;
        private Vector2 _timerFillMaskBaseOffsetMin;
        private Vector2 _timerFillMaskBaseOffsetMax;
        private BasicMovement[] _cachedMovements;

        private void OnEnable()
        {
            SubscribeDirectorEvents();

            ApplyTimerBarSpritesAndTypes();

            if (timerFill != null && forceTimerFillImageToFilled)
            {
                // If we drive by rect/mask cropping, we must NOT use Image.Filled (it would visually shrink/squash).
                // In that mode we keep fillAmount at 1 and crop via RectMask2D.
                if (driveTimerBarByRectTransform)
                {
                    timerFill.type = Image.Type.Simple;
                }
                else
                {
                    timerFill.type = Image.Type.Filled;
                    timerFill.fillMethod = Image.FillMethod.Horizontal;
                    timerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                }
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

        private void ApplyTimerBarSpritesAndTypes()
        {
            if (timerFrame != null && timerFrameSprite != null)
            {
                timerFrame.sprite = timerFrameSprite;
                timerFrame.type = Image.Type.Sliced;
            }

            if (timerFill != null && timerFillSprite != null)
            {
                timerFill.sprite = timerFillSprite;
                timerFill.type = Image.Type.Filled;
                timerFill.fillMethod = Image.FillMethod.Horizontal;
                timerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
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
                // If we crop via mask, keep the image itself full-size (no Filled shrink).
                timerFill.fillAmount = driveTimerBarByRectTransform ? 1f : t;

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
                        timerFill.color = ForceAlpha1(Color.Lerp(barMidColor, barFullColor, u));
                    }
                    else
                    {
                        float u = Mathf.InverseLerp(0f, 0.5f, t);
                        timerFill.color = ForceAlpha1(Color.Lerp(barLowColor, barMidColor, u));
                    }
                }
            }
        }

        private static Color ForceAlpha1(Color c)
        {
            c.a = 1f;
            return c;
        }

        private void CacheTimerFillRectDefaults()
        {
            if (timerFill == null) return;
            EnsureTimerFillMask();
            RectTransform rt = timerFill.rectTransform;
            _timerFillAnchorMin = rt.anchorMin;
            _timerFillAnchorMax = rt.anchorMax;
            _timerFillOffsetMin = rt.offsetMin;
            _timerFillOffsetMax = rt.offsetMax;
        }

        private void ApplyTimerFillByRect(float t)
        {
            if (timerFill == null) return;
            EnsureTimerFillMask();

            // We want the fill to be CROPPED, not squashed.
            // So we keep the fill rect at full width and instead shrink a RectMask2D wrapper.
            if (_timerFillMaskRect == null) return;

            // Keep the mask anchored exactly as authored (no anchor changes),
            // and crop by moving ONLY the right inset. The fill itself keeps full width and gets clipped.
            float clamped = Mathf.Clamp01(t);
            float newWidth = _timerFillMaskBaseWidth * clamped;
            float delta = _timerFillMaskBaseWidth - newWidth; // how much to hide from the right

            _timerFillMaskRect.offsetMin = _timerFillMaskBaseOffsetMin;
            _timerFillMaskRect.offsetMax = new Vector2(_timerFillMaskBaseOffsetMax.x - delta, _timerFillMaskBaseOffsetMax.y);
        }

        private void EnsureTimerFillMask()
        {
            if (_timerFillMaskReady)
            {
                return;
            }
            _timerFillMaskReady = true;

            if (timerFill == null)
            {
                return;
            }

            RectTransform fillRt = timerFill.rectTransform;
            Transform originalParent = fillRt.parent;
            if (originalParent == null)
            {
                return;
            }

            // Create wrapper with RectMask2D at the fill's position.
            var maskGo = new GameObject(fillRt.gameObject.name + "_Mask", typeof(RectTransform), typeof(RectMask2D));
            var maskRt = (RectTransform)maskGo.transform;
            maskRt.SetParent(originalParent, false);
            maskRt.SetSiblingIndex(fillRt.GetSiblingIndex());

            // Copy layout from the fill to the mask.
            maskRt.anchorMin = fillRt.anchorMin;
            maskRt.anchorMax = fillRt.anchorMax;
            maskRt.pivot = fillRt.pivot;
            maskRt.anchoredPosition = fillRt.anchoredPosition;
            maskRt.sizeDelta = fillRt.sizeDelta;
            maskRt.localRotation = fillRt.localRotation;
            maskRt.localScale = fillRt.localScale;
            maskRt.offsetMin = fillRt.offsetMin;
            maskRt.offsetMax = fillRt.offsetMax;

            // Make sure layout is up-to-date before measuring rects.
            Canvas.ForceUpdateCanvases();

            _timerFillMaskBaseOffsetMin = maskRt.offsetMin;
            _timerFillMaskBaseOffsetMax = maskRt.offsetMax;
            // Prefer the authored fill width if available; rect.width can be 0 before first layout pass in some setups.
            float w = maskRt.rect.width;
            w = Mathf.Max(w, fillRt.rect.width);
            w = Mathf.Max(w, fillRt.sizeDelta.x);
            _timerFillMaskBaseWidth = Mathf.Max(1f, w);

            // Reparent fill under the mask and stretch it to full size.
            fillRt.SetParent(maskRt, false);
            // IMPORTANT: keep the fill at FULL WIDTH so it gets clipped by the mask (cropping),
            // not resized to match the mask (squeezing).
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f); // fixed width, stretched height
            fillRt.pivot = new Vector2(0f, 0.5f);   // lock to left edge
            fillRt.anchoredPosition = new Vector2(0f, 0f);
            fillRt.sizeDelta = new Vector2(_timerFillMaskBaseWidth, 0f);

            _timerFillMaskRect = maskRt;
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

            // BasicMovement is DontDestroyOnLoad in this project. During end screens we disable it,
            // so a scene reload would keep the disabled instance alive and the "restart" would be partial.
            // Destroying it forces a clean re-spawn from the scene.
            var movements = UnityEngine.Object.FindObjectsByType<BasicMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < movements.Length; i++)
            {
                BasicMovement m = movements[i];
                if (m == null) continue;
                if (m.gameObject.scene.name == "DontDestroyOnLoad" || m.gameObject.scene.buildIndex < 0)
                {
                    Destroy(m.gameObject);
                }
                else
                {
                    // In case it isn't persistent in some scenes, still reset to a playable state.
                    m.canMove = true;
                    m.ExitStationView();
                    m.enabled = true;
                }
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

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
