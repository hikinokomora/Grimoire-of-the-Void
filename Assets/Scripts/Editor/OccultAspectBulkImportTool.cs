#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GrimoireOfTheVoid.Crafting;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bulk-import OccultAspect + Recipe assets from a hardcoded table text.
/// Idempotent: updates existing assets by ID / name rather than duplicating.
/// </summary>
public static class OccultAspectBulkImportTool
{
    private const string AspectOutDir = "Assets/Resources/" + OccultAspectRegistry.ResourcesCatalogSubfolder;
    private const string RecipesOutDir = "Assets/Resources/OccultRecipes";
    
    // RU name -> EN id base (human translation, not transliteration).
    // If you add new aspects later, extend this map to keep IDs stable and readable.
    private static readonly Dictionary<string, string> RuToEnIdBase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Tier 1
        { "Кровь", "blood" },
        { "Кость", "bone" },
        { "Огонь", "fire" },
        { "Тьма", "darkness" },
        { "Слизь", "slime" },

        // Tier 2
        { "Плоть", "flesh" },
        { "Дух", "spirit" },
        { "Пепел", "ash" },
        { "Гниль", "rot" },
        { "Демон-кровь", "demon_blood" },
        { "Порча", "corruption" },
        { "Прах", "dust" },
        { "Бездна", "abyss" },
        { "Кислота", "acid" },

        // Tier 3
        { "Мертвец", "undead" },
        { "Призрак", "ghost" },
        { "Мутант", "mutant" },
        { "Безумие", "madness" },
        { "Упырь", "ghoul" },
        { "Младший бес", "imp" },
        { "Чума", "plague" },
        { "Голем", "golem" },

        // Tier 4
        { "Повелитель мух", "lord_of_flies" },
        { "Кошмар", "nightmare" },
        { "Архидемон", "archdemon" },
        { "Шепот Древних", "whispers_of_the_ancients" },
        { "Философский камень", "philosophers_stone" },
    };

    [MenuItem("Grimoire/Import/Occult Aspects (from chat list)")]
    public static void ImportFromChatList()
    {
        try
        {
            EnsureFolderPath(AspectOutDir);
            EnsureFolderPath(RecipesOutDir);

            List<Row> rows = ParseRows(ChatList);
            if (rows.Count == 0)
            {
                Debug.LogWarning("[OccultImport] Nothing to import: parsed 0 rows.");
                return;
            }

            // 1) Create/update aspects first (so recipes can reference them).
            Dictionary<string, OccultAspect> byNameRu = new Dictionary<string, OccultAspect>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, OccultAspect> byId = new Dictionary<string, OccultAspect>(StringComparer.Ordinal);

            // Load existing aspects in output folder to be idempotent.
            foreach (string guid in AssetDatabase.FindAssets("t:OccultAspect", new[] { AspectOutDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var a = AssetDatabase.LoadAssetAtPath<OccultAspect>(path);
                if (a == null) continue;
                if (!string.IsNullOrEmpty(a.ID) && !byId.ContainsKey(a.ID)) byId.Add(a.ID, a);
                if (!string.IsNullOrEmpty(a.DisplayName) && !byNameRu.ContainsKey(a.DisplayName)) byNameRu.Add(a.DisplayName, a);
            }

            HashSet<string> usedIds = new HashSet<string>(byId.Keys, StringComparer.Ordinal);
            int createdAspects = 0, updatedAspects = 0;

            // Pre-generate deterministic IDs for all rows to allow forward references.
            Dictionary<string, string> nameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Row r in rows)
            {
                if (nameToId.ContainsKey(r.AspectNameRu)) continue;
                string proposed = MakeIdFromRuName(r.AspectNameRu);
                string unique = MakeUniqueId(proposed, usedIds);
                nameToId.Add(r.AspectNameRu, unique);
                usedIds.Add(unique);
            }

            foreach (Row r in rows)
            {
                string id = nameToId[r.AspectNameRu];
                OccultAspect a = null;

                // Prefer match by ID (if already exists), else by DisplayName.
                if (byId.TryGetValue(id, out OccultAspect byIdExisting) && byIdExisting != null)
                {
                    a = byIdExisting;
                }
                else if (byNameRu.TryGetValue(r.AspectNameRu, out OccultAspect byNameExisting) && byNameExisting != null)
                {
                    a = byNameExisting;
                }

                bool isNew = a == null;
                if (isNew)
                {
                    a = ScriptableObject.CreateInstance<OccultAspect>();
                    string fileName = SanitizeFileName($"{id}.asset");
                    string outPath = AssetDatabase.GenerateUniqueAssetPath($"{AspectOutDir}/{fileName}");
                    AssetDatabase.CreateAsset(a, outPath);
                    createdAspects++;
                }
                else
                {
                    updatedAspects++;
                }

                a.ID = id;
                a.DisplayName = r.AspectNameRu;
                a.description = r.DescriptionUi ?? string.Empty;

                // Base aspects: keep ingredientsText empty so registry treats them as already known.
                a.ingredientsText = r.IsBase ? string.Empty : (r.RecipeTextForBook ?? string.Empty);

                a.tier = Mathf.Max(0, r.Tier);
                a.isUnlocked = r.IsBase; // base aspects are known at run start

                EditorUtility.SetDirty(a);
                if (!byId.ContainsKey(a.ID)) byId[a.ID] = a;
                if (!byNameRu.ContainsKey(a.DisplayName)) byNameRu[a.DisplayName] = a;
            }

            // 2) Create/update recipe assets (only for non-base with parseable recipe).
            int createdRecipes = 0, updatedRecipes = 0, skippedRecipes = 0;

            // Load existing recipes we previously generated.
            Dictionary<string, Recipe> recipeByKey = new Dictionary<string, Recipe>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Recipe", new[] { RecipesOutDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var rec = AssetDatabase.LoadAssetAtPath<Recipe>(path);
                if (rec == null) continue;
                string key = MakeRecipeKey(rec);
                if (!string.IsNullOrEmpty(key) && !recipeByKey.ContainsKey(key)) recipeByKey.Add(key, rec);
            }

            foreach (Row r in rows)
            {
                if (r.IsBase)
                {
                    continue;
                }

                if (r.RecipeIngredientsRu == null || r.RecipeIngredientsRu.Count == 0)
                {
                    skippedRecipes++;
                    continue;
                }

                // Resolve ingredients to created aspects by RU name (from table).
                List<OccultAspect> inputs = new List<OccultAspect>(r.RecipeIngredientsRu.Count);
                bool allResolved = true;
                for (int i = 0; i < r.RecipeIngredientsRu.Count; i++)
                {
                    string ingRu = r.RecipeIngredientsRu[i];
                    if (!nameToId.TryGetValue(ingRu, out string ingId))
                    {
                        allResolved = false;
                        break;
                    }
                    if (!byId.TryGetValue(ingId, out OccultAspect ingAspect) || ingAspect == null)
                    {
                        allResolved = false;
                        break;
                    }
                    inputs.Add(ingAspect);
                }

                if (!allResolved)
                {
                    Debug.LogWarning($"[OccultImport] Skip recipe for '{r.AspectNameRu}': can't resolve all inputs from table.");
                    skippedRecipes++;
                    continue;
                }

                OccultAspect output = byId[nameToId[r.AspectNameRu]];
                string desiredKey = MakeRecipeKey(inputs, output);

                Recipe recAsset = null;
                if (recipeByKey.TryGetValue(desiredKey, out Recipe existingRec) && existingRec != null)
                {
                    recAsset = existingRec;
                    updatedRecipes++;
                }
                else
                {
                    recAsset = ScriptableObject.CreateInstance<Recipe>();
                    string fileName = SanitizeFileName($"recipe_{output.ID}.asset");
                    string outPath = AssetDatabase.GenerateUniqueAssetPath($"{RecipesOutDir}/{fileName}");
                    AssetDatabase.CreateAsset(recAsset, outPath);
                    createdRecipes++;
                    recipeByKey[desiredKey] = recAsset;
                }

                recAsset.inputs = inputs;
                recAsset.output = output;
                EditorUtility.SetDirty(recAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[OccultImport] Done. Aspects: +{createdAspects} / ~{updatedAspects}. Recipes: +{createdRecipes} / ~{updatedRecipes} / skip {skippedRecipes}. " +
                      $"Aspects folder: {AspectOutDir}, recipes folder: {RecipesOutDir}");
        }
        catch (Exception e)
        {
            Debug.LogError("[OccultImport] Failed: " + e);
        }
    }

    private static string MakeRecipeKey(Recipe r)
    {
        if (r == null || r.output == null || r.inputs == null || r.inputs.Count == 0) return string.Empty;
        return MakeRecipeKey(r.inputs, r.output);
    }

    private static string MakeRecipeKey(List<OccultAspect> inputs, OccultAspect output)
    {
        string inKey = string.Join("+", inputs.Where(x => x != null).Select(x => (x.ID ?? string.Empty).Trim().ToLowerInvariant()));
        string outKey = ((output != null ? output.ID : string.Empty) ?? string.Empty).Trim().ToLowerInvariant();
        return $"{inKey}=>{outKey}";
    }

    private static string MakeUniqueId(string baseId, HashSet<string> used)
    {
        if (string.IsNullOrWhiteSpace(baseId)) baseId = "aspect";
        string id = baseId;
        int n = 2;
        while (used.Contains(id))
        {
            id = $"{baseId}_{n}";
            n++;
        }
        return id;
    }

    private static string MakeIdFromRuName(string ru)
    {
        string src = (ru ?? string.Empty).Trim();
        if (RuToEnIdBase.TryGetValue(src, out string translated) && !string.IsNullOrWhiteSpace(translated))
        {
            return translated.Trim().ToLowerInvariant();
        }

        Debug.LogWarning($"[OccultImport] Missing RU→EN ID translation for '{src}'. Falling back to transliteration.");

        string t = TransliterateRuToEn(src);
        t = t.ToLowerInvariant().Trim();

        // keep [a-z0-9_]
        var sb = new StringBuilder(t.Length);
        bool prevUnderscore = false;
        foreach (char ch in t)
        {
            bool ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
            if (ok)
            {
                sb.Append(ch);
                prevUnderscore = false;
                continue;
            }

            if (ch == '_' || ch == ' ' || ch == '-' || ch == '.' || ch == '/')
            {
                if (!prevUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    prevUnderscore = true;
                }
            }
        }

        string res = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(res) ? "aspect" : res;
    }

    private static string TransliterateRuToEn(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new StringBuilder(input.Length * 2);

        foreach (char c in input)
        {
            switch (c)
            {
                case 'А': case 'а': sb.Append("a"); break;
                case 'Б': case 'б': sb.Append("b"); break;
                case 'В': case 'в': sb.Append("v"); break;
                case 'Г': case 'г': sb.Append("g"); break;
                case 'Д': case 'д': sb.Append("d"); break;
                case 'Е': case 'е': sb.Append("e"); break;
                case 'Ё': case 'ё': sb.Append("yo"); break;
                case 'Ж': case 'ж': sb.Append("zh"); break;
                case 'З': case 'з': sb.Append("z"); break;
                case 'И': case 'и': sb.Append("i"); break;
                case 'Й': case 'й': sb.Append("y"); break;
                case 'К': case 'к': sb.Append("k"); break;
                case 'Л': case 'л': sb.Append("l"); break;
                case 'М': case 'м': sb.Append("m"); break;
                case 'Н': case 'н': sb.Append("n"); break;
                case 'О': case 'о': sb.Append("o"); break;
                case 'П': case 'п': sb.Append("p"); break;
                case 'Р': case 'р': sb.Append("r"); break;
                case 'С': case 'с': sb.Append("s"); break;
                case 'Т': case 'т': sb.Append("t"); break;
                case 'У': case 'у': sb.Append("u"); break;
                case 'Ф': case 'ф': sb.Append("f"); break;
                case 'Х': case 'х': sb.Append("kh"); break;
                case 'Ц': case 'ц': sb.Append("ts"); break;
                case 'Ч': case 'ч': sb.Append("ch"); break;
                case 'Ш': case 'ш': sb.Append("sh"); break;
                case 'Щ': case 'щ': sb.Append("shch"); break;
                case 'Ы': case 'ы': sb.Append("y"); break;
                case 'Э': case 'э': sb.Append("e"); break;
                case 'Ю': case 'ю': sb.Append("yu"); break;
                case 'Я': case 'я': sb.Append("ya"); break;
                case 'Ь': case 'ь': break;
                case 'Ъ': case 'ъ': break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

    private static string SanitizeFileName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "asset.asset";
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            s = s.Replace(c, '_');
        }
        return s;
    }

    private static void EnsureFolderPath(string assetPath)
    {
        // assetPath like "Assets/Resources/OccultCatalog"
        string[] parts = assetPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private sealed class Row
    {
        public int Tier;
        public string AspectNameRu;
        public bool IsBase;
        public string RecipeTextForBook;
        public List<string> RecipeIngredientsRu;
        public string DescriptionUi;
    }

    private static List<Row> ParseRows(string raw)
    {
        List<Row> rows = new List<Row>(64);
        if (string.IsNullOrWhiteSpace(raw)) return rows;

        int currentTier = 0;
        string[] lines = raw.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = (lines[i] ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // headers like "1 тир", "2 тир", "3тир"
            if (TryParseTierHeader(line, out int tier))
            {
                currentTier = tier;
                continue;
            }

            if (line.Equals("Аспект", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("Рецепт", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("Описание", StringComparison.OrdinalIgnoreCase)) continue;

            // Expect 3-line blocks: name, recipe, description
            string name = line;
            if (i + 2 >= lines.Length) break;
            string recipe = (lines[i + 1] ?? string.Empty).Trim();
            string desc = (lines[i + 2] ?? string.Empty).Trim();
            i += 2;

            Row r = new Row
            {
                Tier = currentTier,
                AspectNameRu = name,
                DescriptionUi = desc,
            };

            if (recipe.Equals("Базовый", StringComparison.OrdinalIgnoreCase))
            {
                r.IsBase = true;
                r.RecipeTextForBook = string.Empty;
                r.RecipeIngredientsRu = new List<string>();
            }
            else
            {
                r.IsBase = false;
                r.RecipeTextForBook = recipe;
                r.RecipeIngredientsRu = ParseRecipeIngredients(recipe);
            }

            rows.Add(r);
        }

        return rows;
    }

    private static bool TryParseTierHeader(string line, out int tier)
    {
        tier = 0;
        // normalize "3тир" -> "3 тир"
        string s = line.Replace(" ", string.Empty).ToLowerInvariant();
        if (!s.EndsWith("тир")) return false;
        string num = s.Substring(0, s.Length - "тир".Length);
        return int.TryParse(num, out tier);
    }

    private static List<string> ParseRecipeIngredients(string recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe)) return new List<string>();

        // Strip trailing "(3)" markers etc.
        string cleaned = recipe;
        int paren = cleaned.IndexOf('(');
        if (paren >= 0) cleaned = cleaned.Substring(0, paren).Trim();

        // Split by '+'
        string[] parts = cleaned.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> res = new List<string>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (string.IsNullOrEmpty(p)) continue;
            res.Add(p);
        }
        return res;
    }

    // Hardcoded list copied from chat.
    private const string ChatList = @"
1 тир
Аспект
Рецепт (Слияние)
Описание (Для UI)
Кровь
Базовый
Ток жизни.
Кость
Базовый
Каркас смертных.
Огонь
Базовый
Очищающее пламя.
Тьма
Базовый
Отсутствие света.
Слизь
Базовый
Мерзкая влага подземелий.

2 тир
Аспект
Рецепт (Слияние)
Описание (Для UI)
Плоть
Кровь + Кость
Мягкая оболочка.
Дух
Огонь + Тьма
Бесплотная энергия.
Пепел
Огонь + Кость
То, что остается после.
Гниль
Слизь + Кость
Смерть, которая пахнет.
Демон-кровь
Кровь + Огонь
Кипит и обжигает.
Порча
Кровь + Тьма
Болезнь самой сути.
Прах
Тьма + Пепел
Забытые останки.
Бездна
Слизь + Тьма
Глубины, где нет дна.
Кислота
Слизь + Огонь
Разъедает даже камень.

3тир
Аспект
Рецепт (Слияние)
Описание (Для UI)
Мертвец
Плоть + Дух
Поднятый из могилы слуга.
Призрак
Пепел + Дух
Эхо чужой боли.
Мутант
Плоть + Порча
Искаженная мерзость.
Безумие
Бездна + Порча
Разум, разорванный в клочья.
Упырь
Кость + Плоть + Тьма (3)
Вечно голодный людоед.
Младший бес
Дух + Кровь + Огонь (3)
Мелкий пакостник из преисподней.
Чума
Гниль + Прах + Тьма (3)
Невидимая смерть.
Голем
Кость + Кость + Кость (3)
Абсолютная твердость.

4 тир
Аспект
Рецепт (Слияние)
Описание (Для UI)
Повелитель мух
Мертвец + Чума + Демон-кровь
Вестник распада и болезней.
Кошмар
Призрак + Безумие + Бездна
То, что прячется в темноте.
Архидемон
Младший бес + Мутант + Плоть
Воплощение чистого зла.
Шепот Древних
Безумие + Прах + Дух
Сводит с ума одним звуком.
Философский камень
Кровь + Огонь + Бездна (3)
Идеальная пища для Пустоты.
";
}
#endif

