using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftingSearch
{
    // Pole wyszukiwania nad lista przepisow (na cala jej szerokosc) i przycisk sortowania w rzedzie
    // zakladek Craft/Upgrade, przy prawej krawedzi panelu opisu. Polozenie liczone wzgledem
    // elementow okna gry (lista, zakladka, przycisk Craft), a nie pikseli - dziala przy kazdej
    // skali interfejsu. Lista kurczy sie o wysokosc pola.
    internal sealed class RecipeListControls
    {
        private const float RowHeight = 28f;
        private const float RowGap = 4f;
        private const float SortButtonWidth = 120f;
        private const int MaxQueryLength = 40;

        private readonly TMP_InputField _input;
        private readonly TMP_Text _sortLabel;

        public bool IsTyping => _input != null && _input.isFocused;

        private RecipeListControls(TMP_InputField input, TMP_Text sortLabel)
        {
            _input = input;
            _sortLabel = sortLabel;
        }

        public static RecipeListControls Create(InventoryGui gui, Action<string> onQueryChanged, Action onSortClicked)
        {
            var list = gui.m_recipeListRoot.parent as RectTransform;
            var container = list != null ? list.parent as RectTransform : null;
            if (list == null || container == null)
            {
                Plugin.Log.LogWarning("Nie znaleziono prostokata listy przepisow - wyszukiwarka nie zostanie dodana.");
                return null;
            }
            LogLayout(gui, list);

            var row = new GameObject("CraftingSearchRow", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(container, false);
            // Poziomo jak lista, pionowo: pas o wysokosci wiersza przy jej dotychczasowej gornej krawedzi.
            row.anchorMin = new Vector2(list.anchorMin.x, list.anchorMax.y);
            row.anchorMax = list.anchorMax;
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(list.offsetMin.x, list.offsetMax.y - RowHeight);
            row.offsetMax = list.offsetMax;
            ShrinkTop(list, RowHeight + RowGap);
            if (gui.m_recipeListScroll != null && gui.m_recipeListScroll.transform.parent == container)
                ShrinkTop((RectTransform)gui.m_recipeListScroll.transform, RowHeight + RowGap);

            var font = gui.m_recipeName != null ? gui.m_recipeName.font : null;
            var input = CreateInput(row, font);
            input.onValueChanged.AddListener(text => onQueryChanged(text));

            var sortLabel = CreateSortButton(gui, onSortClicked);
            return new RecipeListControls(input, sortLabel);
        }

        public void SetSortLabel(string text)
        {
            if (_sortLabel != null && _sortLabel.text != text)
                _sortLabel.text = text;
        }

        public void Clear()
        {
            if (_input == null)
                return;
            _input.SetTextWithoutNotify("");
            _input.DeactivateInputField();
        }

        private static void ShrinkTop(RectTransform rect, float amount)
        {
            rect.offsetMax = new Vector2(rect.offsetMax.x, rect.offsetMax.y - amount);
        }

        private static TMP_InputField CreateInput(RectTransform row, TMP_FontAsset font)
        {
            var go = new GameObject("RecipeSearch", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(row, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Tlo pola tekstowego z atlasu interfejsu gry (to samo co pola wpisywania w grze).
            var background = go.AddComponent<Image>();
            var sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == "text_field");
            if (sprite != null)
            {
                background.sprite = sprite;
                background.type = Image.Type.Sliced;
                background.color = Color.white;
            }
            else
            {
                background.color = new Color(0f, 0f, 0f, 0.6f);
            }

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            area.SetParent(rect, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(8f, 2f);
            area.offsetMax = new Vector2(-8f, -2f);

            var placeholder = CreateText(area, "Placeholder", font, new Color(0.8529f, 0.725f, 0.5331f, 0.5f));
            placeholder.text = "Search recipes...";
            placeholder.fontStyle = FontStyles.Italic;
            var text = CreateText(area, "Text", font, new Color(0.8529f, 0.725f, 0.5331f, 1f));

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = MaxQueryLength;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = new Color(1f, 0.631f, 0.235f, 1f);
            input.selectionColor = new Color(1f, 0.631f, 0.235f, 0.35f);
            if (font != null)
                input.fontAsset = font;
            input.pointSize = 16f;
            return input;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, TMP_FontAsset font, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSize = 16f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        // Klon przycisku gry (jak "Sort" w Auto Sort Chests) - ten sam wyglad co reszta okna. Stoi w
        // rzedzie zakladek: w pionie jak zakladka Upgrade, prawa krawedzia przy prawej krawedzi
        // przycisku Craft (czyli panelu opisu przepisu).
        private static TMP_Text CreateSortButton(InventoryGui gui, Action onSortClicked)
        {
            var tab = (RectTransform)gui.m_tabUpgrade.transform;
            var parent = (RectTransform)tab.parent;
            var clone = UnityEngine.Object.Instantiate(gui.m_stackAllButton.gameObject, parent);
            clone.name = "CraftingSortButton";

            // Komponent tlumaczenia z przycisku "Place stacks" nadpisuje tekst przy kazdym pokazaniu
            // okna - kopia ma wlasna etykiete.
            foreach (var localize in clone.GetComponentsInChildren<Localize>(true))
                UnityEngine.Object.DestroyImmediate(localize);

            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, tab.anchorMin.y);
            rect.anchorMax = new Vector2(0f, tab.anchorMax.y);
            rect.pivot = new Vector2(1f, tab.pivot.y);
            rect.sizeDelta = new Vector2(SortButtonWidth, tab.rect.height);
            float right = parent.InverseTransformPoint(RightEdge((RectTransform)gui.m_craftButton.transform)).x;
            float left = parent.rect.xMin;
            rect.anchoredPosition = new Vector2(right - left, tab.anchoredPosition.y);

            var button = clone.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSortClicked());

            // Podpowiedz pada skopiowana ze StackAll - ten przycisk nie ma wpiecia do pada.
            foreach (Transform child in clone.transform)
                if (child.name.StartsWith("gamepad_hint", StringComparison.Ordinal))
                    child.gameObject.SetActive(false);

            return clone.GetComponentInChildren<TMP_Text>();
        }

        private static Vector3 RightEdge(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[2] + corners[3]) * 0.5f;
        }

        // Do strojenia ukladu na podstawie logu, gdyby okno craftingu w jakiejs wersji gry wygladalo inaczej.
        private static void LogLayout(InventoryGui gui, RectTransform list)
        {
            Plugin.Log.LogInfo($"Lista przepisow: '{list.name}' w '{list.parent.name}', anchor {list.anchorMin}-{list.anchorMax}, offset {list.offsetMin}/{list.offsetMax}, rozmiar {list.rect.size}; " +
                               $"scroll: '{(gui.m_recipeListScroll != null ? gui.m_recipeListScroll.transform.parent.name : "-")}'");
        }
    }
}
