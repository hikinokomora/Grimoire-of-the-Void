using System.Globalization;
using GrimoireOfTheVoid.Crafting;
using GrimoireOfTheVoid.Game;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        [Header("End screens")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private bool pauseTimeOnGameOver = true;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button victoryRestartButton;
        [SerializeField] private Button quitButton;

        private GameDirector _subscribedDirector;

        private void OnEnable()
        {
            SubscribeDirectorEvents();

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
                timerFill.fillAmount = Mathf.Clamp01(remaining / max);
            }
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
    }
}
