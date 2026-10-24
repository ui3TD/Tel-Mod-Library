using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HarmonyChecker
{
    /// <summary>
    /// A picture in this mod's Images folder, loaded once as a sprite.
    /// </summary>
    internal sealed class ModPicture
    {
        // This mod's title in its info.json, by which its Workshop folder is found.
        public const string ModTitle = "IM-HarmonyIntegration Plugin";

        public readonly string RelativePath;
        public readonly Vector4 Border;
        private Sprite sprite;
        private bool loadFailed;

        public ModPicture(string fileName, Vector4 border)
        {
            RelativePath = Path.Combine("Images", fileName);
            Border = border;
        }

        /// <summary>The picture, loaded once; null (logged once) if it can't be loaded.</summary>
        public Sprite Get()
        {
            if (sprite != null || loadFailed)
            {
                return sprite;
            }

            string path = FindFile(RelativePath, Path.GetDirectoryName(typeof(ModPicture).Assembly.Location),
                Mods._Mods.Where(mod => mod.Title == ModTitle).Select(mod => mod.GetPath()));
            Texture2D texture = path != null ? new Texture2D(2, 2, TextureFormat.RGBA32, false) : null;
            if (texture != null && ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, Border);
                sprite.name = Path.GetFileNameWithoutExtension(RelativePath);
            }
            else
            {
                loadFailed = true;
                Debug.LogError("[Harmony Checker] couldn't load " + RelativePath + "; the menu is drawn without it.");
            }
            return sprite;
        }

        /// <summary>The picture's path: next to the DLL, else in a mod folder with this mod's title; null if neither has it.</summary>
        public static string FindFile(string relativePath, string assemblyDir, IEnumerable<string> modDirs)
        {
            return new[] { assemblyDir }.Concat(modDirs)
                .Where(dir => !string.IsNullOrEmpty(dir))
                .Select(dir => Path.Combine(dir, relativePath))
                .FirstOrDefault(File.Exists);
        }
    }
}
