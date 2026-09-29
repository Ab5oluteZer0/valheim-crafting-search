using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CraftingSearch
{
    // Wyszukiwanie i sortowanie przepisow w kazdym oknie craftingu (stol, kuznia, kociolek,
    // gotowanie, ulepszenia...). Sortowanie korzysta z metod samej gry i jej klucza "sortcraft"
    // (tego z komendy konsoli), dokladajac nazwe od Z do A. Filtr i odwrocona kolejnosc sa
    // nakladane po tym, jak gra zbuduje liste (UpdateRecipeList) - zaznaczenie i sterowanie
    // padem gry dzialaja dalej na juz przefiltrowanej liscie.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.michal.valheim.craftingsearch";
        public const string PluginName = "Crafting Search";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        private static RecipeListControls _controls;
        private static string _query = "";

        private static readonly MethodInfo UpdateCraftingPanelMethod = AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel");

        private void Awake()
        {
            Log = Logger;
            new Harmony(PluginGUID).PatchAll(typeof(Plugin).Assembly);
        }

        // Przebudowa listy przez sama gre - to samo, co gra robi po zmianie zakladki czy zapasow.
        private static void RefreshRecipes()
        {
            var gui = InventoryGui.instance;
            if (gui != null && InventoryGui.IsVisible())
                UpdateCraftingPanelMethod.Invoke(gui, new object[] { false });
        }

        [HarmonyPatch(typeof(InventoryGui), "Awake")]
        private static class InventoryGui_Awake_Patch
        {
            private static void Postfix(InventoryGui __instance)
            {
                try
                {
                    _controls = RecipeListControls.Create(__instance,
                        query =>
                        {
                            _query = query ?? "";
                            RefreshRecipes();
                        },
                        () =>
                        {
                            var player = Player.m_localPlayer;
                            if (player == null)
                                return;
                            SortSettings.Set(player, SortSettings.Next(SortSettings.Current(player)));
                            RefreshRecipes();
                        });
                }
                catch (Exception e)
                {
                    Log.LogError($"Nie udalo sie dodac wyszukiwarki przepisow: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList")]
        private static class InventoryGui_UpdateRecipeList_Patch
        {
            private static void Postfix(InventoryGui __instance)
            {
                try
                {
                    var mode = SortSettings.Current(Player.m_localPlayer);
                    _controls?.SetSortLabel(SortSettings.Label(mode));
                    RecipeList.Apply(__instance, mode, _query);
                }
                catch (Exception e)
                {
                    Log.LogError($"Blad przy filtrowaniu/sortowaniu przepisow: {e}");
                }
            }
        }

        // Nowe otwarcie okna zaczyna od pelnej listy.
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        private static class InventoryGui_Hide_Patch
        {
            private static void Postfix()
            {
                _query = "";
                _controls?.Clear();
            }
        }

        // Gra pomija obsluge klawiszy okna ekwipunku (m.in. E i Tab zamykajace okno) i sterowanie
        // postacia, gdy czat ma fokus. Pisanie w wyszukiwarce zglaszamy tak samo - litery nie
        // zamykaja okna ani nie uruchamiaja akcji, bez wlasnego blokowania klawiszy.
        [HarmonyPatch(typeof(Chat), nameof(Chat.HasFocus))]
        private static class Chat_HasFocus_Patch
        {
            private static void Postfix(ref bool __result)
            {
                if (!__result && _controls != null && _controls.IsTyping)
                    __result = true;
            }
        }
    }
}
