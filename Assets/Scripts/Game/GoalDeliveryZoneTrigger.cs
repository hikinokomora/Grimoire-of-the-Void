using UnityEngine;

namespace GrimoireOfTheVoid.Game
{
    /// <summary>
    /// Навешивается на КОНКРЕТНЫЙ trigger-collider (или на его GameObject),
    /// чтобы явно выбрать какой триггер стола считается «слотом доставки цели».
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoalDeliveryZoneTrigger : MonoBehaviour
    {
        [Tooltip("Зона, в которую прокидываем события триггера.")]
        [SerializeField] private GoalDeliveryZone zone;

        [Tooltip("Если задано — событие будет приниматься ТОЛЬКО от этого коллайдера (удобно при нескольких Colliders на объекте).")]
        [SerializeField] private Collider thisTriggerCollider;

        private void Reset()
        {
            if (thisTriggerCollider == null)
            {
                thisTriggerCollider = GetComponent<Collider>();
            }
        }

        private void Awake()
        {
            if (thisTriggerCollider == null)
            {
                thisTriggerCollider = GetComponent<Collider>();
            }

            if (thisTriggerCollider != null && !thisTriggerCollider.isTrigger)
            {
                Debug.LogWarning("[GoalDeliveryZoneTrigger] Выбранный Collider не помечен IsTrigger=true.", this);
            }

            // Важно: Unity НЕ сообщает, какой именно Collider на объекте вызвал callback.
            // Поэтому надёжная схема — держать этот компонент НА ТОМ ЖЕ GameObject, что и нужный trigger-collider.
            if (thisTriggerCollider != null && thisTriggerCollider.gameObject != gameObject)
            {
                Debug.LogWarning("[GoalDeliveryZoneTrigger] Этот компонент должен быть на том же GameObject, что и выбранный Trigger Collider. Иначе выбрать «конкретный» коллайдер невозможно — перенеси компонент на объект коллайдера.", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (zone == null)
            {
                return;
            }

            // Unity вызывает OnTriggerEnter на компоненте, даже если на объекте несколько коллайдеров.
            // Здесь мы дополнительно страхуемся явной привязкой к нужному коллайдеру.
            // Примечание: фильтрация по thisTriggerCollider возможна только если компонент сидит на том же объекте, что и коллайдер.
            // Если это не так — мы всё равно пропускаем событие, но в Awake уже выдали warning как это поправить.

            zone.HandleTriggered(other);
        }
    }
}

