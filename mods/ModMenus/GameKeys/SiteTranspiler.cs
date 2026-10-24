using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ModMenus.GameKeys
{
    /// <summary>
    /// Makes a game method read each managed key through its key row (see Sites). Only the read changes:
    /// "Input.GetKeyDown(KeyCode.Space)" becomes "Sites.GetKeyDown(KeyCode.Space, varID)", so whatever the
    /// method (or another mod) checks around it stays. A read another mod has already changed doesn't match,
    /// and is left as it is.
    /// </summary>
    public static class SiteTranspiler
    {
        /// <summary>The sites found and changed, so their rows are shown.</summary>
        private static readonly HashSet<Site> Changed = new();

        private static readonly Dictionary<MethodInfo, CallKind> InputReads = new()
        {
            [AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) })] = CallKind.KeyDown,
            [AccessTools.Method(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) })] = CallKind.Key,
            [AccessTools.Method(typeof(Input), nameof(Input.GetKeyUp), new[] { typeof(KeyCode) })] = CallKind.KeyUp,
            [AccessTools.Method(typeof(Input), nameof(Input.GetKeyUp), new[] { typeof(string) })] = CallKind.KeyUpByName,
            [AccessTools.Method(typeof(Input), nameof(Input.GetMouseButtonDown), new[] { typeof(int) })] = CallKind.MouseButtonDown,
        };

        private static readonly Dictionary<CallKind, MethodInfo> Replacements = new()
        {
            [CallKind.KeyDown] = AccessTools.Method(typeof(Sites), nameof(Sites.GetKeyDown)),
            [CallKind.Key] = AccessTools.Method(typeof(Sites), nameof(Sites.GetKey)),
            [CallKind.KeyUp] = AccessTools.Method(typeof(Sites), nameof(Sites.GetKeyUp)),
            [CallKind.KeyUpByName] = AccessTools.Method(typeof(Sites), nameof(Sites.GetKeyUpByName)),
            [CallKind.MouseButtonDown] = AccessTools.Method(typeof(Sites), nameof(Sites.GetMouseButtonDown)),
        };

        /// <summary>
        /// Whether the game still reads one of a row's keys through it.
        /// </summary>
        public static bool IsAvailable(Row row)
        {
            return row.Sites.Any(Changed.Contains);
        }

        /// <summary>
        /// Changes each read of a managed key in the method's code. Each one is a constant key (or name, or mouse
        /// button) loaded just before the Input call; reads of the same key are counted in order. The method is
        /// changed only if it reads exactly the managed keys expected, as many times each: if the game or another
        /// mod has changed one, a count could point at the wrong read, so the whole method is left as it is, with
        /// a warning, and its rows are hidden. An error leaves the method as it is too: a transpiler that fails
        /// would stop Harmony changing the method at all, other mods' patches included.
        /// </summary>
        public static IEnumerable<CodeInstruction> Apply(Type type, string method, IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            List<CodeInstruction> original = code.Select(instruction => new CodeInstruction(instruction)).ToList();
            try
            {
                return Change(type, method, code);
            }
            catch (Exception e)
            {
                Changed.RemoveWhere(site => site.Type == type && site.Method == method);
                Debug.LogError("[ModMenus] Couldn't change the keys " + type.Name + "." + method + " reads, so they are left as they are: " + e);
                return original;
            }
        }

        private static List<CodeInstruction> Change(Type type, string method, List<CodeInstruction> code)
        {
            List<Site> expected = Rows.Sites.Where(site => site.Type == type && site.Method == method).ToList();
            HashSet<string> managed = new(expected.Select(site => ReadName(site.Kind, site.Key)));
            Dictionary<string, int> seen = new();
            List<KeyValuePair<int, Site>> matches = new();

            for (int i = 1; i < code.Count; i++)
            {
                if (!(code[i].operand is MethodInfo called) || !InputReads.TryGetValue(called, out CallKind kind)
                    || (code[i].opcode != OpCodes.Call && code[i].opcode != OpCodes.Callvirt)
                    || !TryReadKey(code[i - 1], kind, out KeyCode key))
                {
                    continue;
                }
                string read = ReadName(kind, key);
                if (!managed.Contains(read))
                {
                    continue;
                }
                seen.TryGetValue(read, out int occurrence);
                seen[read] = occurrence + 1;
                matches.Add(new KeyValuePair<int, Site>(i, expected.FirstOrDefault(s => s.Kind == kind && s.Key == key && s.Occurrence == occurrence)));
            }

            if (matches.Count != expected.Count || matches.Any(match => match.Value == null))
            {
                Debug.LogWarning("[ModMenus] " + type.Name + "." + method + " doesn't read the keys it used to (the game or another mod changed it), so its keys are left as they are: "
                    + string.Join(", ", expected.Select(site => site.ToString()).ToArray()));
                return code;
            }

            // Last first, so the indexes still point at the calls. The key stays on the stack for the game's own
            // read; the row's varID goes after it. Labels and blocks stay on the call, where any jump lands.
            foreach (KeyValuePair<int, Site> match in matches.OrderByDescending(match => match.Key))
            {
                code.Insert(match.Key, new CodeInstruction(OpCodes.Ldstr, match.Value.Row.VarID));
                code[match.Key + 1].opcode = OpCodes.Call;
                code[match.Key + 1].operand = Replacements[match.Value.Kind];
                Changed.Add(match.Value);
            }
            return code;
        }

        private static string ReadName(CallKind kind, KeyCode key) => kind + ":" + key;

        /// <summary>
        /// The key a constant before an Input call names: a KeyCode, a key's name, or a mouse button.
        /// </summary>
        public static bool TryReadKey(CodeInstruction load, CallKind kind, out KeyCode key)
        {
            key = KeyCode.None;
            if (kind == CallKind.KeyUpByName)
            {
                if (load.opcode != OpCodes.Ldstr || !(load.operand is string name))
                {
                    return false;
                }
                // Unity's key names are KeyCode names in lower case for these keys ("f5")
                return Enum.TryParse(name, true, out key) && Enum.IsDefined(typeof(KeyCode), key);
            }
            if (!TryReadInt(load, out int value))
            {
                return false;
            }
            key = kind == CallKind.MouseButtonDown ? KeyCode.Mouse0 + value : (KeyCode)value;
            return true;
        }

        private static bool TryReadInt(CodeInstruction load, out int value)
        {
            OpCode op = load.opcode;
            value = 0;
            if (op == OpCodes.Ldc_I4_M1) value = -1;
            else if (op == OpCodes.Ldc_I4_0) value = 0;
            else if (op == OpCodes.Ldc_I4_1) value = 1;
            else if (op == OpCodes.Ldc_I4_2) value = 2;
            else if (op == OpCodes.Ldc_I4_3) value = 3;
            else if (op == OpCodes.Ldc_I4_4) value = 4;
            else if (op == OpCodes.Ldc_I4_5) value = 5;
            else if (op == OpCodes.Ldc_I4_6) value = 6;
            else if (op == OpCodes.Ldc_I4_7) value = 7;
            else if (op == OpCodes.Ldc_I4_8) value = 8;
            else if ((op == OpCodes.Ldc_I4_S || op == OpCodes.Ldc_I4) && load.operand != null) value = Convert.ToInt32(load.operand);
            else return false;
            return true;
        }

        /// <summary>
        /// For tests: forget which sites were changed.
        /// </summary>
        public static void Reset() => Changed.Clear();
    }

    // Low priority: another mod's transpiler on the same method runs first, and its changes are left alone

    [HarmonyPatch(typeof(Controls), nameof(Controls.Update))]
    public class Controls_Update_Keys
    {
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SiteTranspiler.Apply(typeof(Controls), nameof(Controls.Update), instructions);
    }

    [HarmonyPatch(typeof(Widget_Details), nameof(Widget_Details.Update))]
    public class Widget_Details_Update_Keys
    {
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SiteTranspiler.Apply(typeof(Widget_Details), nameof(Widget_Details.Update), instructions);
    }

    [HarmonyPatch(typeof(Scenes), nameof(Scenes.Update))]
    public class Scenes_Update_Keys
    {
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SiteTranspiler.Apply(typeof(Scenes), nameof(Scenes.Update), instructions);
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Update))]
    public class SaveManager_Update_Keys
    {
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SiteTranspiler.Apply(typeof(SaveManager), nameof(SaveManager.Update), instructions);
    }

    [HarmonyPatch(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update))]
    public class ActiveDialogueController_Update_Keys
    {
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SiteTranspiler.Apply(typeof(ActiveDialogueController), nameof(ActiveDialogueController.Update), instructions);
    }

    [HarmonyPatch(typeof(Profile_Popup), nameof(Profile_Popup.Update))]
    public class Profile_Popup_Update_Keys
    {
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SiteTranspiler.Apply(typeof(Profile_Popup), nameof(Profile_Popup.Update), instructions);
    }

    [HarmonyPatch(typeof(Video_Manager), nameof(Video_Manager.Update))]
    public class Video_Manager_Update_Keys
    {
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SiteTranspiler.Apply(typeof(Video_Manager), nameof(Video_Manager.Update), instructions);
    }

    [HarmonyPatch(typeof(DEBUG), nameof(DEBUG.Update))]
    public class DEBUG_Update_Keys
    {
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SiteTranspiler.Apply(typeof(DEBUG), nameof(DEBUG.Update), instructions);
    }

    /// <summary>
    /// Answers each row's enabledIf: a row is shown while the game still reads one of its keys through it.
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.Get))]
    public class variables_Get
    {
        public static void Postfix(string name, ref string __result)
        {
            if (name == null || !name.StartsWith(Rows.HAS_PREFIX, StringComparison.Ordinal))
            {
                return;
            }
            if (Rows.ByVarID.TryGetValue(Rows.PREFIX + name.Substring(Rows.HAS_PREFIX.Length), out Row row))
            {
                __result = SiteTranspiler.IsAvailable(row) ? "1" : "0";
            }
        }
    }
}
