using GrimoireOfTheVoid.Crafting;
using UnityEngine;

namespace GrimoireOfTheVoid.Game
{
    /// <summary>
    /// Зона «доставки» цели на стол: конкретный trigger-collider выбирается отдельным компонентом
    /// <see cref="GoalDeliveryZoneTrigger"/> (это важно, если на столе несколько trigger-коллайдеров).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoalDeliveryZone : MonoBehaviour
    {
        [Header("Behaviour")]
        [Tooltip("Уничтожать объект аспекта после успешной доставки.")]
        [SerializeField] private bool consumeOnAccept = true;

        [Tooltip("Уничтожать объект при неверной сдаче (штраф по-прежнему из GameDirector).")]
        [SerializeField] private bool consumeOnWrongAspect = true;

        [Header("Optional VFX")]
        [SerializeField] private ParticleSystem acceptVfx;
        [SerializeField] private ParticleSystem rejectVfx;

        [Header("Optional highlight")]
        [SerializeField] private Renderer optionalHighlightRenderer;
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material highlightMaterial;

        private GameDirector _subscribedDirector;

        private void OnEnable()
        {
            _subscribedDirector = GameDirector.Instance;
            if (_subscribedDirector != null)
            {
                _subscribedDirector.OnGoalChanged += HandleGoalChanged;
            }

            RefreshHighlight(GameDirector.Instance != null && GameDirector.Instance.CurrentTarget != null);
        }

        private void OnDisable()
        {
            if (_subscribedDirector != null)
            {
                _subscribedDirector.OnGoalChanged -= HandleGoalChanged;
                _subscribedDirector = null;
            }
        }

        private void HandleGoalChanged(OccultAspect _)
        {
            bool active = GameDirector.Instance != null && GameDirector.Instance.CurrentTarget != null;
            RefreshHighlight(active);
        }

        private void RefreshHighlight(bool activeGoal)
        {
            if (optionalHighlightRenderer == null)
            {
                return;
            }

            if (activeGoal && highlightMaterial != null)
            {
                optionalHighlightRenderer.sharedMaterial = highlightMaterial;
            }
            else if (normalMaterial != null)
            {
                optionalHighlightRenderer.sharedMaterial = normalMaterial;
            }
        }

        /// <summary>Вызывается из <see cref="GoalDeliveryZoneTrigger"/> на выбранном trigger-collider.</summary>
        internal void HandleTriggered(Collider other)
        {
            AspectObject aspect = other != null ? other.GetComponentInParent<AspectObject>() : null;
            if (aspect == null || aspect.aspectData == null)
            {
                // Почти всегда это значит, что Collider аспекта не находится в иерархии объекта с AspectObject.
                // Например: коллайдер на отдельном объекте, а AspectObject — на другом.
                return;
            }

            if (GameDirector.Instance == null)
            {
                return;
            }

            bool ok = GameDirector.Instance.NotifyAspectDelivered(aspect.aspectData, out bool wasWrongAspect);
            if (ok)
            {
                if (acceptVfx != null)
                {
                    acceptVfx.Play();
                }

                if (consumeOnAccept)
                {
                    Destroy(aspect.gameObject);
                }
            }
            else
            {
                if (rejectVfx != null)
                {
                    rejectVfx.Play();
                }

                if (consumeOnWrongAspect && wasWrongAspect && !aspect.isInfiniteSource)
                {
                    Destroy(aspect.gameObject);
                }
            }
        }
    }
}
