using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ConcertHype
{
    /// <summary>
    /// The lineup icons from the mod's Images folder, each loaded once as a sprite.
    /// </summary>
    public static class Icons
    {
        public const string Folder = "Images";

        private static readonly Dictionary<IconKind, Sprite> Loaded = new Dictionary<IconKind, Sprite>();
        private static readonly HashSet<IconKind> Failed = new HashSet<IconKind>();

        public static string FileName(IconKind icon) => icon.ToString().ToLowerInvariant() + ".png";

        /// <summary>
        /// The icon's sprite; null for no icon, or (logged once) when it can't be loaded.
        /// </summary>
        public static Sprite Get(IconKind icon)
        {
            if (icon == IconKind.None || Failed.Contains(icon))
                return null;
            if (Loaded.TryGetValue(icon, out Sprite sprite) && sprite != null)
                return sprite;

            string folder = ModFolder.Find();
            string path = folder == null ? null : Path.Combine(Path.Combine(folder, Folder), FileName(icon));
            Texture2D texture = path != null && File.Exists(path) ? new Texture2D(2, 2, TextureFormat.RGBA32, false) : null;
            if (texture != null && ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = "ConcertHype " + icon;
                Loaded[icon] = sprite;
                return sprite;
            }
            Failed.Add(icon);
            Log.Warn($"couldn't load {Folder}/{FileName(icon)}; its place shows a text symbol instead");
            return null;
        }
    }
}
