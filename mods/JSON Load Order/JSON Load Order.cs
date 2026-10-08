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
                string modDir = mod.Path;

                string modInfoFile = Path.Combine(modDir.TrimEnd(new char[] { Path.DirectorySeparatorChar }), "info.json");
                JSONNode modInfo = mainScript.ProcessInboundData(File.ReadAllText(modInfoFile));
                string orderStr = modInfo[LOADORDER_JSON_FIELD];

                modOrders[mod.ModName] = 0;

                if (orderStr != null && int.TryParse(orderStr, out int order))
                {
                    modOrders[mod.ModName] = order;
                }
            }

            List<Mods._mod> sortedMods = Mods._Mods.OrderBy(mod => modOrders[mod.ModName]).ToList();
            Mods._Mods = sortedMods;
        }
    }

    // Set load order before loading the game constants
    [HarmonyPatch(typeof(Language), "_Load")]
    public class Language__Load
    {
        // Also sort as soon as the mod is applied, right after the game loads its mods: some of the game's JSON
        // (e.g. policies, trivia, dialogues) is read before the constants when a save loads
        public static void Prepare(MethodBase original)
        {
            if (original != null)
                return;

            // A failure here would stop the mod being applied at all; the constants load sorts again anyway
            try
            {
                SortMods();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[JSON Load Order] Couldn't sort the mods: " + e);
            }
        }

        public static void Prefix()
        {
            SortMods();
        }
    }

}
