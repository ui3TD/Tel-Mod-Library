using System;
using System.Linq;
using UiCopies;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace NationalTour.Tests
{
    /// <summary>
    /// The shared helper that copies a game UI object without what other mods have added to it (National Tour
    /// and ModMenus both ship it).
    /// </summary>
    public class UiCopyTests : IDisposable
    {
        private static readonly VanillaShape Shape = new(
            new[] { typeof(RectTransform), typeof(Image), typeof(CanvasGroup) },
            new[] { "BG", "Panel", "Panel/Text" });

        private readonly GameObject parent;
        private readonly GameObject source;
        private readonly LogRecorder log;

        public UiCopyTests()
        {
            TestGame.Reset();
            log = new LogRecorder();
            parent = Seams.NewObject("Container");
            source = Seams.NewObject("World Tour", parent);
            Seams.Add<Image>(source);
            Seams.Add<CanvasGroup>(source);
            Seams.Add<Image>(Seams.NewObject("BG", source));
            Seams.Add<Text>(Seams.NewObject("Text", Seams.NewObject("Panel", source)));
        }

        public void Dispose() => log.Dispose();

        private GameObject Copy() => Seams.CopyGameObject(source, Seams.TransformOf(parent), Shape, "[Test]", "panel");

        private static string[] Paths(GameObject root, string path = null) =>
            Seams.ChildrenOf(root).SelectMany(child =>
            {
                string childPath = path == null ? Seams.NameOf(child) : path + "/" + Seams.NameOf(child);
                return new[] { childPath }.Concat(Paths(child, childPath));
            }).ToArray();

        [Fact]
        public void WithOnlyTheGamesParts_TheCopyHasThemAll_AndNothingIsRemoved()
        {

            GameObject copy = Copy();

            Assert.Equal(new[] { "BG", "Panel", "Panel/Text" }, Paths(copy));
            Assert.Equal(new[] { typeof(RectTransform), typeof(Image), typeof(CanvasGroup) },
                Seams.ComponentsOf<Component>(copy).Select(c => c.GetType()));
            Assert.Empty(Seams.Destroyed.Where(d => d.Name != UiCopy.HOLDER_NAME));
            Assert.Empty(log.Messages);
            Assert.Empty(log.Warnings);
        }

        [Fact]
        public void TheCopyGoesUnderTheParent_AndTheHolderIsGone()
        {
            GameObject copy = Copy();

            Assert.Same(parent, Seams.ParentOf(copy));
            Assert.Equal(new[] { "World Tour", "World Tour" }, Seams.ChildrenOf(parent).Select(Seams.NameOf));
            Assert.Contains((UiCopy.HOLDER_NAME, false), Seams.Destroyed);
        }

        [Fact]
        public void AnotherModsObject_IsLeftOut_AndLogged()
        {
            Seams.NewObject("Another mod's line", source);

            GameObject copy = Copy();

            Assert.Equal(new[] { "BG", "Panel", "Panel/Text" }, Paths(copy));
            Assert.Single(log.Messages, m => m.Contains("'Another mod's line'") && m.StartsWith("[Test]"));
        }

        [Fact]
        public void AnotherModsObjectInsideAGameObject_IsLeftOut()
        {
            GameObject panel = Seams.Child(source, "Panel");
            Seams.NewObject("Badge", Seams.Child(panel, "Text"));
            Seams.NewObject("Another mod's button", panel);

            GameObject copy = Copy();

            Assert.Equal(new[] { "BG", "Panel", "Panel/Text" }, Paths(copy));
        }

        [Fact]
        public void AGameObjectWithTheRightNameInTheWrongPlace_IsLeftOut()
        {
            Seams.NewObject("Text", source);

            GameObject copy = Copy();

            Assert.Equal(new[] { "BG", "Panel", "Panel/Text" }, Paths(copy));
        }

        [Fact]
        public void AnotherModsScriptOnTheObject_IsLeftOut_AndTheGamesStay()
        {
            Seams.Add<Outline>(source);

            GameObject copy = Copy();

            Assert.Equal(new[] { typeof(RectTransform), typeof(Image), typeof(CanvasGroup) },
                Seams.ComponentsOf<Component>(copy).Select(c => c.GetType()));
        }

        [Fact]
        public void ScriptsOnTheGamesObjectsInside_Stay()
        {
            Seams.Add<Outline>(Seams.Child(Seams.Child(source, "Panel"), "Text"));

            GameObject copy = Copy();

            Assert.Single(Seams.ComponentsOf<Outline>(Seams.Child(Seams.Child(copy, "Panel"), "Text")));
        }

        [Fact]
        public void PartsAreRemovedWhileTheCopyIsInactive()
        {
            Seams.NewObject("Another mod's line", source);
            Seams.Add<Outline>(source);

            Copy();

            Assert.Contains(("Another mod's line", false), Seams.Destroyed);
            Assert.Contains((nameof(Outline), false), Seams.Destroyed);
            Assert.DoesNotContain(Seams.Destroyed, d => d.ActiveInScene);
        }

        [Fact]
        public void TheSourceKeepsEverything()
        {
            GameObject extra = Seams.NewObject("Another mod's line", source);
            Outline script = Seams.Add<Outline>(source);

            Copy();

            Assert.Equal(new[] { "BG", "Panel", "Panel/Text", "Another mod's line" }, Paths(source));
            Assert.Same(source, Seams.ParentOf(extra));
            Assert.Contains(script, Seams.ComponentsOf<Outline>(source));
            Assert.True(Seams.IsActive(source));
        }

        [Fact]
        public void APartThatCantBeRemoved_IsLoggedAndKept_AndTheRestIsStillRemoved()
        {
            Seams.NewObject("Stuck", source);
            Seams.NewObject("Another mod's line", source);
            Seams.Add<Outline>(source);
            Seams.CantDestroy = "Stuck";

            GameObject copy = Copy();

            Assert.Equal(new[] { "BG", "Panel", "Panel/Text", "Stuck" }, Paths(copy));
            Assert.Empty(Seams.ComponentsOf<Outline>(copy));
            Assert.Single(log.Warnings, w => w.Contains("'Stuck'") && w.Contains("Can't destroy"));
        }
    }
}
