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

        private static Sprite cancel;
        private static bool cancelLooked;
        private static UnityEngine.UI.Image emptyPlace;
        private static bool emptyPlaceLooked;

        public static string FileName(IconKind icon) => icon.ToString().ToLowerInvariant() + ".png";

        /// <summary>
        /// The game's own cancel icon, from the agency idol list's job cancel button (logged once when missing).
        /// It's only borrowed, never copied into the mod.
        /// </summary>
        public static Sprite GameCancel()
        {
            if (cancel != null || cancelLooked)
                return cancel;
            cancelLooked = true;
            foreach (GirlButtonSmall button in Resources.FindObjectsOfTypeAll<GirlButtonSmall>())
            {
                if (button == null || button.Button_Cancel == null)
                    continue;
                foreach (UnityEngine.UI.Image image in button.Button_Cancel.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if (image.sprite != null)
                        return cancel = image.sprite;
                }
            }
            foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite != null && sprite.name == "icon_cancel")
                    return cancel = sprite;
            }
            Log.Warn("couldn't find the game's cancel icon; the talk break host shows none on hover");
            return null;
        }

        /// <summary>
        /// The game's empty senbatsu place (a framed white box, drawn sliced), from a senbatsu place of the show or
        /// single popup, as it draws it; null (logged once) when missing.
        /// </summary>
        public static UnityEngine.UI.Image GameEmptyPlace()
        {
            if (emptyPlace != null || emptyPlaceLooked)
                return emptyPlace;
            emptyPlaceLooked = true;
            foreach (SenbatsuReciever place in Resources.FindObjectsOfTypeAll<SenbatsuReciever>())
            {
                UnityEngine.UI.Image image = place == null ? null : place.GetComponent<UnityEngine.UI.Image>();
                if (image != null && image.sprite != null)
                    return emptyPlace = image;
            }
            Log.Warn("couldn't find the game's empty senbatsu place; empty places show no picture");
            return null;
        }

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
