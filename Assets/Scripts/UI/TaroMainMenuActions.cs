using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrimoireOfTheVoid.UI
{
    [DisallowMultipleComponent]
    public sealed class TaroMainMenuActions : MonoBehaviour
    {
        private const string NextSceneOverrideKey = "menu_nextSceneName";

        [Header("Canvases")]
        [SerializeField] private GameObject settingsCanvasRoot;
        [SerializeField] private GameObject mainMenuCanvasRoot;

        [Header("Ritual (video intro)")]
        [SerializeField] private string introSceneName = "Intro";
        [SerializeField] private string gameSceneName = "Scene";

        [Header("Cultists button sound")]
        [SerializeField] private AudioSource cultistsAudioSource;
        [SerializeField] private AudioClip cultistsClip;
        [SerializeField] [Min(0f)] private float cultistsVolume = 1f;

        private void Awake()
        {
            if (settingsCanvasRoot != null && settingsCanvasRoot.activeSelf && mainMenuCanvasRoot != null)
            {
                // If someone left settings open in the scene, prefer the main menu on start.
                settingsCanvasRoot.SetActive(false);
                mainMenuCanvasRoot.SetActive(true);
            }
        }

        public void OpenSettings()
        {
            if (settingsCanvasRoot != null) settingsCanvasRoot.SetActive(true);
            if (mainMenuCanvasRoot != null) mainMenuCanvasRoot.SetActive(false);
        }

        public void CloseSettings()
        {
            if (settingsCanvasRoot != null) settingsCanvasRoot.SetActive(false);
            if (mainMenuCanvasRoot != null) mainMenuCanvasRoot.SetActive(true);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void PlayCultistsSound()
        {
            if (cultistsAudioSource == null || cultistsClip == null)
            {
                return;
            }

            cultistsAudioSource.pitch = 1f;
            cultistsAudioSource.PlayOneShot(cultistsClip, Mathf.Clamp01(cultistsVolume));
        }

        public void StartRitual()
        {
            if (!string.IsNullOrWhiteSpace(gameSceneName))
            {
                PlayerPrefs.SetString(NextSceneOverrideKey, gameSceneName);
                PlayerPrefs.Save();
            }

            if (!string.IsNullOrWhiteSpace(introSceneName))
            {
                SceneManager.LoadScene(introSceneName);
            }
        }
    }
}

