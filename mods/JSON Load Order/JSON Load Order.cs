using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using SimpleJSON;
using System.IO;
using static JSONLoadOrder.ModLoadOrder;

namespace JSONLoadOrder
{
    public class ModLoadOrder
    {
        public const string loadOrderFieldID = "JSONLoadOrder";

        public static Dictionary<string, int> modOrders = new();
    }

    // Set load order before loading the game constants
    [HarmonyPatch(typeof(Language), "_Load")]
    public class Language__Load
    {
        public static void Prefix()
        {
            if (Mods._Mods.Count == 0)
                return;

            foreach (Mods._mod mod in Mods._Mods)
            {
                string modDir = mod.Path;

                string modInfoFile = Path.Combine(modDir.TrimEnd(new char[] { Path.DirectorySeparatorChar }), "info.json");
                JSONNode modInfo = mainScript.ProcessInboundData(File.ReadAllText(modInfoFile));
                string orderStr = modInfo[loadOrderFieldID];

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

}
