using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// The game's portrait loader and queue, and the mod's placeholder and late renders, reset for each test.
    /// </summary>
    public abstract class PortraitTestBase
    {
        private static readonly FieldInfo TexturesInstance = AccessTools.Field(typeof(data_girls_textures), "_this");
        private static readonly FieldInfo QueueField = AccessTools.Field(typeof(data_girls_textures), "Queue");
        private static readonly Type QueueEntry = AccessTools.Inner(typeof(data_girls_textures), "_queue");

        protected readonly data_girls_textures Textures = TestGame.LiveComponent<data_girls_textures>();
        protected readonly Sprite Placeholder = TestGame.LiveComponent<Sprite>();

        protected PortraitTestBase()
        {
            TestGame.Reset();
            TexturesInstance.SetValue(null, Textures);
            QueueField.SetValue(null, Activator.CreateInstance(typeof(List<>).MakeGenericType(QueueEntry)));
            PortraitLoading.placeholder = Placeholder;
            PortraitLoading.cardsLoadedAt = 0f;
            PortraitLoading.lateRenderers.Clear();
            // The mod logs some warnings once per session
            ((HashSet<string>)AccessTools.Field(typeof(PatchLog), "OnceKeys").GetValue(null)).Clear();
        }

        protected static IList Queue => (IList)QueueField.GetValue(null);

        protected static void AddToQueue(data_girls.girls girl, GameObject target = null)
        {
            object entry = Activator.CreateInstance(QueueEntry, true);
            AccessTools.Field(QueueEntry, "Girl").SetValue(entry, girl);
            AccessTools.Field(QueueEntry, "Target_Object").SetValue(entry, target);
            Queue.Add(entry);
        }

        protected static data_girls.girls[] QueuedGirls() =>
            Queue.Cast<object>().Select(e => (data_girls.girls)AccessTools.Field(QueueEntry, "Girl").GetValue(e)).ToArray();

        protected static Sprite RealSprite() => TestGame.LiveComponent<Sprite>();
    }

    /// <summary>
    /// The audition popup waits up to 2 s for its portraits, then unlocks the cards with a clear placeholder
    /// on those still loading.
    /// </summary>
    public class AuditionUnlockTests : PortraitTestBase
    {
        private readonly Popup_Audition popup = TestGame.Component<Popup_Audition>();
        private readonly List<object> children = new();

        public AuditionUnlockTests()
        {
            popup.Cards_Container = TestGame.LiveComponent<GameObject>();
            Seams.Children[popup.Cards_Container] = children;
        }

        private GameObject Card(Sprite sprite)
        {
            Audition_Closed_Card card = TestGame.LiveComponent<Audition_Closed_Card>();
            card.Portrait = Seams.ImageWithSprite(sprite);
            children.Add(card);
            return card.Portrait;
        }

        private bool PortraitsLoaded(bool vanilla)
        {
            bool result = vanilla;
            Popup_Audition_PortraitsLoaded.Postfix(popup, ref result);
            return result;
        }

        private static void LoadCardsAt(float time)
        {
            Seams.Now = time;
            Popup_Audition_LoadCards.Prefix();
        }

        [Fact]
        public void BeforeTwoSeconds_KeepsWaiting()
        {
            LoadCardsAt(10f);
            GameObject blank = Card(null);
            Seams.Now = 11.9f;

            Assert.False(PortraitsLoaded(false));
            Assert.Null(Seams.SpriteOf(blank));
        }

        [Fact]
        public void AfterTwoSeconds_UnlocksWithPlaceholdersOnBlankCards()
        {
            LoadCardsAt(10f);
            Sprite face = RealSprite();
            GameObject loaded = Card(face);
            GameObject blank = Card(null);
            Seams.Now = 12f;

            Assert.True(PortraitsLoaded(false));
            Assert.Same(Placeholder, Seams.SpriteOf(blank));
            Assert.Same(face, Seams.SpriteOf(loaded));
        }

        [Fact]
        public void AllPortraitsLoaded_UnlocksAtOnce()
        {
            LoadCardsAt(10f);
            Sprite face = RealSprite();
            GameObject loaded = Card(face);
            Seams.Now = 10.1f;

            Assert.True(PortraitsLoaded(true));
            Assert.Same(face, Seams.SpriteOf(loaded));
        }

        [Fact]
        public void TheWaitRestartsForEachAudition()
        {
            LoadCardsAt(10f);
            LoadCardsAt(30f);
            Card(null);
            Seams.Now = 31f;

            Assert.False(PortraitsLoaded(false));
        }

        [Fact]
        public void ChildrenThatArentClosedCards_AreSkipped()
        {
            LoadCardsAt(0f);
            children.Add(TestGame.Component<Audition_Closed_Card>()); // destroyed
            Audition_Closed_Card noPortrait = TestGame.LiveComponent<Audition_Closed_Card>();
            children.Add(noPortrait);
            GameObject blank = Card(null);
            Seams.Now = 5f;

            Assert.True(PortraitsLoaded(false));
            Assert.Same(Placeholder, Seams.SpriteOf(blank));
            Assert.Empty(Log.Of(LogType.Warning));
        }
    }

    /// <summary>
    /// A card opened before her portrait arrived starts empty, and loads the portrait itself: its face, its
    /// saved copy for showing her again, and the stats panel if it still shows her.
    /// </summary>
    public class LatePortraitTests : PortraitTestBase
    {
        private static readonly FieldInfo SavedPortrait = AccessTools.Field(typeof(Audition_Golden_Card), "Portrait_");

        private readonly Popup_Audition popup = TestGame.LiveComponent<Popup_Audition>();
        private readonly Audition_Golden_Card card = TestGame.LiveComponent<Audition_Golden_Card>();
        private readonly Audition_Data_Card panel = TestGame.LiveComponent<Audition_Data_Card>();
        private readonly Auditions.data._girl candidate;

        public LatePortraitTests()
        {
            card.Portrait = Seams.ImageWithSprite(Placeholder);
            card.PortraitShadow = Seams.ImageWithSprite(Placeholder);
            SavedPortrait.SetValue(card, Placeholder);
            panel.Portrait = Seams.ImageWithSprite(Placeholder);
            panel.Portrait_Shadow = Seams.ImageWithSprite(Placeholder);
            popup.Card_Container = Seams.Holding(panel);
            candidate = new Auditions.data._girl { girl = TestGame.Idol(), CardObject = Seams.Holding(card) };
            panel.Girl = candidate;
        }

        /// <summary>
        /// Opens the card, and returns the game's loader the mod started for it.
        /// </summary>
        private IEnumerator OpenCard(Sprite portrait)
        {
            Popup_Audition_OpenCard.Postfix(popup, candidate, portrait);
            if (Seams.Coroutines.Count == 0)
                return null;
            Assert.Single(Seams.Coroutines);
            Assert.Same(Textures, Seams.Coroutines[0].host);
            return Traverse.Create(Seams.Coroutines[0].routine).Field("loader").GetValue<IEnumerator>();
        }

        private static void Arrive(IEnumerator loader) => Traverse.Create(loader).Field("callback").GetValue<Action>()();

        [Fact]
        public void OpeningAnEmptyCard_LoadsItsPortraitOntoTheOpenedCard()
        {
            IEnumerator loader = OpenCard(Placeholder);

            Assert.NotNull(loader);
            Assert.Same(candidate.girl, Traverse.Create(loader).Field("girl").GetValue());
            Assert.Equal(new[] { card.Portrait, card.PortraitShadow },
                Traverse.Create(loader).Field("target").GetValue<List<GameObject>>(), new SameObject<GameObject>());
            Assert.Empty(Log.Of(LogType.Warning));
        }

        [Fact]
        public void OpeningACardWithItsPortrait_StartsNothing()
        {
            Assert.Null(OpenCard(RealSprite()));
        }

        [Fact]
        public void ThePortraitArrives_FillsTheSavedCopyAndTheStatsPanel()
        {
            IEnumerator loader = OpenCard(Placeholder);
            Sprite face = RealSprite();
            // The game's loader sets the opened card's face and shadow, then calls back
            Seams.Sprites[Seams.Images[card.Portrait]] = face;
            Seams.Sprites[Seams.Images[card.PortraitShadow]] = face;

            Arrive(loader);

            Assert.Same(face, SavedPortrait.GetValue(card));
            Assert.Same(face, Seams.SpriteOf(panel.Portrait));
            Assert.Same(face, Seams.SpriteOf(panel.Portrait_Shadow));
        }

        [Fact]
        public void ThePanelShowsSomeoneElse_IsLeftAlone()
        {
            IEnumerator loader = OpenCard(Placeholder);
            panel.Girl = new Auditions.data._girl { girl = TestGame.Idol() };
            Sprite face = RealSprite();
            Seams.Sprites[Seams.Images[card.Portrait]] = face;

            Arrive(loader);

            Assert.Same(face, SavedPortrait.GetValue(card));
            Assert.Same(Placeholder, Seams.SpriteOf(panel.Portrait));
        }

        [Fact]
        public void AFailedLoad_ChangesNothing()
        {
            // The game queues a failed load again and calls back without setting the face
            IEnumerator loader = OpenCard(Placeholder);

            Arrive(loader);

            Assert.Same(Placeholder, SavedPortrait.GetValue(card));
            Assert.Same(Placeholder, Seams.SpriteOf(panel.Portrait));
        }

        [Fact]
        public void TheOpenedCardWasDestroyed_ChangesNothing()
        {
            IEnumerator loader = OpenCard(Placeholder);
            Seams.Sprites[Seams.Images[card.Portrait]] = RealSprite();
            TestGame.Kill(card);

            Arrive(loader);

            Assert.Same(Placeholder, SavedPortrait.GetValue(card));
            Assert.Same(Placeholder, Seams.SpriteOf(panel.Portrait));
        }
    }

    /// <summary>
    /// The game's portrait loaders stop once every object they'd set the portrait on is destroyed, even while
    /// they're waiting for the render. Without that they kept running and threw when they reached it.
    /// </summary>
    public class PortraitLoaderTests : PortraitTestBase
    {
        private readonly List<string> steps = new();
        private bool rendered;

        private IEnumerator Loader()
        {
            steps.Add("start");
            yield return new WaitUntil(() => rendered);
            steps.Add("set");
            yield return "next";
            steps.Add("end");
        }

        private IEnumerator Wrapped(params GameObject[] targets)
        {
            IEnumerator loader = Loader();
            data_girls_textures_setPortrait.Postfix(targets.ToList(), ref loader);
            return loader;
        }

        [Fact]
        public void LiveTargets_RunTheWholeLoader()
        {
            IEnumerator loader = Wrapped(TestGame.LiveComponent<GameObject>());

            Assert.True(loader.MoveNext()); // waiting for the render
            Assert.Null(loader.Current);
            rendered = true;
            Assert.True(loader.MoveNext());
            Assert.Equal("next", loader.Current); // other yields reach Unity unchanged
            Assert.False(loader.MoveNext());
            Assert.Equal(new[] { "start", "set", "end" }, steps);
        }

        [Fact]
        public void TargetsDestroyedWhileWaiting_StopTheLoader()
        {
            GameObject portrait = TestGame.LiveComponent<GameObject>();
            GameObject shadow = TestGame.LiveComponent<GameObject>();
            IEnumerator loader = Wrapped(portrait, shadow);
            Assert.True(loader.MoveNext());

            TestGame.Kill(portrait);
            Assert.True(loader.MoveNext()); // the shadow is still there
            TestGame.Kill(shadow);
            rendered = true;

            Assert.False(loader.MoveNext());
            Assert.Equal(new[] { "start" }, steps);
        }

        [Fact]
        public void TargetsDestroyedBeforeItStarts_RunNothing()
        {
            IEnumerator loader = Wrapped(TestGame.Component<GameObject>());

            Assert.False(loader.MoveNext());
            Assert.Empty(steps);
        }

        [Fact]
        public void NoTargets_KeepsTheGamesLoader()
        {
            IEnumerator loader = Loader();
            IEnumerator original = loader;

            data_girls_textures_setPortrait.Postfix(new List<GameObject>(), ref loader);
            Assert.Same(original, loader);
            data_girls_textures_setPortrait.Postfix(null, ref loader);
            Assert.Same(original, loader);
        }

        [Fact]
        public void TheGamesLoader_IsWrapped()
        {
            IEnumerator loader = Textures.setPortrait(TestGame.Idol(), new List<GameObject> { TestGame.LiveComponent<GameObject>() });

            Assert.Contains(nameof(PortraitLoading.WhileTargetsExist), loader.GetType().Name);
        }

        [Fact]
        public void SettingAPortraitOnADestroyedObject_IsSkipped()
        {
            Assert.False(data_girls_textures_SetSprite.Prefix(TestGame.Component<GameObject>()));
            Assert.False(data_girls_textures_SetSprite.Prefix(null));
            Assert.True(data_girls_textures_SetSprite.Prefix(TestGame.LiveComponent<GameObject>()));
        }
    }

    /// <summary>
    /// Closing an audition drops the queued portraits of candidates who weren't hired.
    /// </summary>
    public class AuditionCloseTests : PortraitTestBase
    {
        private readonly Popup_Audition popup = TestGame.Component<Popup_Audition>();
        private readonly data_girls.girls hired = TestGame.Idol(name: "Hired");
        private readonly data_girls.girls notHired = TestGame.Idol(name: "Not hired");
        private readonly data_girls.girls member = TestGame.Idol(name: "Member");

        public AuditionCloseTests()
        {
            TestGame.Hire(member);
            TestGame.Hire(hired);
            Auditions.data data = new();
            data.Girls.Add(new Auditions.data._girl { girl = hired });
            data.Girls.Add(new Auditions.data._girl { girl = notHired });
            AccessTools.Field(typeof(Popup_Audition), "Data").SetValue(popup, data);
        }

        [Fact]
        public void CandidatesWhoWerentHired_LoseTheirQueuedPortraits()
        {
            AddToQueue(hired);
            AddToQueue(notHired);
            AddToQueue(member);
            AddToQueue(notHired, TestGame.LiveComponent<GameObject>()); // a failed load queued again

            Popup_Audition_Close.Prefix(popup);

            Assert.Equal(new[] { hired, member }, QueuedGirls());
        }

        [Fact]
        public void NoAuditionData_ChangesNothing()
        {
            AddToQueue(notHired);
            AccessTools.Field(typeof(Popup_Audition), "Data").SetValue(popup, null);

            Popup_Audition_Close.Prefix(popup);

            Assert.Equal(new[] { notHired }, QueuedGirls());
            Assert.Empty(Log.Of(LogType.Warning));
        }
    }

    /// <summary>
    /// A portrait render that takes over 2 s lets the shared queue move on and finishes in the background:
    /// at most 3 at once, each for up to 60 s.
    /// </summary>
    public class RenderTimeoutTests : PortraitTestBase
    {
        private static readonly MethodBase CachePortrait =
            AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(data_girls_textures), "NEW_Cache_Portrait"));

        private readonly data_girls.girls girl = TestGame.Idol();
        private int updates;

        public RenderTimeoutTests()
        {
            girl.Update = () => updates++;
            girl.TexturesUpdate = () => updates++;
        }

        private GameObject Renderer()
        {
            Portrait_Renderer renderer = TestGame.LiveComponent<Portrait_Renderer>();
            renderer.Girl = girl;
            return Seams.Holding(renderer);
        }

        private static int Calls(IEnumerable<CodeInstruction> codes, string method) =>
            codes.Count(ci => ci.operand is MethodInfo m && m.DeclaringType == typeof(PortraitLoading) && m.Name == method);

        [Fact]
        public void Transpiler_ReplacesTheRenderWaitAndTheDestroy()
        {
            List<CodeInstruction> patched = data_girls_textures_NEW_Cache_Portrait.Transpiler(PatchProcessor.GetOriginalInstructions(CachePortrait)).ToList();

            Assert.Equal(1, Calls(patched, nameof(PortraitLoading.WaitForRender)));
            Assert.Equal(1, Calls(patched, nameof(PortraitLoading.DestroyRenderer)));
            Assert.DoesNotContain(patched, ci => ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo c && c.DeclaringType == typeof(WaitUntil));
            Assert.DoesNotContain(patched, ci => ci.operand is MethodInfo m && m.DeclaringType == typeof(UnityEngine.Object) && m.Name == nameof(UnityEngine.Object.Destroy));
            Assert.Empty(Log.Of(LogType.Warning));
        }

        [Fact]
        public void Transpiler_LeavesUnexpectedCodeAlone()
        {
            List<CodeInstruction> patched = data_girls_textures_NEW_Cache_Portrait.Transpiler(new List<CodeInstruction> { new(OpCodes.Ret) }).ToList();

            Assert.Single(patched);
            Assert.Single(Log.Of(LogType.Warning));
        }

        [Fact]
        public void TheWait_EndsWhenTheRenderFinishes()
        {
            bool done = false;
            WaitUntil wait = PortraitLoading.WaitForRender(() => done);

            Assert.True(wait.keepWaiting);
            done = true;
            Assert.False(wait.keepWaiting);
        }

        [Fact]
        public void TheWait_EndsAfterTwoSeconds()
        {
            Seams.Now = 100f;
            WaitUntil wait = PortraitLoading.WaitForRender(() => false);

            Seams.Now = 101.9f;
            Assert.True(wait.keepWaiting);
            Seams.Now = 102f;
            Assert.False(wait.keepWaiting);
        }

        [Fact]
        public void TheWait_HoldsTheQueueWhileThreeRendersRunLate()
        {
            GameObject[] late = { TestGame.LiveComponent<GameObject>(), TestGame.LiveComponent<GameObject>(), TestGame.LiveComponent<GameObject>() };
            PortraitLoading.lateRenderers.AddRange(late);
            WaitUntil wait = PortraitLoading.WaitForRender(() => false);
            Seams.Now = 10f;

            Assert.True(wait.keepWaiting);
            TestGame.Kill(late[0]); // e.g. a scene change destroyed it
            Assert.False(wait.keepWaiting);
        }

        [Fact]
        public void AFinishedRender_IsDestroyedAsInTheGame()
        {
            girl.texture.cached = true;
            GameObject renderer = Renderer();

            PortraitLoading.DestroyRenderer(renderer);

            Assert.Same(renderer, Assert.Single(Seams.Destroyed));
            Assert.Empty(Seams.Coroutines);
            Assert.Empty(PortraitLoading.lateRenderers);
        }

        [Fact]
        public void AnUnfinishedRender_FinishesInTheBackground()
        {
            GameObject renderer = Renderer();

            PortraitLoading.DestroyRenderer(renderer);

            Assert.Empty(Seams.Destroyed);
            Assert.Same(renderer, Assert.Single(PortraitLoading.lateRenderers));
            (MonoBehaviour host, IEnumerator routine) = Assert.Single(Seams.Coroutines);
            Assert.Same(Textures, host);
            Assert.Single(Log.Of(LogType.Warning));

            Seams.Now = 5f;
            Assert.True(routine.MoveNext());
            girl.texture.cached = true;
            Assert.False(routine.MoveNext());

            Assert.Same(renderer, Assert.Single(Seams.Destroyed));
            Assert.Empty(PortraitLoading.lateRenderers);
            Assert.Equal(2, updates); // what the queue does after a render
        }

        [Fact]
        public void AStuckRender_IsStoppedAfterSixtySeconds()
        {
            GameObject renderer = Renderer();
            PortraitLoading.DestroyRenderer(renderer);
            IEnumerator routine = Seams.Coroutines[0].routine;

            Assert.True(routine.MoveNext());
            Seams.Now = 59.9f;
            Assert.True(routine.MoveNext());
            Seams.Now = 60f;
            Assert.False(routine.MoveNext());

            Assert.Same(renderer, Assert.Single(Seams.Destroyed));
            Assert.Empty(PortraitLoading.lateRenderers);
            Assert.Equal(0, updates);
            Assert.Equal(2, Log.Of(LogType.Warning).Count());
        }
    }
}
