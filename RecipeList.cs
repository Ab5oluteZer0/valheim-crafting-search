using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CraftingSearch
{
    // Tryby sortowania listy przepisow. Original/Name/Type/Weight to metody samej gry, zapisywane
    // w kluczu gracza "sortcraft" - tym samym, ktory ustawia komenda konsoli "sortcraft", wiec mod
    // i konsola zawsze pokazuja to samo. Nazwy od Z do A gra nie ma - to wlasny klucz obok.
    internal enum SortMode
    {
        Original,
        NameAscending,
        NameDescending,
        Type,
        Weight,
    }

    internal static class SortSettings
    {
        private const string GameKey = "sortcraft";
        private const string DescendingKey = "craftingsearch_desc";
        private static readonly SortMode[] Cycle =
            { SortMode.Original, SortMode.NameAscending, SortMode.NameDescending, SortMode.Type, SortMode.Weight };

        public static SortMode Current(Player player)
        {
            if (player == null || !player.TryGetUniqueKeyValue(GameKey, out var value) ||
                !System.Enum.TryParse(value, true, out InventoryGui.SortMethod method))
                return SortMode.Original;
            switch (method)
            {
                case InventoryGui.SortMethod.Name:
                    return player.TryGetUniqueKeyValue(DescendingKey, out _) ? SortMode.NameDescending : SortMode.NameAscending;
                case InventoryGui.SortMethod.Type:
                    return SortMode.Type;
                case InventoryGui.SortMethod.Weight:
                    return SortMode.Weight;
                default:
                    return SortMode.Original;
            }
        }

        public static SortMode Next(SortMode mode) => Cycle[(System.Array.IndexOf(Cycle, mode) + 1) % Cycle.Length];

        public static void Set(Player player, SortMode mode)
        {
            player.RemoveUniqueKeyValue(GameKey);
            player.RemoveUniqueKeyValue(DescendingKey);
            string method = mode switch
            {
                SortMode.NameAscending => nameof(InventoryGui.SortMethod.Name),
                SortMode.NameDescending => nameof(InventoryGui.SortMethod.Name),
                SortMode.Type => nameof(InventoryGui.SortMethod.Type),
                SortMode.Weight => nameof(InventoryGui.SortMethod.Weight),
                _ => null,
            };
            if (method != null)
                player.AddUniqueKeyValue(GameKey, method);
            if (mode == SortMode.NameDescending)
                player.AddUniqueKeyValue(DescendingKey, "1");
        }

        public static string Label(SortMode mode) => mode switch
        {
            SortMode.NameAscending => "Sort: A-Z",
            SortMode.NameDescending => "Sort: Z-A",
            SortMode.Type => "Sort: Type",
            SortMode.Weight => "Sort: Weight",
            _ => "Sort: Default",
        };
    }

    // Operacje na liscie przepisow okna craftingu (InventoryGui.m_availableRecipes), wolane po tym,
    // jak gra zbuduje i posortuje ja w UpdateRecipeList. Elementy listy to prywatna struktura gry
    // (RecipeDataPair), wiec dostep przez refleksje - tylko przy przebudowie listy, nie co klatke.
    internal static class RecipeList
    {
        private static readonly FieldInfo AvailableField = AccessTools.Field(typeof(InventoryGui), "m_availableRecipes");
        private static readonly FieldInfo BaseSizeField = AccessTools.Field(typeof(InventoryGui), "m_recipeListBaseSize");
        private static readonly System.Type PairType = AccessTools.Inner(typeof(InventoryGui), "RecipeDataPair");
        private static readonly PropertyInfo RecipeProperty = AccessTools.Property(PairType, "Recipe");
        private static readonly PropertyInfo ItemDataProperty = AccessTools.Property(PairType, "ItemData");
        private static readonly PropertyInfo ElementProperty = AccessTools.Property(PairType, "InterfaceElement");
        private static readonly PropertyInfo CanCraftProperty = AccessTools.Property(PairType, "CanCraft");

        public static void Apply(InventoryGui gui, SortMode mode, string query)
        {
            if (!(AvailableField.GetValue(gui) is IList list))
                return;

            bool changed = Filter(list, query);
            if (mode == SortMode.NameDescending)
            {
                SortByNameDescending(list);
                changed = true;
            }
            if (changed)
                Relayout(gui, list);
        }

        // Zostaja tylko przepisy, ktorych przetlumaczona nazwa zawiera szukany tekst - bez
        // wzgledu na wielkosc liter i znaki diakrytyczne ("sztylet" znajdzie "Sztylet", "zelazo" - "żelazo").
        private static bool Filter(IList list, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return false;
            string needle = query.Trim();
            var compare = CultureInfo.InvariantCulture.CompareInfo;
            bool removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                string name = LocalizedName(list[i]);
                if (compare.IndexOf(name, needle, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                    continue;
                Object.Destroy((GameObject)ElementProperty.GetValue(list[i]));
                list.RemoveAt(i);
                removed = true;
            }
            return removed;
        }

        // Ta sama kolejnosc kryteriow co sortowanie gry po nazwie (najpierw mozliwe do zrobienia,
        // potem grupa przepisu), tylko nazwa odwrocona.
        private static void SortByNameDescending(IList list)
        {
            var items = new List<object>(list.Count);
            foreach (var item in list)
                items.Add(item);
            items.Sort((a, b) =>
            {
                int result = ((bool)CanCraftProperty.GetValue(b)).CompareTo((bool)CanCraftProperty.GetValue(a));
                if (result == 0)
                    result = Recipe(a).m_listSortWeight.CompareTo(Recipe(b).m_listSortWeight);
                if (result == 0)
                    result = string.Compare(LocalizedName(b), LocalizedName(a), System.StringComparison.CurrentCulture);
                if (result == 0)
                    result = Quality(b).CompareTo(Quality(a));
                return result;
            });
            for (int i = 0; i < items.Count; i++)
                list[i] = items[i];
        }

        private static void Relayout(InventoryGui gui, IList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var element = (GameObject)ElementProperty.GetValue(list[i]);
                ((RectTransform)element.transform).anchoredPosition = new Vector2(0f, i * -gui.m_recipeListSpace);
            }
            float baseSize = (float)BaseSizeField.GetValue(gui);
            gui.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(baseSize, list.Count * gui.m_recipeListSpace));
        }

        private static Recipe Recipe(object pair) => (Recipe)RecipeProperty.GetValue(pair);

        private static int Quality(object pair) => ItemDataProperty.GetValue(pair) is ItemDrop.ItemData data ? data.m_quality : 0;

        private static string LocalizedName(object pair)
        {
            string token = Recipe(pair).m_item.m_itemData.m_shared.m_name;
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }
}
