using HarmonyLib;
using System;

namespace UnofficialPatch
{
    // Base game bugfix:
    // SaveManager.LoadData(...) assigns SaveManager.Data = null when the target file is missing/corrupt,
    // then returns early ("huh"). Autosave later calls SaveEvent, and staticVars.SaveFunction crashes
    // because Camera.main.GetComponent<mainScript>().GetSavedData() is null.
    //
    // Fix:
    // During play, a failed load leaves the game untouched, so the previous SaveManager.Data is restored
    // and autosave keeps saving the game being played.
    // From the main menu, the game scene opens before the file is read, so a failed load leaves an empty game.
    // Data stays null and that scene's autosaves are skipped, so the empty game can't overwrite a save.
    public static class FailedLoadGuard
    {
        private const string EmptyGameError =
            "The save could not be loaded, so this game is empty. Autosave is off for this session so it can't overwrite your saves. Return to the main menu and load another save.";

        // Set while the main menu loads a save into the game scene it has just opened.
        internal static bool MenuLoadPending;

        // The save manager of a game scene whose save failed to load from the main menu.
        // Cleared by the next successful load, or when the main menu starts another game.
        internal static SaveManager BlockedSaveManager;

        // What the LoadData prefixes record for their postfixes.
        public struct LoadState
        {
            public SaveManager.SavedData Previous;
            public bool FromMenu;
        }

        internal static LoadState BeforeLoad(SaveManager manager)
        {
            LoadState state = new LoadState
            {
                Previous = manager != null ? manager.Data : null,
                FromMenu = MenuLoadPending,
            };
            MenuLoadPending = false;
            return state;
        }

        internal static void AfterLoad<TPatch>(SaveManager manager, LoadState state, string restoredWarning)
        {
            if (manager == null)
                return;

            if (manager.Data != null)
            {
                // The load worked, so this scene holds a real game again.
                if (ReferenceEquals(manager, BlockedSaveManager))
                    BlockedSaveManager = null;
                return;
            }

            if (!state.FromMenu && state.Previous != null)
            {
                manager.Data = state.Previous;
                PatchLog.WarnOncePerPatch<TPatch>(restoredWarning);
                return;
            }

            BlockedSaveManager = manager;
            PatchLog.Error<TPatch>(EmptyGameError);
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(bool) })]
    public class SaveManager_LoadData_Bool_NullGuard
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_LoadData_Bool_NullGuard>(ex);

        private const string WarningMessage =
            "LoadData(bool) left SaveManager.Data null. Restored previous in-memory save data to prevent SaveEvent/autosave null crash.";

        public static void Prefix(SaveManager __instance, ref FailedLoadGuard.LoadState __state)
        {
            __state = FailedLoadGuard.BeforeLoad(__instance);
        }

        public static void Postfix(SaveManager __instance, FailedLoadGuard.LoadState __state)
        {
            FailedLoadGuard.AfterLoad<SaveManager_LoadData_Bool_NullGuard>(__instance, __state, WarningMessage);
        }
    }

    // Same null-guard for the string-path overload used by some load flows and mods.
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(string) })]
    public class SaveManager_LoadData_Path_NullGuard
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_LoadData_Path_NullGuard>(ex);

        private const string WarningMessage =
            "LoadData(string) left SaveManager.Data null. Restored previous in-memory save data to prevent SaveEvent/autosave null crash.";

        public static void Prefix(SaveManager __instance, ref FailedLoadGuard.LoadState __state)
        {
            __state = FailedLoadGuard.BeforeLoad(__instance);
        }

        public static void Postfix(SaveManager __instance, FailedLoadGuard.LoadState __state)
        {
            FailedLoadGuard.AfterLoad<SaveManager_LoadData_Path_NullGuard>(__instance, __state, WarningMessage);
        }
    }

    // Fixes saving and loading pushing back the bankruptcy deadline: BankruptcyDanger isn't saved, so loading a save
    // in the red clears it, and the next day the game sets it again with a fresh one-month deadline. Restore the flag
    // after a load, keeping the loaded deadline (OnMoneyChange sets it whenever money is below zero).
    // Idempotent: it sets the flag from the same condition however often it runs.
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(bool) })]
    public class SaveManager_LoadData_Bool_Bankruptcy
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_LoadData_Bool_Bankruptcy>(ex);

        public static void Postfix() => Bankruptcy_Restore.AfterLoad();
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(string) })]
    public class SaveManager_LoadData_Path_Bankruptcy
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_LoadData_Path_Bankruptcy>(ex);

        public static void Postfix() => Bankruptcy_Restore.AfterLoad();
    }

    public static class Bankruptcy_Restore
    {
        public static void AfterLoad()
        {
            if (resources.Money() < 0)
                loans.BankruptcyDanger = true;
        }
    }

    // Marks loads the main menu makes into the game scene it has just opened. Starting another game
    // from the main menu also ends an earlier scene's autosave block.
    [HarmonyPatch(typeof(MainMenu_LoadGameManager), nameof(MainMenu_LoadGameManager.LoadAsync))]
    public class MainMenu_LoadGameManager_LoadAsync
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<MainMenu_LoadGameManager_LoadAsync>(ex);

        public static void Prefix(bool loadSave)
        {
            FailedLoadGuard.MenuLoadPending = loadSave;
            FailedLoadGuard.BlockedSaveManager = null;
        }
    }

    // Skips autosaves in a game scene whose save failed to load from the main menu.
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveData), new Type[] { typeof(bool), typeof(bool) })]
    public class SaveManager_SaveData
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<SaveManager_SaveData>(ex);

        public static bool Prefix(SaveManager __instance, bool autoSave)
        {
            return !autoSave || !ReferenceEquals(__instance, FailedLoadGuard.BlockedSaveManager);
        }
    }
}
