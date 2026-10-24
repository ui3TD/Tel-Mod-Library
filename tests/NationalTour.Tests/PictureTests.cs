using System.IO;
using System.Linq;
using UnityEngine;
using Xunit;
using static NationalTour.Pictures;

namespace NationalTour.Tests
{
    /// <summary>
    /// The mod loads its three pictures when a save starts: from the folder its DLL was loaded from, or from its
    /// entry in the game's mod list.
    /// </summary>
    public class PictureTests
    {
        public PictureTests() => TestGame.Reset();

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
        /// The mod's folder is the one its DLL was loaded from; another installed copy with the same title isn't used then.
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
        /// With no folder to read pictures from, nothing is loaded and the error is logged; the National Tour
        /// tab then isn't added (NationalTabTests).
        /// </summary>
        [Fact]
        public void NoPictures_LoadsNothing()
        {
            new Scene();
            TestGame.AddMod("Some Other Mod", Path.Combine(Path.GetTempPath(), "Other"));
            using LogRecorder log = new();

            Seams.GameStart();

            Assert.Empty(Seams.Loads);
            Assert.True(TOUR_map_2 is null);
            Assert.Contains(log.Errors, e => e.Contains("Textures folder"));
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
    }
}
