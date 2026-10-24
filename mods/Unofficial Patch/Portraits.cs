using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace UnofficialPatch
{
    // Base game bugfix:
    // The audition popup keeps every card locked until all the candidates' portraits have loaded, and
    // portraits render one at a time through a queue the whole game shares. A render that never finished
    // kept the popup locked, and stopped every later portrait in the game for the rest of the session.
    //
    // Fix:
    // After 2 s the cards unlock. A card whose portrait hasn't loaded shows a clear placeholder: an unopened
    // card has no silhouette, an opened card no face, and the stats panel no image. Each portrait fills in
    // wherever it's shown when it arrives.
    // A render that takes over 2 s lets the queue move on and finishes in the background (at most 3 at once,
    // each for up to 60 s).
    // Cards can now close before their portraits load, so portrait loaders stop once everything they'd set a
    // portrait on is destroyed, and closing an audition drops the queued portraits of candidates who weren't hired.
    internal static class PortraitLoading
    {
        // Seconds the audition waits for portraits before it unlocks the cards.
        internal const float UnlockSeconds = 2f;
        // Seconds a portrait render can hold the shared queue.
        internal const float RenderSeconds = 2f;
        // Renders that can keep running after letting the queue move on.
        internal const int MaxLateRenders = 3;
        // Seconds such a render can keep running before it's stopped.
        internal const float LateRenderSeconds = 60f;

        private static readonly FieldInfo TexturesInstance = AccessTools.Field(typeof(data_girls_textures), nameof(data_girls_textures._this));
        private static readonly FieldInfo QueueField = AccessTools.Field(typeof(data_girls_textures), nameof(data_girls_textures.Queue));
        private static readonly FieldInfo QueueGirl = AccessTools.Field(AccessTools.Inner(typeof(data_girls_textures), nameof(data_girls_textures._queue)), nameof(data_girls_textures._queue.Girl));
        private static readonly FieldInfo GoldenCardPortrait = AccessTools.Field(typeof(Audition_Golden_Card), nameof(Audition_Golden_Card.Portrait_));

        // The clear sprite on cards whose portrait hasn't loaded. Made the first time it's needed.
        internal static Sprite placeholder;
        // When the audition popup last laid out its cards.
        internal static float cardsLoadedAt;
        // Renderers still running after letting the queue move on.
        internal static readonly List<GameObject> lateRenderers = new List<GameObject>();

        internal static Sprite Placeholder
        {
            get
            {
                if (placeholder == null)
                    placeholder = CreatePlaceholder();
                return placeholder;
            }
        }

        private static Sprite CreatePlaceholder()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();
            // Closing the audition popup unloads unused assets; the placeholder is kept for the session.
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        internal static bool IsPlaceholder(Sprite sprite)
        {
            return !ReferenceEquals(sprite, null) && ReferenceEquals(sprite, placeholder);
        }

        // Real time, so a paused game still counts. A lambda, so tests can replace the clock.
        internal static Func<float> Clock = () => Time.realtimeSinceStartup;

        internal static float Now()
        {
            return Clock();
        }

        // The game's portrait loader and queue.
        internal static data_girls_textures Textures()
        {
            return TexturesInstance != null ? TexturesInstance.GetValue(null) as data_girls_textures : null;
        }

        // Runs a portrait loader until it finishes or every object it would set the portrait on is destroyed.
        // Its waits are checked here each frame, so it also stops while it's still waiting for the render.
        internal static IEnumerator WhileTargetsExist(IEnumerator loader, List<GameObject> targets)
        {
            while (AnyExists(targets) && loader.MoveNext())
            {
                CustomYieldInstruction wait = loader.Current as CustomYieldInstruction;
                if (wait == null)
                {
                    yield return loader.Current;
                    continue;
                }
                while (wait.keepWaiting)
                {
                    if (!AnyExists(targets))
                        yield break;
                    yield return null;
                }
            }
        }

        private static bool AnyExists(List<GameObject> targets)
        {
            foreach (GameObject target in targets)
            {
                if (target != null)
                    return true;
            }
            return false;
        }

        // A portrait that arrived after its card was opened: the opened card keeps a copy to show again when
        // she's clicked, and the stats panel gets it if it still shows her.
        internal static void ShowOpenedPortrait(Popup_Audition popup, Auditions.data._girl girl, Audition_Golden_Card card)
        {
            if (card == null || card.Portrait == null)
                return;
            Image image = card.Portrait.GetComponent<Image>();
            Sprite sprite = image != null ? image.sprite : null;
            // A load that failed is queued again and later sets only the opened card.
            if (sprite == null || IsPlaceholder(sprite))
                return;

            if (GoldenCardPortrait != null)
                GoldenCardPortrait.SetValue(card, sprite);

            Audition_Data_Card panel = popup != null && popup.Card_Container != null ? popup.Card_Container.GetComponent<Audition_Data_Card>() : null;
            if (panel == null || !ReferenceEquals(panel.Girl, girl))
                return;
            SetSprite(panel.Portrait, sprite);
            SetSprite(panel.Portrait_Shadow, sprite);
        }

        private static void SetSprite(GameObject obj, Sprite sprite)
        {
            Image image = obj != null ? obj.GetComponent<Image>() : null;
            if (image != null)
                image.sprite = sprite;
        }

        // Removes the queued portrait jobs of these idols. A job that's already running finishes.
        internal static int DropQueuedPortraits(ICollection<data_girls.girls> girls)
        {
            IList queue = QueueField != null ? QueueField.GetValue(null) as IList : null;
            if (queue == null || QueueGirl == null || girls.Count == 0)
                return 0;

            int dropped = 0;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (queue[i] != null && QueueGirl.GetValue(queue[i]) is data_girls.girls girl && girls.Contains(girl))
                {
                    queue.RemoveAt(i);
                    dropped++;
                }
            }
            return dropped;
        }

        // Renders still running after letting the queue move on. A scene change destroys them.
        internal static int LateRenderCount()
        {
            lateRenderers.RemoveAll(r => r == null);
            return lateRenderers.Count;
        }

        // The queue's wait for a portrait render: until it finishes, or once 2 s have passed while fewer than
        // 3 renders are already running late.
        public static WaitUntil WaitForRender(Func<bool> rendered)
        {
            float deadline = Now() + RenderSeconds;
            return new WaitUntil(() => rendered() || (Now() >= deadline && LateRenderCount() < MaxLateRenders));
        }

        // Called where the queue destroys the renderer. A render that hasn't finished keeps running.
        public static void DestroyRenderer(GameObject renderer)
        {
            Portrait_Renderer portrait = renderer != null ? renderer.GetComponent<Portrait_Renderer>() : null;
            data_girls.girls girl = portrait != null ? portrait.Girl : null;
            data_girls_textures textures = Textures();
            if (girl == null || girl.texture == null || girl.texture.cached || textures == null)
            {
                UnityEngine.Object.Destroy(renderer);
                return;
            }

            lateRenderers.Add(renderer);
            PatchLog.WarnOncePerPatch<data_girls_textures_NEW_Cache_Portrait>(
                "a portrait took over " + RenderSeconds + " s to render, so the next portrait started while it finishes.");
            textures.StartCoroutine(FinishLateRender(renderer, girl));
        }

        private static IEnumerator FinishLateRender(GameObject renderer, data_girls.girls girl)
        {
            float stopAt = Now() + LateRenderSeconds;
            while (renderer != null && !girl.texture.cached && Now() < stopAt)
                yield return null;

            lateRenderers.RemoveAll(r => ReferenceEquals(r, renderer));
            if (renderer != null)
                UnityEngine.Object.Destroy(renderer);

            if (!girl.texture.cached)
            {
                PatchLog.Warn<data_girls_textures_NEW_Cache_Portrait>("a portrait render didn't finish in " + LateRenderSeconds + " s and was stopped.");
                yield break;
            }
            // What the queue does after a render.
            if (girl.Update != null)
                girl.Update();
            if (girl.TexturesUpdate != null)
                girl.TexturesUpdate();
        }
    }

    // Starts the 2 s wait for the audition's portraits.
    [HarmonyPatch(typeof(Popup_Audition), nameof(Popup_Audition.LoadCards))]
    public class Popup_Audition_LoadCards
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Popup_Audition_LoadCards>(ex);

        public static void Prefix()
        {
            PortraitLoading.cardsLoadedAt = PortraitLoading.Now();
        }
    }

    // After 2 s the audition's cards unlock; those still without a portrait get the clear placeholder.
    // Each card's loader keeps running and sets her portrait when it arrives.
    [HarmonyPatch(typeof(Popup_Audition), nameof(Popup_Audition.PortraitsLoaded))]
    public class Popup_Audition_PortraitsLoaded
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Popup_Audition_PortraitsLoaded>(ex);

        public static void Postfix(Popup_Audition __instance, ref bool __result)
        {
            try
            {
                if (__result || PortraitLoading.Now() - PortraitLoading.cardsLoadedAt < PortraitLoading.UnlockSeconds)
                    return;

                foreach (Audition_Closed_Card card in __instance.Cards_Container.GetComponentsInChildren<Audition_Closed_Card>(true))
                {
                    Image image = card != null && card.Portrait != null ? card.Portrait.GetComponent<Image>() : null;
                    if (image != null && image.sprite == null)
                        image.sprite = PortraitLoading.Placeholder;
                }
                __result = true;
            }
            catch (Exception ex)
            {
                PatchLog.WarnOncePerPatch<Popup_Audition_PortraitsLoaded>("failed: " + ex);
            }
        }
    }

    // A card opened before her portrait arrived starts empty. The card being opened is destroyed, which stops
    // its loader, so the opened card loads the portrait itself.
    [HarmonyPatch(typeof(Popup_Audition), nameof(Popup_Audition.OpenCard))]
    public class Popup_Audition_OpenCard
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Popup_Audition_OpenCard>(ex);

        public static void Postfix(Popup_Audition __instance, Auditions.data._girl girl, Sprite portrait)
        {
            try
            {
                if (!PortraitLoading.IsPlaceholder(portrait) || girl == null || girl.girl == null || girl.CardObject == null)
                    return;

                Audition_Golden_Card card = girl.CardObject.GetComponent<Audition_Golden_Card>();
                data_girls_textures textures = PortraitLoading.Textures();
                if (card == null || textures == null)
                    return;

                List<GameObject> targets = new List<GameObject> { card.Portrait, card.PortraitShadow };
                textures.StartCoroutine(textures.setPortrait(girl.girl, targets, 0f, () => PortraitLoading.ShowOpenedPortrait(__instance, girl, card)));
            }
            catch (Exception ex)
            {
                PatchLog.WarnOncePerPatch<Popup_Audition_OpenCard>("failed: " + ex);
            }
        }
    }

    // Closing an audition drops the queued portraits of candidates who weren't hired, so the queue doesn't
    // render faces nobody will see before the next portrait the game needs.
    [HarmonyPatch(typeof(Popup_Audition), nameof(Popup_Audition.Close))]
    public class Popup_Audition_Close
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Popup_Audition_Close>(ex);

        private static readonly FieldInfo DataField = AccessTools.Field(typeof(Popup_Audition), nameof(Popup_Audition.Data));

        public static void Prefix(Popup_Audition __instance)
        {
            try
            {
                Auditions.data data = DataField != null ? DataField.GetValue(__instance) as Auditions.data : null;
                if (data == null || data.Girls == null)
                    return;

                HashSet<data_girls.girls> notHired = new HashSet<data_girls.girls>();
                foreach (Auditions.data._girl candidate in data.Girls)
                {
                    if (candidate != null && candidate.girl != null && (data_girls.girl == null || !data_girls.girl.Contains(candidate.girl)))
                        notHired.Add(candidate.girl);
                }
                PortraitLoading.DropQueuedPortraits(notHired);
            }
            catch (Exception ex)
            {
                PatchLog.WarnOncePerPatch<Popup_Audition_Close>("failed: " + ex);
            }
        }
    }

    // Portrait loaders stop once everything they'd set the portrait on is destroyed. The game's loader kept
    // running, then threw when it reached the destroyed object.
    [HarmonyPatch(typeof(data_girls_textures), nameof(data_girls_textures.setPortrait))]
    public class data_girls_textures_setPortrait
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_textures_setPortrait>(ex);

        public static void Postfix(List<GameObject> target, ref IEnumerator __result)
        {
            if (__result != null && target != null && target.Count > 0)
                __result = PortraitLoading.WhileTargetsExist(__result, target);
        }
    }

    // Skips setting a portrait on a destroyed object. The queue's job that retries a failed portrait load sets
    // it before starting the next job, so the error stopped every later portrait for the rest of the session.
    [HarmonyPatch(typeof(data_girls_textures), nameof(data_girls_textures.SetSprite))]
    public class data_girls_textures_SetSprite
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_textures_SetSprite>(ex);

        public static bool Prefix(GameObject obj)
        {
            return obj != null;
        }
    }

    // A portrait render that takes over 2 s lets the queue move on, and finishes in the background.
    // Each renderer has its own camera and render texture and is placed apart from the others, so renders
    // can run side by side.
    [HarmonyPatch(typeof(data_girls_textures), nameof(data_girls_textures.NEW_Cache_Portrait), MethodType.Enumerator)]
    public class data_girls_textures_NEW_Cache_Portrait
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_textures_NEW_Cache_Portrait>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            ConstructorInfo newWait = AccessTools.Constructor(typeof(WaitUntil), new Type[] { typeof(Func<bool>) });
            MethodInfo destroy = AccessTools.Method(typeof(UnityEngine.Object), nameof(UnityEngine.Object.Destroy), new Type[] { typeof(UnityEngine.Object) });
            MethodInfo waitForRender = AccessTools.Method(typeof(PortraitLoading), nameof(PortraitLoading.WaitForRender));
            MethodInfo destroyRenderer = AccessTools.Method(typeof(PortraitLoading), nameof(PortraitLoading.DestroyRenderer));
            if (newWait == null || destroy == null || waitForRender == null || destroyRenderer == null)
            {
                PatchLog.WarnOncePerPatch<data_girls_textures_NEW_Cache_Portrait>("method lookup failed.");
                return instructions;
            }

            List<CodeInstruction> codes = instructions.ToList();
            List<CodeInstruction> waits = codes.Where(ci => ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo ctor && ctor == newWait).ToList();
            List<CodeInstruction> destroys = codes.Where(ci => IlHelpers.IsCallTo(ci, destroy)).ToList();
            if (waits.Count != 1 || destroys.Count != 1)
            {
                PatchLog.WarnOncePerPatch<data_girls_textures_NEW_Cache_Portrait>(
                    "expected one render wait and one renderer destroy, found " + waits.Count + " and " + destroys.Count + ".");
                return codes;
            }

            waits[0].opcode = OpCodes.Call;
            waits[0].operand = waitForRender;
            destroys[0].opcode = OpCodes.Call;
            destroys[0].operand = destroyRenderer;
            return codes;
        }
    }

    // Fixes a promoted male producer's blank small portrait: the staff thumbnail atlas has Player_Female_Pro but no
    // Player_Male_Pro. Both patches below fill in this mod's ready-made picture (assets/Images) only when the atlas
    // lookup came back empty, so a mod that supplies the sprite or takes over LoadPortrait_Small wins.
    public static class PlayerThumbnail
    {
        public const string ModTitle = "Unofficial Patch";
        public static readonly string RelativePath = Path.Combine("Images", "Player_Male_Pro.png");

        private static Sprite sprite;
        private static bool loadFailed;

        /// <summary>True for the one thumbnail the atlas lacks: small, promoted, the male producer, not unique staff.</summary>
        public static bool IsMissingThumbnail(staff._type type, bool big, bool pro, staff._staff._unique_type uniqueType)
        {
            return !big && pro && uniqueType == staff._staff._unique_type.NONE && type == staff._type.player;
        }

        /// <summary>The picture's path: next to the DLL, else in a mod folder with this mod's title; null if neither has it.</summary>
        public static string FindFile(string assemblyDir, IEnumerable<string> modDirs)
        {
            return new[] { assemblyDir }.Concat(modDirs)
                .Where(dir => !string.IsNullOrEmpty(dir))
                .Select(dir => Path.Combine(dir, RelativePath))
                .FirstOrDefault(File.Exists);
        }

        /// <summary>The picture, loaded once; null (logged once) if it can't be loaded, which leaves the game's blank portrait.</summary>
        public static Sprite Get()
        {
            if (sprite != null || loadFailed)
                return sprite;

            string path = FindFile(Path.GetDirectoryName(typeof(PlayerThumbnail).Assembly.Location),
                Mods._Mods.Where(mod => mod.Title == ModTitle).Select(mod => mod.GetPath()));
            sprite = path != null ? IMG2Sprite.instance.LoadNewSprite(path, 100f) : null;
            if (sprite != null)
                sprite.name = "Player_Male_Pro";
            else
            {
                loadFailed = true;
                PatchLog.Error<staff_GetPortrait>("couldn't load " + RelativePath + "; a promoted male producer keeps the game's blank thumbnail.");
            }
            return sprite;
        }
    }

    // Thumbnails once the atlas is loaded: LoadPortrait_Small uses GetPortrait's sprite if it isn't null.
    [HarmonyPatch(typeof(staff), nameof(staff.GetPortrait))]
    public class staff_GetPortrait
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<staff_GetPortrait>(ex);

        public static void Postfix(staff._type Type, bool Big, bool Pro, staff._staff._unique_type UniqueType, SpriteAtlas ___Portrait_Thumbs, ref Sprite __result)
        {
            // A null atlas means it isn't loaded yet: LoadPortrait_Small then loads it, and the patch below takes over
            if (__result == null && ___Portrait_Thumbs != null && PlayerThumbnail.IsMissingThumbnail(Type, Big, Pro, UniqueType))
                __result = PlayerThumbnail.Get();
        }
    }

    // The first thumbnail: LoadPortrait_Small loads the atlas and sets the sprite in a callback (a compiler-made closure).
    [HarmonyPatch]
    public class staff_LoadPortrait_Small_AtlasLoaded
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<staff_LoadPortrait_Small_AtlasLoaded>(ex);

        public static MethodBase TargetMethod()
        {
            return typeof(staff).GetNestedTypes(AccessTools.all)
                .SelectMany(type => type.GetMethods(AccessTools.all))
                .Single(method => method.Name.Contains("<" + nameof(staff.LoadPortrait_Small) + ">"));
        }

        public static void Postfix(Image ___target, staff._type ___Type, bool ___Pro, staff._staff._unique_type ___UniqueType)
        {
            if (___target != null && ___target.sprite == null && PlayerThumbnail.IsMissingThumbnail(___Type, false, ___Pro, ___UniqueType))
                ___target.sprite = PlayerThumbnail.Get();
        }
    }
}
