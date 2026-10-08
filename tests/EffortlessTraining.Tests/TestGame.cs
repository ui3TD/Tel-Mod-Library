using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

// Every test shares the game's static state (policies, game speed).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EffortlessTraining.Tests
{
    /// <summary>
    /// Builds the minimal game state a training tick reads: the policies, the game speed, and an idol in a room.
    /// </summary>
    public static class TestGame
    {
        public const string ModNamespace = "EffortlessTraining";

        // Not "tests.EffortlessTraining": PatchTargetTests unpatches everything under that ID
        public const string HarmonyId = "tests.EffortlessTraining.Behaviour";

        private static readonly Lazy<bool> Patched = new(() =>
        {
            Harmony harmony = new(HarmonyId);
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(agency__room_DoGirlTraining).Assembly, ModNamespace))
                harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        });

        /// <summary>
        /// Resets the game state to normal speed and default policies. With patched set, the mod is applied
        /// to the game's methods (once per test run), so tests can call them as the game would.
        /// </summary>
        public static void Reset(bool patched = false)
        {
            if (patched)
                _ = Patched.Value;

            staticVars.dateTimeAddMinutesPerSecond = 50.0;
            staticVars.dateTimeDivider = 4f;
            UsePolicies(policies._value.performances_neutral);
        }

        /// <summary>
        /// Selects this performances policy, and a training policy that keeps practice going while stamina is above 5.
        /// </summary>
        public static void UsePolicies(policies._value performances)
        {
            policies.Values = new List<policies.value>
            {
                new() { Type = policies._type.performances, Value = performances, Selected = true },
                new() { Type = policies._type.training, Value = policies._value.training_stop_at_0, Selected = true },
            };
        }

        /// <summary>
        /// An idol with every stat at 40 and both staminas at 100.
        /// </summary>
        public static data_girls.girls Idol(traits._trait._type trait = traits._trait._type.None)
        {
            data_girls.girls girl = new() { trait = trait };
            foreach (data_girls._paramType type in Enum.GetValues(typeof(data_girls._paramType)))
            {
                float value = type is data_girls._paramType.physicalStamina or data_girls._paramType.mentalStamina ? 100f : 40f;
                girl.parameters.Add(new data_girls.girls.param { type = type, _val = value, Parent = girl });
            }
            return girl;
        }

        /// <summary>
        /// A room of this type where the idol is part-way through practicing (no coach, so no staff work starts).
        /// </summary>
        public static agency._room TrainingRoom(agency._type type, data_girls.girls girl, data_girls._paramType style = data_girls._paramType.cute) => new()
        {
            type = type,
            status = agency._room._status.girlTraining,
            practicing_style = style,
            girl = girl,
            Progress = 0.5f,
        };

        /// <summary>
        /// The number of training ticks in one in-game day at the current game speed, as the game counts them.
        /// </summary>
        public static int TicksPerDay() => (int)Math.Floor(1440f / (float)(staticVars.dateTimeAddMinutesPerSecond / staticVars.dateTimeDivider));

        /// <summary>
        /// Runs the room's training tick (a private game method) this many times.
        /// </summary>
        public static void Train(agency._room room, int ticks)
        {
            for (int i = 0; i < ticks; i++)
                Traverse.Create(room).Method("DoGirlTraining").GetValue();
        }

        public static float Stamina(data_girls.girls girl) => girl.getParam(data_girls._paramType.physicalStamina).val;

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModFile(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Effortless Training", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));
    }
}
