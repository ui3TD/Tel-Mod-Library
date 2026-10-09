using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using SimpleJSON;
using System.IO;
using System.Reflection;
using static JSONLoadOrder.ModLoadOrder;

namespace JSONLoadOrder
{
    public class ModLoadOrder
    {
        public const string LOADORDER_JSON_FIELD = "JSONLoadOrder";

        public static Dictionary<string, int> modOrders = new();

        // Sorts the game's mod list by each mod's JSONLoadOrder, which sets the order mods' JSON files are read in
        public static void SortMods()
        {
            if (Mods._Mods.Count == 0)
                return;

            foreach (Mods._mod mod in Mods._Mods)
            {
                modOrders[mod.ModName] = ReadOrder(mod);
            }

            List<Mods._mod> sortedMods = Mods._Mods.OrderBy(mod => modOrders[mod.ModName]).ToList();
            Mods._Mods = sortedMods;
        }

        // A mod's JSONLoadOrder, or 0. A mod whose info.json can't be read gets 0 with a warning, so it can't
        // stop the other mods being sorted or the game's text loading.
        private static int ReadOrder(Mods._mod mod)
        {
            try
            {
                string modInfoFile = Path.Combine(mod.Path.TrimEnd(new char[] { Path.DirectorySeparatorChar }), "info.json");
                JSONNode modInfo = mainScript.ProcessInboundData(File.ReadAllText(modInfoFile));
                string orderStr = modInfo?[LOADORDER_JSON_FIELD];
                return orderStr != null && int.TryParse(orderStr, out int order) ? order : 0;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[JSON Load Order] Couldn't read " + mod.ModName + "'s info.json, so it loads at 0: " + e.Message);
                return 0;
            }
        }
    }

    // Set load order before loading the game constants
    [HarmonyPatch(typeof(Language), nameof(Language._Load))]
    public class Language__Load
    {
        // Also sort as soon as the mod is applied, right after the game loads its mods: some of the game's JSON
        // (e.g. policies, trivia, dialogues) is read before the constants when a save loads
        public static void Prepare(MethodBase original)
        {
            if (original != null)
                return;

            // A failure here would stop the mod being applied at all; the constants load sorts again anyway.
            // SortMods already skips unreadable info.json files, so this only catches the unexpected.
            try
            {
                SortMods();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[JSON Load Order] Couldn't sort the mods: " + e);
            }
        }

        // The game's text and constants load right after this, so a failure must not reach Language._Load
        public static void Prefix()
        {
            try
            {
                SortMods();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[JSON Load Order] Couldn't sort the mods: " + e);
            }
        }
    }

}
