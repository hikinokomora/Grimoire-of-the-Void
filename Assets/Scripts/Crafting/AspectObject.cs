using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Представляет физический объект аспекта на сцене (например, колбу на столе), 
    /// который можно положить в котел.
    /// </summary>
    public class AspectObject : MonoBehaviour
    {
        [Tooltip("Данные аспекта, который представляет этот физический объект")]
        public OccultAspect aspectData;

        [Tooltip("Если true, объект не будет уничтожен при добавлении в котел (бесконечный базовый элемент).")]
        public bool isInfiniteSource = false;

        private Collider[] _cachedColliders;
        private Rigidbody[] _cachedBodies;
        private bool[] _storedKinematic;
        private bool[] _storedUseGravity;

        private void Awake()
        {
            _cachedColliders = GetComponentsInChildren<Collider>(true);
            _cachedBodies = GetComponentsInChildren<Rigidbody>(true);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(ReEnableColliders));
        }

        /// <summary>
        /// Временная «льгота» после спавна: отключаем коллайдеры и замораживаем Rigidbody,
        /// чтобы только что созданный клон не застревал/не улетал/не проваливался.
        /// </summary>
        public void ApplySpawnGrace(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            if (_cachedColliders == null || _cachedColliders.Length == 0)
            {
                _cachedColliders = GetComponentsInChildren<Collider>(true);
            }

            if (_cachedBodies == null)
            {
                _cachedBodies = GetComponentsInChildren<Rigidbody>(true);
            }

            if (_cachedBodies.Length > 0)
            {
                if (_storedKinematic == null || _storedKinematic.Length != _cachedBodies.Length)
                {
                    _storedKinematic = new bool[_cachedBodies.Length];
                    _storedUseGravity = new bool[_cachedBodies.Length];
                }

                for (int i = 0; i < _cachedBodies.Length; i++)
                {
                    Rigidbody rb = _cachedBodies[i];
                    if (rb == null) continue;
                    _storedKinematic[i] = rb.isKinematic;
                    _storedUseGravity[i] = rb.useGravity;
                    // Сначала гасим скорость, потом делаем кинематикой — иначе Unity спамит warn'ами.
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }
            }

            for (int i = 0; i < _cachedColliders.Length; i++)
            {
                if (_cachedColliders[i] != null)
                {
                    _cachedColliders[i].enabled = false;
                }
            }

            CancelInvoke(nameof(ReEnableColliders));
            Invoke(nameof(ReEnableColliders), seconds);
        }

        private void ReEnableColliders()
        {
            if (_cachedColliders == null) return;
            for (int i = 0; i < _cachedColliders.Length; i++)
            {
                if (_cachedColliders[i] != null)
                {
                    _cachedColliders[i].enabled = true;
                }
            }

            if (_cachedBodies == null || _cachedBodies.Length == 0) return;
            if (_storedKinematic == null || _storedUseGravity == null) return;
            if (_storedKinematic.Length != _cachedBodies.Length || _storedUseGravity.Length != _cachedBodies.Length) return;

            for (int i = 0; i < _cachedBodies.Length; i++)
            {
                Rigidbody rb = _cachedBodies[i];
                if (rb == null) continue;
                rb.isKinematic = _storedKinematic[i];
                rb.useGravity = _storedUseGravity[i];
            }
        }

        // Опционально: Rigidbody на корне — <see cref="GrimoireOfTheVoid.Crafting.CraftingInteractor"/>
        // вращает/двигает кинематикой (<see cref="UnityEngine.Rigidbody.MovePosition"/>) в режиме стола.
    }
}
