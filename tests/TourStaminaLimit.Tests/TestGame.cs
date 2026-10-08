using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TourStamina;
using UnityEngine;
using Xunit;
using _country = SEvent_Tour._country;

// Every test shares the game's static state (tour constants, fame, text) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TourStaminaLimit.Tests
{
    /// <summary>
    /// Builds the world tour state the mod reads, and applies the mod to the game's methods.
    /// </summary>
    public static class TestGame
    {
        public const string ModNamespace = "TourStamina";

        /// <summary>
        /// The game's English text the tooltip uses.
        /// </summary>
        public static readonly Dictionary<string, string> GameText = new()
        {
            ["STAMINA"] = "Stamina",
            ["PT"] = "pt",
            ["COST"] = "Cost",
            ["SHOW__REVENUE"] = "Revenue",
            ["TOUR__FATIGUE"] = "Fatigue",
        };

        private static readonly Lazy<bool> Patched = new(() =>
        {
            // Not "tests.TourStamina": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.TourStamina.Behaviour");

            // Picking a country recalculates the tour's cost and expected revenue too, which read awards,
            // policies and the player's chapter. The stamina cap only needs the stamina recalculation.
            HarmonyMethod skip = new(typeof(TestGame), nameof(SkipOriginal));
            harmony.Patch(AccessTools.Method(typeof(SEvent_Tour.tour), "RecalcProductionCost"), prefix: skip);
            harmony.Patch(AccessTools.Method(typeof(SEvent_Tour.tour), "RecalcExpectedRevenue"), prefix: skip);

            Seams.Install(harmony);

            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(SEvent_Tour_tour_SelectCountry).Assembly, ModNamespace))
            {
                try
                {
                    harmony.CreateClassProcessor(patchClass).Patch();
                }
                catch (HarmonyException e) when (e.InnerException is System.Security.SecurityException)
                {
                    // Tour_Country.OnClick and the fan roll call Unity native methods, which can't be compiled
                    // outside the game. Their patches are tested through Seams.OnClickPostfix and Seams.NewFansByAttendance.
                }
            }
            return true;
        });

        private static bool SkipOriginal() => false;

        /// <summary>
        /// Resets the game state and loads the mod's text into the game's language table. With patched set,
        /// the mod is applied to the game's methods (once per test run), so tests can call them as the game would.
        /// </summary>
        public static void Reset(bool patched = false)
        {
            if (patched)
                _ = Patched.Value;
            Seams.Reset();

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            staticVars.dateTime = new DateTime(2023, 6, 5);
            resources.resource = Enumerable.Repeat(0L, Enum.GetValues(typeof(resources.type)).Length).ToList();
            resources.resource[(int)resources.type.fame] = 10;
            SEvent_Tour.Const_Capacity = new List<int> { 1000, 2000, 5000, 10000, 20000 };
            SEvent_Tour.Const_CostCoeff = new List<int> { 1, 2, 4, 8, 16 };

            Language.Data.Clear();
            foreach (KeyValuePair<string, string> text in GameText)
                Language.Data[text.Key] = text.Value;
            foreach (KeyValuePair<string, string> text in Constants())
                Language.Data[text.Key] = text.Value;
        }

        /// <summary>
        /// The mod's text entries, parsed the way the game does.
        /// </summary>
        public static Dictionary<string, string> Constants()
        {
            SimpleJSON.JSONNode constants = mainScript.ProcessInboundData(File.ReadAllText(ModAsset("JSON/Constants/constants.json")));
            return Enumerable.Range(0, constants.Count).ToDictionary(i => constants[i]["id"].Value, i => constants[i]["text"].Value);
        }

        /// <summary>
        /// A country the player can tour. France costs 10 stamina, every other country 20.
        /// </summary>
        public static SEvent_Tour.country Country(_country type = _country.china) =>
            new() { Type = type, Area = new SEvent_Tour._area(), Cost = 50000, TicketPrice = 3000 };

        /// <summary>
        /// A new tour with these countries already picked at level 1, as the game picks them.
        /// </summary>
        public static SEvent_Tour.tour Tour(params SEvent_Tour.country[] countries)
        {
            SEvent_Tour.tour tour = new();
            foreach (SEvent_Tour.country country in countries)
                tour.SelectedCountries.Add(new SEvent_Tour.tour.selectedCountry { Country = country, Level = 1 });
            tour.Stamina = countries.Sum(c => c.GetStaminaCost());
            return tour;
        }

        /// <summary>
        /// This many distinct 20-stamina countries.
        /// </summary>
        public static SEvent_Tour.country[] Countries(int count) =>
            Enum.GetValues(typeof(_country)).Cast<_country>().Where(c => c != _country.france).Take(count).Select(c => Country(c)).ToArray();

        /// <summary>
        /// A country button in the new tour popup.
        /// </summary>
        public static Tour_Country CountryButton(SEvent_Tour.country country, SEvent_Tour.tour tour)
        {
            Tour_Country button = Component<Tour_Country>();
            button.Country = country;
            button.CountryType = country.Type;
            button.TourPopup = Component<Tour_New_Popup>();
            button.TourPopup.Tour = tour;
            return button;
        }

        /// <summary>
        /// One of a country button's level stars, which carry the country's tooltip.
        /// </summary>
        public static Tour_Star Star(SEvent_Tour.country country, SEvent_Tour.tour tour, int id = 0)
        {
            Tour_Star star = Component<Tour_Star>();
            star.ID = id;
            AccessTools.Field(typeof(Tour_Star), "TourCountry").SetValue(star, CountryButton(country, tour));
            return star;
        }

        /// <summary>
        /// The game's components are MonoBehaviours, which can't be constructed outside Unity.
        /// The methods under test don't read their other fields.
        /// </summary>
        public static T Component<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModFile(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Tour Stamina Limit", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));
    }

    /// <summary>
    /// Unity's == treats every fake object as destroyed, and so equal; fakes are told apart by reference.
    /// </summary>
    public class SameObject : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// The patched game methods reach Unity's native code (Random, components, transforms). That can't run
    /// outside the game, so those calls are redirected to these stubs.
    /// </summary>
    public static class Seams
    {
        /// <summary>
        /// Random.Range(float, float), as the game's fan roll calls it.
        /// </summary>
        public static Func<float, float, float> Range;
        public static readonly List<(float min, float max)> RangesRolled = new();

        /// <summary>
        /// The tooltips set on star buttons, in order.
        /// </summary>
        public static readonly List<string> Tooltips = new();

        /// <summary>
        /// The country buttons under each popup's countries container, and the ones redrawn since the last Reset.
        /// </summary>
        public static readonly Dictionary<object, Tour_Country[]> Children = new(new SameObject());
        public static readonly List<Tour_Country> Updated = new();

        private static readonly Dictionary<object, Transform> Transforms = new(new SameObject());

        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new()
        {
            [AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(float), typeof(float) })] = Stub(nameof(StubRange)),
            [AccessTools.Method(typeof(ButtonDefault), nameof(ButtonDefault.SetTooltip))] = Stub(nameof(StubSetTooltip)),
            [AccessTools.PropertyGetter(typeof(GameObject), nameof(GameObject.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.Method(typeof(Tour_Country), nameof(Tour_Country.UpdateData))] = Stub(nameof(StubUpdateData)),
        };

        private static readonly MethodBase[] Patched =
        {
            AccessTools.Method(typeof(Tour_Star), nameof(Tour_Star.SetTooltip)),
        };

        private static bool installed;

        /// <summary>
        /// Redirects the native calls in the game methods the mod patches. Called before the mod is applied.
        /// </summary>
        public static void Install(Harmony harmony)
        {
            HarmonyMethod transpiler = new(typeof(Seams), nameof(Redirect));
            List<string> failed = new();
            foreach (MethodBase method in Patched)
            {
                try { harmony.Patch(method, transpiler: transpiler); }
                catch (HarmonyException e) { failed.Add(method.DeclaringType.Name + "." + method.Name + ": " + e.InnerException?.Message); }
            }
            if (failed.Count > 0)
                throw new InvalidOperationException(string.Join("; ", failed));
        }

        /// <summary>
        /// The click postfix reads GameObject.transform, an engine call the tests can't even compile, so
        /// Harmony can't patch it in place (it compiles the original first). This is a copy of the postfix
        /// with the same stubs; tests call it instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OnClickPostfix(Tour_Country __instance) =>
            throw new InvalidOperationException("Call Seams.Reset first");

        /// <summary>
        /// The game's fan roll calls Random.Range directly, so it can't be patched in place either. This is a
        /// copy of the game method with the stubs; the mod's postfix is applied to its result.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GameNewFansByAttendance(SEvent_Tour.tour instance, int attendance) =>
            throw new InvalidOperationException("Call Seams.Reset first");

        /// <summary>
        /// The new fans from a concert with this attendance, with the mod applied.
        /// </summary>
        public static int NewFansByAttendance(SEvent_Tour.tour tour, int attendance)
        {
            int fans = GameNewFansByAttendance(tour, attendance);
            SEvent_Tour_tour_GetNewFansByAttendance.Postfix(ref fans);
            return fans;
        }

        public static void Reset()
        {
            if (!installed)
            {
                Harmony.ReversePatch(
                    AccessTools.Method(typeof(Tour_Country_OnClick), nameof(Tour_Country_OnClick.Postfix)),
                    new HarmonyMethod(typeof(Seams), nameof(OnClickPostfix)),
                    AccessTools.Method(typeof(Seams), nameof(Redirect)),
                    ilmanipulator: null);
                Harmony.ReversePatch(
                    AccessTools.Method(typeof(SEvent_Tour.tour), nameof(SEvent_Tour.tour.GetNewFansByAttendance)),
                    new HarmonyMethod(typeof(Seams), nameof(GameNewFansByAttendance)),
                    AccessTools.Method(typeof(Seams), nameof(Redirect)),
                    ilmanipulator: null);
                installed = true;
            }

            Range = (min, max) => throw new InvalidOperationException($"Unexpected roll Range({min}, {max})");
            RangesRolled.Clear();
            Tooltips.Clear();
            Children.Clear();
            Updated.Clear();
            Transforms.Clear();
        }

        /// <summary>
        /// The fake transform of a GameObject.
        /// </summary>
        public static Transform TransformOf(object owner)
        {
            if (!Transforms.TryGetValue(owner, out Transform transform))
                Transforms[owner] = transform = TestGame.Component<Transform>();
            return transform;
        }

        private static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo generic
                    && generic.IsGenericMethod && generic.GetParameters().Length == 0
                    && (generic.DeclaringType == typeof(GameObject) || generic.DeclaringType == typeof(Component))
                    && (generic.Name == nameof(Component.GetComponent) || generic.Name == nameof(Component.GetComponentsInChildren)))
                {
                    // GetComponent<T>() and GetComponentsInChildren<T>() call native code; the JIT can inline them
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = Stub(generic.Name == nameof(Component.GetComponent) ? nameof(StubGetComponent) : nameof(StubGetComponentsInChildren))
                        .MakeGenericMethod(generic.GetGenericArguments());
                }
                else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                    && instruction.operand is MethodBase target && Redirects.TryGetValue(target, out MethodInfo stub))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = stub;
                }
                yield return instruction;
            }
        }

        private static MethodInfo Stub(string name) => AccessTools.Method(typeof(Seams), name);

        private static float StubRange(float min, float max)
        {
            RangesRolled.Add((min, max));
            return Range(min, max);
        }

        private static T StubGetComponent<T>(object owner) => TestGame.Component<T>();

        private static T[] StubGetComponentsInChildren<T>(object owner)
        {
            Assert.Equal(typeof(Tour_Country), typeof(T));
            return Children.TryGetValue(owner, out Tour_Country[] children) ? (T[])(object)children : new T[0];
        }

        private static void StubSetTooltip(ButtonDefault button, string text) => Tooltips.Add(text);
        private static Transform StubTransform(object owner) => TransformOf(owner);
        private static void StubUpdateData(Tour_Country country) => Updated.Add(country);
    }
}
