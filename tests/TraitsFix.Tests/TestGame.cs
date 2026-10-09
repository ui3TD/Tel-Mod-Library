using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using TraitFix;
using UnityEngine;
using Xunit;

// Every test shares the game's static state (idols, singles, relationships, policies, date) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TraitsFixTests
{
    /// <summary>
    /// Builds the game state the trait code reads, and applies the mod to the game's methods.
    /// </summary>
    public static class TestGame
    {
        public static readonly DateTime Today = new(2023, 6, 5);

        private static readonly Lazy<bool> Patched = new(() =>
        {
            // Not "tests.TraitFix": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.TraitFix.Behaviour");
            Assembly mod = typeof(TraitsFix).Assembly;
            foreach (string ns in new[] { "TraitFix", "StatLimits" })
            {
                foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(mod, ns))
                {
                    try
                    {
                        harmony.CreateClassProcessor(patchClass).Patch();
                    }
                    catch (HarmonyException e) when (e.InnerException is System.Security.SecurityException)
                    {
                        // The target calls Unity native methods, which can't be compiled outside the game
                        // (e.g. Instantiate in Birthday_Popup.DoParam). Those patches are tested directly.
                    }
                }
            }
            return true;
        });

        /// <summary>
        /// Resets the game state and the mod's calculation contexts. With patched set, the mod is applied
        /// to the game's methods (once per test run), so tests can call them as the game would.
        /// </summary>
        public static void Reset(bool patched = false)
        {
            if (patched)
                _ = Patched.Value;
            Seams.Reset();

            staticVars.dateTime = Today;
            data_girls.girl = new List<data_girls.girls>();
            singles.Singles = new List<singles._single>();
            Groups.Groups_ = new List<Groups._group> { new() { ID = 0, Girls = new List<data_girls.girls>() } };
            Relationships.RelationshipsData = new List<Relationships._relationship>();
            Rivals.Date_To_Month = new List<Rivals._date_to_month_id>();
            Rivals.Groups = new List<Rivals._group>();
            SEvent_Tour.Tours = new List<SEvent_Tour.tour>();
            SEvent_SSK.Elections = new List<SEvent_SSK._SSK>();
            SEvent_Concerts.Concerts = new List<SEvent_Concerts._concert>();
            resources.resource = new List<long>(new long[32]);
            SetDatingPolicy(policies._value.dating_allowed);

            ClearStack("traitCalculationContexts");
            ClearStack("birthdayDeteriorationContexts");
        }

        private static void ClearStack(string field)
        {
            object stack = AccessTools.Field(typeof(TraitsFix), field).GetValue(null);
            stack.GetType().GetMethod("Clear").Invoke(stack, null);
        }

        public static void SetDatingPolicy(policies._value value)
        {
            policies.Values = new List<policies.value>
            {
                new() { Type = policies._type.dating, Value = value, Selected = true },
                // A vibe that boosts none of the stats the tests read, so senbatsu scores aren't scaled
                new() { Type = policies._type.vibe, Value = policies._value.vibe_cute, Selected = true },
            };
        }

        /// <summary>
        /// The main group, which every hired idol joins.
        /// </summary>
        public static Groups._group MainGroup => Groups.Groups_[0];

        /// <summary>
        /// An active idol with this trait, aged 20 with a peak age of 18. Every stat starts at statValue,
        /// stamina at 100, and fame and scandal points at 0.
        /// </summary>
        public static data_girls.girls Idol(traits._trait._type trait = traits._trait._type.None, float statValue = 40f, string name = "Idol", int age = 20)
        {
            data_girls.girls girl = new() { nickname = name, trait = trait, peakAge = 18, birthday = Today.AddYears(-age).AddDays(-1) };
            foreach (data_girls._paramType type in Enum.GetValues(typeof(data_girls._paramType)))
            {
                float value = type switch
                {
                    data_girls._paramType.physicalStamina or data_girls._paramType.mentalStamina => 100f,
                    data_girls._paramType.famePoints or data_girls._paramType.scandalPoints => 0f,
                    _ => statValue,
                };
                girl.parameters.Add(new data_girls.girls.param { type = type, _val = value, Parent = girl });
            }
            return girl;
        }

        /// <summary>
        /// Adds the idol to the agency and its main group.
        /// </summary>
        public static data_girls.girls Hire(data_girls.girls girl)
        {
            data_girls.girl.Add(girl);
            MainGroup.Girls.Add(girl);
            return girl;
        }

        /// <summary>
        /// A single released on the given date by the main group, with these idols in senbatsu order
        /// (the first is the center).
        /// </summary>
        public static singles._single ReleaseSingle(DateTime releaseDate, int chartPosition = 0, long sales = 1000, params data_girls.girls[] girls)
        {
            singles._single single = new() { id = singles.Singles.Count + 1, status = singles._single._status.released };
            single.ReleaseData.ReleaseDate = releaseDate;
            single.ReleaseData.Chart_Position = chartPosition;
            single.ReleaseData.Sales = sales;
            single.girls.AddRange(girls);
            singles.Singles.Add(single);
            MainGroup.Singles.Add(single);
            return single;
        }

        /// <summary>
        /// Adds the monthly chart dated in this month, with rival singles at these sales figures.
        /// As in the game, it lists the player's singles released the month before.
        /// </summary>
        public static void AddChart(DateTime month, params long[] rivalSales)
        {
            int id = Rivals.Date_To_Month.Count + 1;
            Rivals.Date_To_Month.Add(new Rivals._date_to_month_id { ID = id, Date = new DateTime(month.Year, month.Month, 1) });
            foreach (long sales in rivalSales)
            {
                Rivals._group rival = new();
                rival.Singles.Add(new Rivals._group._single { MonthID = id, Sales = sales, Group = rival });
                Rivals.Groups.Add(rival);
            }
        }

        public static float Stat(data_girls.girls girl, data_girls._paramType type) => girl.getParam(type).val;

        public static void SetStat(data_girls.girls girl, data_girls._paramType type, float value) => girl.getParam(type)._val = value;

        /// <summary>
        /// The game's components are MonoBehaviours, which can't be constructed outside Unity.
        /// The methods under test don't read their other fields.
        /// </summary>
        public static T Component<T>() => (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T));

        /// <summary>
        /// A component Unity's == operator treats as alive, for code that checks it against null.
        /// </summary>
        public static T LiveComponent<T>() where T : UnityEngine.Object
        {
            T component = Component<T>();
            AccessTools.Field(typeof(UnityEngine.Object), "m_CachedPtr").SetValue(component, new IntPtr(1));
            return component;
        }

        /// <summary>
        /// Calls a private game method, unwrapping exceptions it throws.
        /// </summary>
        public static object CallPrivate(object instance, Type type, string method, params object[] args)
        {
            try
            {
                return AccessTools.Method(type, method).Invoke(instance, args);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModFile(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Traits Fix", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));

        /// <summary>
        /// Parses one of the mod's JSON files the way the game does.
        /// </summary>
        public static SimpleJSON.JSONNode LoadJson(string relativePath) =>
            mainScript.ProcessInboundData(File.ReadAllText(ModAsset(relativePath)));
    }

    /// <summary>
    /// Some of the mod's patches call game methods that reach Unity's native code (Random, notifications,
    /// the scandal counter, UI). Those can't run outside the game, so the mod's own methods are patched to
    /// call these stubs instead.
    /// </summary>
    public static class Seams
    {
        public static Func<float, bool> Chance;

        public static readonly List<float> ChancesRolled = new();
        public static readonly List<(data_girls.girls girl, data_girls._paramType type, float val)> ParamsAdded = new();
        public static readonly List<string> Notifications = new();
        public static readonly List<data_girls.girls> Deteriorated = new();
        public static readonly List<Shows._show._castType> CastTypesSet = new();

        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new()
        {
            [AccessTools.Method(typeof(mainScript), nameof(mainScript.chance), new[] { typeof(int) })] = Stub(nameof(StubChanceInt)),
            [AccessTools.Method(typeof(mainScript), nameof(mainScript.chance), new[] { typeof(float) })] = Stub(nameof(StubChance)),
            [AccessTools.Method(typeof(NotificationManager), nameof(NotificationManager.AddNotification), new[] { typeof(string), typeof(Color32), typeof(NotificationManager._notification._type) })] = Stub(nameof(StubNotification)),
            [AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.addParam))] = Stub(nameof(StubAddParam)),
            [AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.AgeDeterioration))] = Stub(nameof(StubAgeDeterioration)),
            [AccessTools.Method(typeof(Show_Popup), nameof(Show_Popup.SetCastType))] = Stub(nameof(StubSetCastType)),
        };

        private static readonly MethodBase[] Patched =
        {
            AccessTools.Method(typeof(Data_girls_AgeDeterioration), nameof(Data_girls_AgeDeterioration.Postfix)),
            AccessTools.Method(typeof(Data_girls_girls_UpdateDatingStatus), nameof(Data_girls_girls_UpdateDatingStatus.Postfix)),
            AccessTools.Method(typeof(TraitsFix), nameof(TraitsFix.TryLeakCouple)),
            AccessTools.Method(typeof(Show_Popup_SetParam), nameof(Show_Popup_SetParam.Postfix)),
            AccessTools.Method(typeof(Shows__show_SetStamina), nameof(Shows__show_SetStamina.Postfix)),
            AccessTools.Method(typeof(Singles_ReleaseSingle), nameof(Singles_ReleaseSingle.Postfix)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            Harmony harmony = new("tests.TraitFix.Seams");
            HarmonyMethod transpiler = new(typeof(Seams), nameof(Redirect));
            foreach (MethodBase method in Patched)
                harmony.Patch(method, transpiler: transpiler);
            return true;
        });

        /// <summary>
        /// Installs the stubs once per test run, clears what earlier tests recorded, and loads the mod's
        /// notification text into the game's language table.
        /// </summary>
        public static void Reset()
        {
            _ = Installed.Value;
            Chance = _ => false;
            ChancesRolled.Clear();
            ParamsAdded.Clear();
            Notifications.Clear();
            Deteriorated.Clear();
            CastTypesSet.Clear();

            SimpleJSON.JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            for (int i = 0; i < constants.Count; i++)
                Language.Data[constants[i]["id"]] = constants[i]["text"];
        }

        private static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                    && instruction.operand is MethodBase target && Redirects.TryGetValue(target, out MethodInfo stub))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = stub;
                }
                yield return instruction;
            }
        }

        private static MethodInfo Stub(string name) => AccessTools.Method(typeof(Seams), name);

        private static bool StubChance(float num)
        {
            ChancesRolled.Add(num);
            return Chance(num);
        }

        private static bool StubChanceInt(int num) => StubChance(num);
        private static void StubNotification(string text, Color32 color, NotificationManager._notification._type type) => Notifications.Add(text);
        private static void StubAddParam(data_girls.girls girl, data_girls._paramType type, float val, bool ignorePotential) => ParamsAdded.Add((girl, type, val));
        private static void StubAgeDeterioration(data_girls.girls girl) => Deteriorated.Add(girl);
        private static void StubSetCastType(Show_Popup popup, Shows._show._castType type) => CastTypesSet.Add(type);
    }
}
