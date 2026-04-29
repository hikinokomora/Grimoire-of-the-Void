using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GrimoireOfTheVoid.Crafting
{
    /// <summary>
    /// Один каталог мира: все <see cref="OccultAspect"/>, разблокировка сессии по ID, каноничная ссылка по ID.
    /// </summary>
    public static class OccultAspectRegistry
    {
        /// <summary>
        /// Подпапка внутри любой <c>Resources</c> (например <c>Assets/Resources/OccultCatalog</c>).
        /// Не используем <c>LoadAll</c> с путём "" — Unity тогда обходит все Resources (TMP, пост-обработка и т.д.),
        /// что даёт ложные предупреждения «The referenced script (Unknown) is missing» на чужих ассетах.
        /// </summary>
        public const string ResourcesCatalogSubfolder = "OccultCatalog";

        private static bool _initialized;
        private static readonly List<OccultAspect> _ordered = new List<OccultAspect>(32);
        private static readonly Dictionary<string, OccultAspect> _byId = new Dictionary<string, OccultAspect>(StringComparer.Ordinal);
        private static readonly HashSet<string> _sessionRevealedIds = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> _sessionImageRevealedIds = new HashSet<string>(StringComparer.Ordinal);

        private sealed class Runner : MonoBehaviour { }
        private static Runner _runner;
        private static Coroutine _revealAllRoutine;
        private static HashSet<string> _revealAllPrevRevealed;
        private static HashSet<string> _revealAllPrevImages;
        private static Dictionary<string, bool> _revealAllPrevSessionUnlocked;
        private static bool _revealAllActive;

        private static Runner EnsureRunner()
        {
            if (_runner != null) return _runner;
            GameObject go = new GameObject("OccultAspectRegistryRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
            return _runner;
        }

        public static bool IsInitialized => _initialized;

        /// <param name="inspectorList">Список из инспектора (котёл/сцена); может быть null.</param>
        /// <param name="alsoMergeResources">Дополнить каталогом из Resources, если ID ещё нет.</param>
        public static void Initialize(IList<OccultAspect> inspectorList, bool alsoMergeResources)
        {
            if (_initialized) return;
            _ordered.Clear();
            _byId.Clear();
            _sessionRevealedIds.Clear();
            _sessionImageRevealedIds.Clear();
            if (inspectorList != null) MergeIn(inspectorList, "inspector");
            if (alsoMergeResources)
            {
                OccultAspect[] r = Resources.LoadAll<OccultAspect>(ResourcesCatalogSubfolder);
                if (r != null && r.Length > 0) MergeIn(r, "Resources");
            }
            if (_byId.Count == 0)
            {
                Debug.LogError("[OccultAspectRegistry] Каталог пуст. Добавь аспекты в AspectManager (AspectCatalog) и/или в Resources/" + ResourcesCatalogSubfolder + " (только OccultAspect).");
            }
            else
            {
                foreach (OccultAspect a in _ordered)
                {
                    a.sessionUnlocked = a.isUnlocked;
                    if (a.isUnlocked && !string.IsNullOrEmpty(a.ID))
                    {
                        _sessionRevealedIds.Add(a.ID);
                        _sessionImageRevealedIds.Add(a.ID);
                    }
                }
            }
            _initialized = true;
            Debug.Log($"[OccultAspectRegistry] Инициализация: аспектов = {_byId.Count}");
        }

        private static void MergeIn(IEnumerable<OccultAspect> items, string source)
        {
            foreach (OccultAspect a in items)
            {
                if (a == null) continue;
                if (string.IsNullOrEmpty(a.ID))
                {
                    Debug.LogWarning($"[OccultAspectRegistry] Пропущен {a.name} ({source}): пустой ID");
                    continue;
                }
                if (_byId.ContainsKey(a.ID))
                {
                    continue;
                }
                _byId.Add(a.ID, a);
                _ordered.Add(a);
            }
        }

        public static IReadOnlyList<OccultAspect> AllOrdered => _ordered;

        public static List<OccultAspect> CloneOrderedList() => new List<OccultAspect>(_ordered);

        public static int Count => _byId.Count;

        public static OccultAspect GetCanonical(OccultAspect a)
        {
            if (a == null) return null;
            if (!string.IsNullOrEmpty(a.ID) && _byId.TryGetValue(a.ID, out OccultAspect c)) return c;
            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_ordered[i] == a) return a;
            }
            return null;
        }

        /// <summary>Добавляет в каталог, если в инспектор/Resources забыли (например Flesh только в папке рецепта).</summary>
        public static void RegisterAdHocIfMissing(OccultAspect a)
        {
            if (a == null || string.IsNullOrEmpty(a.ID)) return;
            EnsureDefaultFromResources();
            if (!_initialized) return;
            if (_byId.ContainsKey(a.ID)) return;
            _byId.Add(a.ID, a);
            _ordered.Add(a);
            a.sessionUnlocked = a.isUnlocked;
            if (a.isUnlocked)
            {
                _sessionRevealedIds.Add(a.ID);
                _sessionImageRevealedIds.Add(a.ID);
            }
            Debug.Log($"[OccultAspectRegistry] Добавлен ad-hoc аспект: {a.ID}");
        }

        public static void UnlockForSession(OccultAspect a)
        {
            if (a == null) return;
            EnsureDefaultFromResources();
            if (!_initialized) return;
            if (!string.IsNullOrEmpty(a.ID) && !_byId.ContainsKey(a.ID)) RegisterAdHocIfMissing(a);
            OccultAspect c = GetCanonical(a) ?? a;
            if (c == null) return;
            c.sessionUnlocked = true;
            if (!string.IsNullOrEmpty(c.ID))
            {
                _sessionRevealedIds.Add(c.ID);
                _sessionImageRevealedIds.Add(c.ID);
            }
        }

        public static void UnlockImageForSession(OccultAspect a)
        {
            if (a == null) return;
            EnsureDefaultFromResources();
            if (!_initialized) return;
            if (!string.IsNullOrEmpty(a.ID) && !_byId.ContainsKey(a.ID)) RegisterAdHocIfMissing(a);
            OccultAspect c = GetCanonical(a) ?? a;
            if (c == null) return;
            if (!string.IsNullOrEmpty(c.ID)) _sessionImageRevealedIds.Add(c.ID);
        }

        public static void RevealImageAndNotifyUI(OccultAspect aspect, bool goToPageForAspect = true)
        {
            if (aspect == null) return;
            EnsureDefaultFromResources();
            UnlockImageForSession(aspect);
            NotifyBooks(goToPageForAspect ? aspect : null);
        }

        /// <summary>Постоянно раскрывает только рецепт (текст) для одного аспекта и обновляет книги.</summary>
        public static void RevealRecipeAndNotifyUI(OccultAspect aspect, bool goToPageForAspect = true)
        {
            if (aspect == null) return;
            EnsureDefaultFromResources();
            if (!_initialized) return;
            if (!string.IsNullOrEmpty(aspect.ID) && !_byId.ContainsKey(aspect.ID)) RegisterAdHocIfMissing(aspect);
            OccultAspect c = GetCanonical(aspect) ?? aspect;
            if (c == null) return;
            c.sessionUnlocked = true;
            if (!string.IsNullOrEmpty(c.ID))
            {
                _sessionRevealedIds.Add(c.ID);
            }
            NotifyBooks(goToPageForAspect ? c : null);
        }

        public static void RevealAllForSecondsAndNotifyUI(float seconds)
        {
            EnsureDefaultFromResources();
            if (!_initialized) return;
            Runner r = EnsureRunner();
            if (_revealAllRoutine != null)
            {
                r.StopCoroutine(_revealAllRoutine);
                RestoreRevealAllSnapshot();
            }
            _revealAllActive = true;

            _revealAllPrevRevealed = new HashSet<string>(_sessionRevealedIds, StringComparer.Ordinal);
            _revealAllPrevImages = new HashSet<string>(_sessionImageRevealedIds, StringComparer.Ordinal);
            _revealAllPrevSessionUnlocked = new Dictionary<string, bool>(StringComparer.Ordinal);

            for (int i = 0; i < _ordered.Count; i++)
            {
                OccultAspect a = _ordered[i];
                if (a == null || string.IsNullOrEmpty(a.ID)) continue;
                OccultAspect c = GetCanonical(a) ?? a;
                if (c != null) _revealAllPrevSessionUnlocked[c.ID] = c.sessionUnlocked;
                _sessionRevealedIds.Add(a.ID);
                _sessionImageRevealedIds.Add(a.ID);
                if (c != null) c.sessionUnlocked = true;
            }

            NotifyBooks(null);
            _revealAllRoutine = r.StartCoroutine(CoRestoreRevealAllAfter(seconds));
        }

        private static IEnumerator CoRestoreRevealAllAfter(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            RestoreRevealAllSnapshot();
            _revealAllActive = false;
            NotifyBooks(null);
            _revealAllRoutine = null;
        }

        /// <summary>
        /// Debug/cheat: показать все рецепты (и иллюстрации) во всех книгах до отключения.
        /// Возвращает предыдущие состояния при выключении.
        /// </summary>
        public static void SetRevealAllAndNotifyUI(bool enabled)
        {
            EnsureDefaultFromResources();
            if (!_initialized) return;

            if (enabled)
            {
                if (_revealAllActive) return;

                if (_revealAllRoutine != null)
                {
                    Runner r = EnsureRunner();
                    r.StopCoroutine(_revealAllRoutine);
                    _revealAllRoutine = null;
                }

                _revealAllPrevRevealed = new HashSet<string>(_sessionRevealedIds, StringComparer.Ordinal);
                _revealAllPrevImages = new HashSet<string>(_sessionImageRevealedIds, StringComparer.Ordinal);
                _revealAllPrevSessionUnlocked = new Dictionary<string, bool>(StringComparer.Ordinal);

                for (int i = 0; i < _ordered.Count; i++)
                {
                    OccultAspect a = _ordered[i];
                    if (a == null || string.IsNullOrEmpty(a.ID)) continue;
                    OccultAspect c = GetCanonical(a) ?? a;
                    if (c != null) _revealAllPrevSessionUnlocked[c.ID] = c.sessionUnlocked;
                    _sessionRevealedIds.Add(a.ID);
                    _sessionImageRevealedIds.Add(a.ID);
                    if (c != null) c.sessionUnlocked = true;
                }

                _revealAllActive = true;
                NotifyBooks(null);
                return;
            }

            if (!_revealAllActive) return;
            RestoreRevealAllSnapshot();
            _revealAllActive = false;
            NotifyBooks(null);
        }

        private static void RestoreRevealAllSnapshot()
        {
            if (_revealAllPrevRevealed == null || _revealAllPrevImages == null || _revealAllPrevSessionUnlocked == null) return;

            _sessionRevealedIds.Clear();
            foreach (string id in _revealAllPrevRevealed) _sessionRevealedIds.Add(id);

            _sessionImageRevealedIds.Clear();
            foreach (string id in _revealAllPrevImages) _sessionImageRevealedIds.Add(id);

            for (int i = 0; i < _ordered.Count; i++)
            {
                OccultAspect a = _ordered[i];
                if (a == null || string.IsNullOrEmpty(a.ID)) continue;
                OccultAspect c = GetCanonical(a) ?? a;
                if (c == null) continue;
                if (_revealAllPrevSessionUnlocked.TryGetValue(c.ID, out bool prev)) c.sessionUnlocked = prev;
            }

            _revealAllPrevRevealed = null;
            _revealAllPrevImages = null;
            _revealAllPrevSessionUnlocked = null;
        }

        private static void NotifyBooks(OccultAspect focusPhysicalBookPage)
        {
            PhysicalRecipeBook[] pBooks = UnityEngine.Object.FindObjectsByType<PhysicalRecipeBook>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < pBooks.Length; i++)
            {
                if (pBooks[i] != null) pBooks[i].SyncFromRegistry(focusPhysicalBookPage);
            }
            RecipeBook[] uiBooks = UnityEngine.Object.FindObjectsByType<RecipeBook>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < uiBooks.Length; i++)
            {
                if (uiBooks[i] != null) uiBooks[i].SyncFromRegistry();
            }
        }

        public static bool IsRevealedForPage(OccultAspect d)
        {
            if (d == null) return false;
            if (!_initialized)
            {
                return d.isUnlocked || d.sessionUnlocked
                    || string.IsNullOrWhiteSpace(d.ingredientsText)
                    || d.ingredientsText.Trim() == "Нет данных";
            }
            if (d.isUnlocked) return true;
            string ingText = d.ingredientsText;
            if (string.IsNullOrWhiteSpace(ingText) || ingText.Trim() == "Нет данных")
            {
                return true;
            }
            if (!string.IsNullOrEmpty(d.ID) && _sessionRevealedIds.Contains(d.ID)) return true;
            return d.sessionUnlocked;
        }

        /// <summary>Иллюстрация в книге: отдельно от текста рецепта; при крафте открывается вместе с рецептом (<see cref="UnlockForSession"/>).</summary>
        public static bool IsImageRevealedForPage(OccultAspect d)
        {
            if (d == null) return false;
            if (!_initialized)
            {
                return d.isUnlocked || d.sessionUnlocked
                    || string.IsNullOrWhiteSpace(d.ingredientsText)
                    || d.ingredientsText.Trim() == "Нет данных";
            }
            if (d.isUnlocked) return true;
            string ingText = d.ingredientsText;
            if (string.IsNullOrWhiteSpace(ingText) || ingText.Trim() == "Нет данных")
            {
                return true;
            }
            if (!string.IsNullOrEmpty(d.ID) && _sessionImageRevealedIds.Contains(d.ID)) return true;
            return false;
        }

        public static void EnsureDefaultFromResources()
        {
            if (_initialized) return;
            Initialize(null, true);
        }

        public static void UnlockAndNotifyUI(OccultAspect aspect)
        {
            if (aspect == null) return;
            EnsureDefaultFromResources();
            UnlockForSession(aspect);
            NotifyBooks(aspect);
        }
    }
}
