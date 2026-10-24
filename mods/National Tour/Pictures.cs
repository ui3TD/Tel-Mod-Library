using HarmonyLib;
using System.IO;
using UnityEngine;

namespace NationalTour
{
    /// <summary>
    /// The mod's three pictures: the map of Japan, the National Tour picture and its blurred copy.
    /// </summary>
    public static class Pictures
    {
        public const string MOD_TITLE = "National Tour";
        public const string MOD_TEXTURE_DIR = "Patch";
        public const string TOUR_POPUP_BG_FILE = "World_tour_def.jpg";
        public const string TOUR_POPUP_BG_BLURRED_FILE = "World_tour_def_BG.jpg";
        public const string TOUR_MAP_FILE = "TOUR_map_2.jpg";

        public static Sprite TOUR_map_2;
        public static Sprite World_tour_def;
        public static Texture World_tour_def_tex;
        public static Sprite World_tour_def_BG;

        /// <summary>
        /// The mod's folder: the one the running DLL was loaded from (IM-HarmonyIntegration loads
        /// "&lt;mod folder&gt;/&lt;HarmonyID&gt;.dll"), so the pictures match the code. If that folder has no
        /// pictures, the mod named MOD_TITLE in the game's mod list. Null if neither has them.
        /// </summary>
        public static string GetModDirectory(string assemblyDir)
        {
            if (HasPictures(assemblyDir))
                return assemblyDir;

            foreach (Mods._mod mod in Mods._Mods)
            {
                if (mod.Title == MOD_TITLE && HasPictures(mod.GetPath()))
                    return mod.GetPath();
            }
            return null;
        }

        private static bool HasPictures(string dir) =>
            !string.IsNullOrEmpty(dir) && Directory.Exists(Path.Combine(Path.Combine(dir, "Textures"), MOD_TEXTURE_DIR));
    }


    // Load the pictures. Once is enough: they stay loaded from one save to the next.
    [HarmonyPatch(typeof(mainScript), nameof(mainScript.Start))]
    public class mainScript_Start
    {
        public static void Postfix()
        {
            if (mainScript.IsMainMenu())
                return;

            // Already loaded (Unity's == is also true for a picture the engine has since destroyed)
            if (Pictures.World_tour_def != null && Pictures.World_tour_def_BG != null && Pictures.TOUR_map_2 != null)
                return;

            string modDir = Pictures.GetModDirectory(Path.GetDirectoryName(typeof(Pictures).Assembly.Location));
            if (modDir == null)
            {
                Debug.LogError("[National Tour] Couldn't find the mod's Textures folder, so there is no National Tour tab");
                return;
            }

            string imageDir = Path.Combine(modDir, "Textures", Pictures.MOD_TEXTURE_DIR);
            string path_World_tour_def = Path.GetFullPath(Path.Combine(imageDir, Pictures.TOUR_POPUP_BG_FILE));
            string path_World_tour_def_BG = Path.GetFullPath(Path.Combine(imageDir, Pictures.TOUR_POPUP_BG_BLURRED_FILE));
            string path_TOUR_map_2 = Path.GetFullPath(Path.Combine(imageDir, Pictures.TOUR_MAP_FILE));

            Pictures.World_tour_def = IMG2Sprite.instance.LoadNewSprite(path_World_tour_def, 100f);
            // The sprite covers the whole picture, so its texture is the picture itself
            Pictures.World_tour_def_tex = Pictures.World_tour_def != null ? Pictures.World_tour_def.texture : null;
            Pictures.World_tour_def_BG = IMG2Sprite.instance.LoadNewSprite(path_World_tour_def_BG, 100f);
            Pictures.TOUR_map_2 = IMG2Sprite.instance.LoadNewSprite(path_TOUR_map_2, 100f);
        }
    }
}
