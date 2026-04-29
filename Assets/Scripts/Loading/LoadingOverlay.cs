using UnityEngine;

namespace GrimoireOfTheVoid.Loading
{
    /// <summary>
    /// Simple optional overlay controller. Put this on a Canvas root (or any GameObject) and
    /// assign it to IntroManager to hide the "empty" frame while loading/warmup runs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool disableRaycastsWhileHidden = true;

        private void Reset()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
            HideImmediate();
        }

        public void Show()
        {
            if (canvasGroup == null)
            {
                gameObject.SetActive(true);
                return;
            }
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            HideImmediate();
        }

        private void HideImmediate()
        {
            if (canvasGroup == null)
            {
                if (disableRaycastsWhileHidden)
                {
                    gameObject.SetActive(false);
                }
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (disableRaycastsWhileHidden)
            {
                gameObject.SetActive(false);
            }
        }
    }
}

