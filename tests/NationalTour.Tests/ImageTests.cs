using DG.Tweening;
using System.IO;
using System.Linq;
using UnityEngine;
using Xunit;
using static NationalTour.Utility;

namespace NationalTour.Tests
{
    /// <summary>
    /// The mod loads its own pictures when a save starts and shows them in place of the World Tour ones:
    /// on the tour results popup and on the Special Events popup's tour tab.
    /// </summary>
    public class ImageTests
    {
        public ImageTests() => TestGame.Reset();

        [Fact]
        public void GameStart_LoadsThePicturesFromThisModsFolder()
        {
            new Scene();
            TestGame.AddMod("Some Other Mod", Path.Combine(Path.GetTempPath(), "Other"));
            Mods._mod mod = TestGame.AddThisMod();

            Seams.GameStart();

            string Picture(string file) => Path.GetFullPath(Path.Combine(mod.Path, "Textures", "Patch", file));
            Assert.Equal(new[]
            {
                (nameof(IMG2Sprite.LoadNewSprite), Picture("World_tour_def.jpg"), 100f),
                (nameof(IMG2Sprite.LoadNewSprite), Picture("World_tour_def_BG.jpg"), 100f),
                (nameof(IMG2Sprite.LoadNewSprite), Picture("TOUR_map_2.jpg"), 100f),
            }, Seams.Loads.Select(l => (l.Method, l.Path, l.PixelsPerUnit)));

            Assert.Same(Seams.Loads[0].Result, World_tour_def);
            // The results background is the same picture: the sprite's texture, not a second copy
            Assert.Same(Seams.SpriteTextures[World_tour_def], World_tour_def_tex);
            Assert.Same(Seams.Loads[1].Result, World_tour_def_BG);
            Assert.Same(Seams.Loads[2].Result, TOUR_map_2);
        }

        /// <summary>
        /// The pictures stay loaded from one save to the next, so loading another save doesn't read them again.
        /// </summary>
        [Fact]
        public void SecondGameStart_KeepsThePictures()
        {
            new Scene();
            TestGame.AddThisMod();
            Seams.GameStart();
            Sprite map = TOUR_map_2;
            Seams.Loads.Clear();

            Seams.GameStart();

            Assert.Empty(Seams.Loads);
            Assert.Same(map, TOUR_map_2);
        }

        /// <summary>
        /// The mod's folder is the one its DLL was loaded from; another installed copy with the same
        /// title isn't used then.
        /// </summary>
        [Fact]
        public void ModFolder_IsTheDllsFolder_WhenItHasThePictures()
        {
            string dllFolder = Path.Combine(Path.GetTempPath(), "NationalTour.Tests.Dll");
            Directory.CreateDirectory(Path.Combine(dllFolder, "Textures", "Patch"));
            TestGame.AddThisMod();

            Assert.Equal(dllFolder, GetModDirectory(dllFolder));
        }

        /// <summary>
        /// Without pictures next to the DLL (the unit tests' case), the mod list's copy is used, by title.
        /// </summary>
        [Fact]
        public void ModFolder_FallsBackToTheModList()
        {
            Mods._mod mod = TestGame.AddThisMod();

            Assert.Equal(mod.GetPath(), GetModDirectory(Path.GetTempPath()));
        }

        /// <summary>
        /// With no folder to read pictures from, the game keeps its own (nothing is loaded, nothing throws).
        /// </summary>
        [Fact]
        public void NoPictures_LoadsNothing()
        {
            new Scene();
            TestGame.AddMod("Some Other Mod", Path.Combine(Path.GetTempPath(), "Other"));
            UnityEngine.ILogHandler gameLog = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new Silent();
            try
            {
                Seams.GameStart();
            }
            finally
            {
                Debug.unityLogger.logHandler = gameLog;
            }

            Assert.Empty(Seams.Loads);
            Assert.True(TOUR_map_2 is null);
        }

        private sealed class Silent : UnityEngine.ILogHandler
        {
            public void LogFormat(LogType logType, Object context, string format, params object[] args) { }
            public void LogException(System.Exception exception, Object context) { }
        }

        [Fact]
        public void GameStart_PicturesItLoadsAreShipped()
        {
            new Scene();
            TestGame.AddThisMod();

            Seams.GameStart();

            Assert.All(Seams.Loads, l => Assert.True(File.Exists(l.Path), $"Missing {l.Path}"));
        }

        [Fact]
        public void MainMenu_LoadsNothing()
        {
            new Scene(inGame: false);
            TestGame.AddThisMod();

            Seams.GameStart();

            Assert.Empty(Seams.Loads);
            Assert.True(World_tour_def is null);
        }

        [Fact]
        public void TourResults_FadeInTheNationalTourPicture()
        {
            TestGame.LoadImages();
            Scene scene = new();

            Seams.TourResultsReset();

            Assert.Same(World_tour_def_tex, scene.BGRaw.texture);
            Assert.Equal(0f, Seams.Alphas[scene.BGFade]);
            Assert.Equal(new Vector2(1024, 0), Seams.Sizes[scene.BGRect]);
            Assert.Collection(Seams.Tweens,
                fade =>
                {
                    Assert.Equal(nameof(DOTweenModuleUI.DOFade), fade.Method);
                    Assert.Same(scene.BGFade, fade.Target);
                    Assert.Equal(1f, fade.EndValue);
                    Assert.Equal(0.5f, fade.Duration);
                    Assert.Equal(Ease.OutQuint, fade.Ease);
                },
                zoom =>
                {
                    Assert.Equal(nameof(DOTweenModuleUI.DOSizeDelta), zoom.Method);
                    Assert.Same(scene.BGRect, zoom.Target);
                    Assert.Equal(new Vector2(1074, 20), zoom.EndValue);
                    Assert.Equal(4.5f, zoom.Duration);
                    Assert.Equal(Ease.OutQuint, zoom.Ease);
                });
        }

        [Fact]
        public void TourResults_ShowTheBlurredPictureBehindTheResults()
        {
            TestGame.LoadImages();
            Scene scene = new();

            Seams.TourResultsReset();

            Assert.Same(World_tour_def_BG, scene.ResultsBG.sprite);
            Assert.Equal(Color.white, scene.ResultsBG.color);
        }

        [Fact]
        public void TourTab_ShowsTheNationalTourPicture()
        {
            TestGame.LoadImages();
            Scene scene = new();

            Seams.OpenWorldTourTab();

            Assert.Same(World_tour_def, scene.SpecialEventsTourBG.sprite);
        }

        [Fact]
        public void TourTab_OpenedAgain_ReusesTheImageItFound()
        {
            TestGame.LoadImages();
            Scene scene = new();
            Seams.OpenWorldTourTab();
            int finds = Seams.Finds;

            Seams.OpenWorldTourTab();

            Assert.Equal(finds, Seams.Finds);
            Assert.Same(scene.SpecialEventsTourBG, tourPopupBGImage);
        }

        [Fact]
        public void SpecialEvents_OpenedOnTheTourTab_ShowsTheNationalTourPicture()
        {
            TestGame.LoadImages();
            Scene scene = new();
            scene.SpecialEvents.OpenTab = SpecialEvents_Manager._type.WorldTour;

            Seams.OpenSpecialEvents(scene.SpecialEvents);

            Assert.Same(World_tour_def, scene.SpecialEventsTourBG.sprite);
        }

        [Theory]
        [InlineData(SpecialEvents_Manager._type.Concert)]
        [InlineData(SpecialEvents_Manager._type.SSK)]
        public void SpecialEvents_OpenedOnAnotherTab_LeavesTheTourTabAlone(SpecialEvents_Manager._type tab)
        {
            TestGame.LoadImages();
            Scene scene = new();
            scene.SpecialEvents.OpenTab = tab;

            Seams.OpenSpecialEvents(scene.SpecialEvents);

            Assert.True(scene.SpecialEventsTourBG.sprite is null);
            Assert.Equal(0, Seams.Finds);
        }

        [Fact]
        public void SpecialEvents_ThenTheTourTab_FindTheSameImage()
        {
            TestGame.LoadImages();
            Scene scene = new();
            scene.SpecialEvents.OpenTab = SpecialEvents_Manager._type.WorldTour;
            Seams.OpenSpecialEvents(scene.SpecialEvents);
            int finds = Seams.Finds;

            Seams.OpenWorldTourTab();

            Assert.Equal(finds, Seams.Finds);
            Assert.Same(World_tour_def, scene.SpecialEventsTourBG.sprite);
        }
    }
}
