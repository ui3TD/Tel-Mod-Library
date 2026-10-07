using System;
using System.Linq;
using UnityEngine;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A load that finds no readable file leaves SaveManager.Data null, and the game's next save throws.
    /// During play the previous save data is put back. From the main menu, which opens an empty game scene
    /// before reading the file, that scene's autosaves are skipped instead, so they can't overwrite a save.
    /// </summary>
    public class FailedLoadTests : IDisposable
    {
        public FailedLoadTests()
        {
            TestGame.Reset();
            FailedLoadGuard.MenuLoadPending = false;
            FailedLoadGuard.BlockedSaveManager = null;
        }

        public void Dispose()
        {
            FailedLoadGuard.MenuLoadPending = false;
            FailedLoadGuard.BlockedSaveManager = null;
        }

        /// <summary>
        /// Both LoadData overloads, run around a load that ends with this Data (null when the file was unreadable).
        /// </summary>
        public static TheoryData<string> Overloads => new() { "bool", "string" };

        private static void Load(string overload, SaveManager manager, SaveManager.SavedData loaded)
        {
            FailedLoadGuard.LoadState state = default;
            if (overload == "bool")
            {
                SaveManager_LoadData_Bool_NullGuard.Prefix(manager, ref state);
                manager.Data = loaded;
                SaveManager_LoadData_Bool_NullGuard.Postfix(manager, state);
            }
            else
            {
                SaveManager_LoadData_Path_NullGuard.Prefix(manager, ref state);
                manager.Data = loaded;
                SaveManager_LoadData_Path_NullGuard.Postfix(manager, state);
            }
        }

        private static SaveManager Scene()
        {
            SaveManager manager = TestGame.LiveComponent<SaveManager>();
            manager.Data = new SaveManager.SavedData();
            return manager;
        }

        private static void FromMenu(bool loadSave = true) => MainMenu_LoadGameManager_LoadAsync.Prefix(loadSave);

        private static bool Saves(SaveManager manager, bool autoSave) => SaveManager_SaveData.Prefix(manager, autoSave);

        [Theory]
        [MemberData(nameof(Overloads))]
        public void DuringPlay_PreviousDataIsRestored(string overload)
        {
            SaveManager scene = Scene();
            SaveManager.SavedData playing = scene.Data;

            Load(overload, scene, null);

            Assert.Same(playing, scene.Data);
            Assert.True(Saves(scene, autoSave: true));
            Assert.Empty(Log.Of(LogType.Error));
        }

        [Theory]
        [MemberData(nameof(Overloads))]
        public void FromTheMenu_DataStaysEmptyAndAutosaveIsSkipped(string overload)
        {
            SaveManager scene = Scene();
            FromMenu();

            Load(overload, scene, null);

            Assert.Null(scene.Data);
            Assert.False(Saves(scene, autoSave: true));
            Assert.Contains(Log.Of(LogType.Error), message => message.Contains("Autosave is off"));
        }

        [Fact]
        public void FromTheMenu_ManualSavesStillRunTheGame()
        {
            // The game's own save then fails on the empty Data, as without the mod, so nothing is written
            SaveManager scene = Scene();
            FromMenu();
            Load("string", scene, null);

            Assert.True(Saves(scene, autoSave: false));
        }

        [Fact]
        public void FromTheMenu_OtherScenesStillAutosave()
        {
            SaveManager broken = Scene();
            FromMenu();
            Load("bool", broken, null);

            Assert.True(Saves(Scene(), autoSave: true));
        }

        [Fact]
        public void Block_EndsWithASuccessfulLoadInTheSameScene()
        {
            SaveManager scene = Scene();
            FromMenu();
            Load("bool", scene, null);

            Load("string", scene, new SaveManager.SavedData());

            Assert.True(Saves(scene, autoSave: true));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Block_EndsWhenTheMenuStartsAnotherGame(bool loadSave)
        {
            SaveManager scene = Scene();
            FromMenu();
            Load("bool", scene, null);

            FromMenu(loadSave);

            Assert.True(Saves(scene, autoSave: true));
        }

        [Fact]
        public void SuccessfulMenuLoad_ChangesNothing()
        {
            SaveManager scene = Scene();
            FromMenu();
            SaveManager.SavedData loaded = new();

            Load("bool", scene, loaded);

            Assert.Same(loaded, scene.Data);
            Assert.True(Saves(scene, autoSave: true));
            Assert.Empty(Log.Messages);
        }

        [Fact]
        public void MenuMark_IsUsedUpByTheMenusLoad()
        {
            // A quickload that fails later in the same game restores the data as usual
            SaveManager scene = Scene();
            FromMenu();
            Load("bool", scene, new SaveManager.SavedData());
            SaveManager.SavedData playing = scene.Data;

            Load("bool", scene, null);

            Assert.Same(playing, scene.Data);
            Assert.True(Saves(scene, autoSave: true));
        }

        [Fact]
        public void NewGame_DoesNotMarkLaterLoads()
        {
            FromMenu(loadSave: false);
            SaveManager scene = Scene();
            SaveManager.SavedData playing = scene.Data;

            Load("string", scene, null);

            Assert.Same(playing, scene.Data);
            Assert.True(Saves(scene, autoSave: true));
        }

        [Fact]
        public void NoPreviousData_IsTreatedLikeTheMenu()
        {
            // Never invent save data: with nothing to restore, block autosave instead
            SaveManager scene = TestGame.LiveComponent<SaveManager>();

            Load("bool", scene, null);

            Assert.Null(scene.Data);
            Assert.False(Saves(scene, autoSave: true));
        }
    }
}
