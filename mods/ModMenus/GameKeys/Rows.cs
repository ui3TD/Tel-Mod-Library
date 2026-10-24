using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModMenus.GameKeys
{
    /// <summary>
    /// How a game method reads a key: which UnityEngine.Input method it calls.
    /// </summary>
    public enum CallKind
    {
        /// <summary>Input.GetKeyDown(KeyCode)</summary>
        KeyDown,
        /// <summary>Input.GetKey(KeyCode)</summary>
        Key,
        /// <summary>Input.GetKeyUp(KeyCode)</summary>
        KeyUp,
        /// <summary>Input.GetKeyUp(string), with the key's name ("f5")</summary>
        KeyUpByName,
        /// <summary>Input.GetMouseButtonDown(int), with a mouse button (Mouse1 is button 1)</summary>
        MouseButtonDown,
    }

    /// <summary>
    /// One place a game method reads a key: the method, how it reads it, the key, and which read of that key
    /// in the method it is, counted from 0.
    /// </summary>
    public sealed class Site
    {
        public readonly System.Type Type;
        public readonly string Method;
        public readonly CallKind Kind;
        public readonly KeyCode Key;
        public readonly int Occurrence;

        /// <summary>The key row whose key the site reads instead of its own.</summary>
        public Row Row { get; internal set; }

        public Site(System.Type type, string method, CallKind kind, KeyCode key, int occurrence)
        {
            Type = type;
            Method = method;
            Kind = kind;
            Key = key;
            Occurrence = occurrence;
        }

        public override string ToString() => Type.Name + "." + Method + " " + Kind + "(" + Key + ") #" + Occurrence;
    }

    /// <summary>
    /// A key row for one of the game's keys: its varID, the game's key, where the key works (see Contexts), and
    /// the places in the game that read it.
    /// </summary>
    public sealed class Row
    {
        public readonly string VarID;
        public readonly KeyCode Default;
        public readonly string[] Contexts;
        public readonly Site[] Sites;

        public Row(string varID, KeyCode def, string[] contexts, params Site[] sites)
        {
            VarID = varID;
            Default = def;
            Contexts = contexts;
            Sites = sites;
            foreach (Site site in sites)
            {
                site.Row = this;
            }
        }
    }

    /// <summary>
    /// Every game key row, in menu order. ModMenus' own modmenu.json has the same rows, with these defaults.
    /// </summary>
    public static class Rows
    {
        public const string PREFIX = "ModMenus_Key_";

        /// <summary>The variable a row's enabledIf names: on while the game still reads its key through the row.</summary>
        public const string HAS_PREFIX = "ModMenus_HasKey_";

        public static readonly Row[] All =
        {
            new Row("ModMenus_Key_Pause", KeyCode.Space, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.Space, 0)),
            new Row("ModMenus_Key_SpeedPaused", KeyCode.Alpha1, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.Alpha1, 0)),
            new Row("ModMenus_Key_SpeedNormal", KeyCode.Alpha2, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.Alpha2, 0)),
            new Row("ModMenus_Key_SpeedFast", KeyCode.Alpha3, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.Alpha3, 0)),
            new Row("ModMenus_Key_NextTab", KeyCode.Tab, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.Tab, 0)),
            new Row("ModMenus_Key_NextTab2", KeyCode.PageDown, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.PageDown, 0)),
            new Row("ModMenus_Key_NextTabMouse", KeyCode.Mouse2, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.MouseButtonDown, KeyCode.Mouse2, 0)),
            new Row("ModMenus_Key_PrevTab", KeyCode.PageUp, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.PageUp, 0)),
            new Row("ModMenus_Key_FirstTab", KeyCode.Home, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.Home, 0)),
            new Row("ModMenus_Key_LastTab", KeyCode.End, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.End, 0)),
            new Row("ModMenus_Key_CloseTab", KeyCode.Escape, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.Escape, 0)),
            new Row("ModMenus_Key_CloseTabMouse", KeyCode.Mouse1, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.MouseButtonDown, KeyCode.Mouse1, 0)),
            new Row("ModMenus_Key_ScrollUp", KeyCode.UpArrow, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.Key, KeyCode.UpArrow, 0)),
            new Row("ModMenus_Key_ScrollDown", KeyCode.DownArrow, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.Key, KeyCode.DownArrow, 0)),
            new Row("ModMenus_Key_ZoomOut", KeyCode.Z, new[] { "normal", "debug" }, new Site(typeof(Scenes), nameof(Scenes.Update), CallKind.KeyUp, KeyCode.Z, 0)),
            new Row("ModMenus_Key_Mute", KeyCode.M, new[] { "normal", "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.M, 0)),
            new Row("ModMenus_Key_Promotion", KeyCode.Z, new[] { "normal" }, new Site(typeof(Widget_Details), nameof(Widget_Details.Update), CallKind.KeyUp, KeyCode.Z, 0)),
            new Row("ModMenus_Key_Performance", KeyCode.X, new[] { "normal" }, new Site(typeof(Widget_Details), nameof(Widget_Details.Update), CallKind.KeyUp, KeyCode.X, 0)),
            new Row("ModMenus_Key_Spa", KeyCode.C, new[] { "normal" }, new Site(typeof(Widget_Details), nameof(Widget_Details.Update), CallKind.KeyUp, KeyCode.C, 0)),
            new Row("ModMenus_Key_QuickSave", KeyCode.F5, new[] { "normal", "debug", "dialogue", "profile", "credits" }, new Site(typeof(SaveManager), nameof(SaveManager.Update), CallKind.KeyUpByName, KeyCode.F5, 0)),
            new Row("ModMenus_Key_QuickLoad", KeyCode.F9, new[] { "normal", "debug", "dialogue", "profile", "credits" }, new Site(typeof(SaveManager), nameof(SaveManager.Update), CallKind.KeyUpByName, KeyCode.F9, 0)),
            new Row("ModMenus_Key_DialogueUp", KeyCode.UpArrow, new[] { "dialogue" }, new Site(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update), CallKind.KeyDown, KeyCode.UpArrow, 0)),
            new Row("ModMenus_Key_DialogueDown", KeyCode.DownArrow, new[] { "dialogue" }, new Site(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update), CallKind.KeyDown, KeyCode.DownArrow, 0)),
            new Row("ModMenus_Key_DialogueConfirm", KeyCode.Return, new[] { "dialogue" }, new Site(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update), CallKind.KeyDown, KeyCode.Return, 0), new Site(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update), CallKind.KeyDown, KeyCode.Return, 1)),
            new Row("ModMenus_Key_DialogueConfirm2", KeyCode.Space, new[] { "dialogue" }, new Site(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update), CallKind.KeyDown, KeyCode.Space, 0), new Site(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update), CallKind.KeyDown, KeyCode.Space, 1)),
            new Row("ModMenus_Key_DialogueNext", KeyCode.KeypadEnter, new[] { "dialogue" }, new Site(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update), CallKind.KeyDown, KeyCode.KeypadEnter, 0)),
            new Row("ModMenus_Key_CloseProfile", KeyCode.Escape, new[] { "profile" }, new Site(typeof(Profile_Popup), nameof(Profile_Popup.Update), CallKind.KeyDown, KeyCode.Escape, 0)),
            new Row("ModMenus_Key_SkipCredits", KeyCode.Escape, new[] { "credits" }, new Site(typeof(Video_Manager), nameof(Video_Manager.Update), CallKind.KeyDown, KeyCode.Escape, 0)),
            new Row("ModMenus_Key_DebugPanel", KeyCode.F7, new[] { "normal", "debug", "dialogue", "profile", "credits" }, new Site(typeof(DEBUG), nameof(DEBUG.Update), CallKind.KeyDown, KeyCode.F7, 0)),
            new Row("ModMenus_Key_SkipDialogue", KeyCode.Escape, new[] { "dialogue" }, new Site(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update), CallKind.KeyDown, KeyCode.Escape, 0)),
            new Row("ModMenus_Key_DialogueLauncher", KeyCode.BackQuote, new[] { "debug" }, new Site(typeof(DEBUG), nameof(DEBUG.Update), CallKind.KeyDown, KeyCode.BackQuote, 0)),
            new Row("ModMenus_Key_WordCount", KeyCode.F4, new[] { "debug" }, new Site(typeof(DEBUG), nameof(DEBUG.Update), CallKind.KeyDown, KeyCode.F4, 0)),
            new Row("ModMenus_Key_CheatLocalAudition", KeyCode.Z, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyUp, KeyCode.Z, 0)),
            new Row("ModMenus_Key_CheatRegionalAudition", KeyCode.X, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyUp, KeyCode.X, 0)),
            new Row("ModMenus_Key_CheatNationwideAudition", KeyCode.C, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyUp, KeyCode.C, 0)),
            new Row("ModMenus_Key_CheatSlower", KeyCode.Q, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.Q, 0)),
            new Row("ModMenus_Key_CheatDefaultSpeed", KeyCode.W, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.W, 0)),
            new Row("ModMenus_Key_CheatFaster", KeyCode.E, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.E, 0)),
            new Row("ModMenus_Key_CheatRelationship", KeyCode.B, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyUp, KeyCode.B, 0)),
            new Row("ModMenus_Key_CheatFans", KeyCode.F, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.F, 0)),
            new Row("ModMenus_Key_CheatFame", KeyCode.S, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.S, 0)),
            new Row("ModMenus_Key_CheatMoney", KeyCode.Y, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.Y, 0)),
            new Row("ModMenus_Key_CheatScandal", KeyCode.F4, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.F4, 0)),
            new Row("ModMenus_Key_CheatExperience", KeyCode.U, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.U, 0)),
            new Row("ModMenus_Key_CheatResearch", KeyCode.P, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.P, 0)),
            new Row("ModMenus_Key_CheatEventCooldowns", KeyCode.F2, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.F2, 0)),
            new Row("ModMenus_Key_CheatStamina", KeyCode.F3, new[] { "debug" }, new Site(typeof(Controls), nameof(Controls.Update), CallKind.KeyDown, KeyCode.F3, 0)),
        };

        public static readonly Dictionary<string, Row> ByVarID = All.ToDictionary(row => row.VarID);

        public static IEnumerable<Site> Sites => All.SelectMany(row => row.Sites);
    }
}
