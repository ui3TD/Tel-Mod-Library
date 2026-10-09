using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static policies._value;

namespace PoliciesThatMatter.Tests
{
    public class PerformanceTests : IDisposable
    {
        private readonly double minutesPerSecond = staticVars.dateTimeAddMinutesPerSecond;
        private readonly float divider = staticVars.dateTimeDivider;

        public PerformanceTests()
        {
            Seams.Reset();
        }

        public void Dispose()
        {
            staticVars.dateTimeAddMinutesPerSecond = minutesPerSecond;
            staticVars.dateTimeDivider = divider;
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(10000, 15000)]
        [InlineData(18000, 27000)]
        [InlineData(50000, 75000)]
        public void Energetic_PaysHalfAgainForPerformances(int vanilla, int expected)
        {
            TestPolicies.Use(performances_energy);
            int result = vanilla;
            Activities_GetPerformanceMoneyPerLevel.Postfix(false, ref result);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Café dish revenue reads the same table with forceHard, and keeps the vanilla value.
        /// </summary>
        [Fact]
        public void Energetic_LeavesCafeDishesAlone()
        {
            TestPolicies.Use(performances_energy);
            int result = 10000;
            Activities_GetPerformanceMoneyPerLevel.Postfix(true, ref result);
            Assert.Equal(10000, result);
        }

        [Theory]
        [InlineData(performances_neutral)]
        [InlineData(performances_quality)]
        public void OtherPolicies_KeepPerformancePay(policies._value policy)
        {
            TestPolicies.Use(policy);
            int result = 10000;
            Activities_GetPerformanceMoneyPerLevel.Postfix(false, ref result);
            Assert.Equal(10000, result);
        }

        [Theory]
        [InlineData(performances_quality, data_girls._paramType.cute, 500f)]
        [InlineData(performances_quality, data_girls._paramType.vocal, 500f)]
        [InlineData(performances_quality, data_girls._paramType.physicalStamina, 1000f)]
        [InlineData(performances_quality, data_girls._paramType.mentalStamina, 1000f)]
        [InlineData(performances_neutral, data_girls._paramType.cute, 1000f)]
        [InlineData(performances_energy, data_girls._paramType.cute, 1000f)]
        public void Quality_HalvesStatTrainingTime(policies._value policy, data_girls._paramType stat, float expected)
        {
            TestPolicies.Use(policy);
            float duration = 1000f;
            data_girls_girls_param_GetDuration.Postfix(ref duration, new data_girls.girls.param { type = stat });
            Assert.Equal(expected, duration);
        }

        /// <summary>
        /// The drain runs every training tick, sized so a week of ticks costs 0.5 mental stamina
        /// at any game speed.
        /// </summary>
        [Theory]
        [InlineData(50.0, 4f)]
        [InlineData(100.0, 4f)]
        [InlineData(200.0, 4f)]
        public void Quality_DrainsHalfAMentalPointPerWeekOfTraining(double minutesPerSecond, float divider)
        {
            staticVars.dateTimeAddMinutesPerSecond = minutesPerSecond;
            staticVars.dateTimeDivider = divider;
            TestPolicies.Use(performances_quality);
            data_girls.girls girl = new();
            agency._room room = new() { girl = girl };

            int ticksPerWeek = 7 * (int)Math.Floor(1440f / (float)(minutesPerSecond / divider));
            for (int i = 0; i < ticksPerWeek; i++)
                agency__room_DoGirlTraining.Infix(room);

            Assert.All(Seams.ParamsAdded, p => Assert.Equal((girl, data_girls._paramType.mentalStamina), (p.girl, p.type)));
            Assert.Equal(-0.5f, Seams.ParamsAdded.Sum(p => p.val), 3);
        }

        [Theory]
        [InlineData(performances_neutral)]
        [InlineData(performances_energy)]
        public void OtherPolicies_DontDrainTraining(policies._value policy)
        {
            TestPolicies.Use(policy);
            agency__room_DoGirlTraining.Infix(new agency._room { girl = new data_girls.girls() });
            Assert.Empty(Seams.ParamsAdded);
        }

        [Fact]
        public void EmptyRoom_DoesNothing()
        {
            TestPolicies.Use(performances_quality);
            agency__room_DoGirlTraining.Infix(new agency._room());
            Assert.Empty(Seams.ParamsAdded);
        }

        /// <summary>
        /// The drain is inserted where the game works out the per-tick stamina cost of training,
        /// just before it checks the Quality policy.
        /// </summary>
        [Fact]
        public void Drain_IsInsertedBeforeTheQualityCheck()
        {
            MethodInfo original = AccessTools.Method(typeof(agency._room), "DoGirlTraining");
            List<CodeInstruction> patched = agency__room_DoGirlTraining.Transpiler(PatchProcessor.GetOriginalInstructions(original)).ToList();
            MethodInfo infix = AccessTools.Method(typeof(agency__room_DoGirlTraining), nameof(agency__room_DoGirlTraining.Infix));
            MethodInfo policyCheck = AccessTools.Method(typeof(policies), nameof(policies.GetSelectedPolicyValue));

            int index = Assert.Single(Enumerable.Range(0, patched.Count), i => patched[i].Calls(infix));

            // ... 3f / Mathf.Floor(...); store the cost; this.Infix(); GetSelectedPolicyValue(performances) ...
            Assert.Equal(OpCodes.Div, patched[index - 3].opcode);
            Assert.Equal(OpCodes.Stloc_S, patched[index - 2].opcode);
            Assert.Equal(OpCodes.Ldarg_0, patched[index - 1].opcode);
            Assert.Equal((int)policies._type.performances, Convert.ToInt32(patched[index + 1].operand));
            Assert.True(patched[index + 2].Calls(policyCheck));
        }
    }

    public class BackgroundCheckTests
    {
        public BackgroundCheckTests()
        {
            Seams.Reset();
            traits.Traits = new List<traits._trait>
            {
                new() { Type = traits._trait._type.Moonlighter, Positive = true },
                new() { Type = traits._trait._type.Clumsy, Positive = false },
                new() { Type = traits._trait._type.Spoiled, Positive = true },
                new() { Type = traits._trait._type.Prodigy, Positive = true },
            };
        }

        /// <summary>
        /// Extensive always rolls among positive traits, leaving out Moonlighter and Spoiled like vanilla.
        /// </summary>
        [Fact]
        public void Extensive_AlwaysGivesAPositiveTrait()
        {
            TestPolicies.Use(background_check_extensive);

            traits._trait._type result = traits._trait._type.Clumsy;
            traits_GetRandomTraitType.Postfix(ref result);

            Assert.Equal(traits._trait._type.Prodigy, result);
            Assert.Equal((0, 1), Assert.Single(Seams.RangesRolled));
        }

        /// <summary>
        /// Fixed in 1.2.0: with only Moonlighter and Spoiled among the positive traits, the roll repeated forever.
        /// Now the game's own roll is kept.
        /// </summary>
        [Fact]
        public void Extensive_NoAllowedPositiveTrait_KeepsTheGamesRoll()
        {
            TestPolicies.Use(background_check_extensive);
            traits.Traits.RemoveAll(t => t.Type == traits._trait._type.Prodigy);

            traits._trait._type result = traits._trait._type.Clumsy;
            traits_GetRandomTraitType.Postfix(ref result);

            Assert.Equal(traits._trait._type.Clumsy, result);
            Assert.Empty(Seams.RangesRolled);
        }

        [Theory]
        [InlineData(background_check_no_check)]
        [InlineData(background_check_basic)]
        public void OtherPolicies_KeepTheVanillaRoll(policies._value policy)
        {
            TestPolicies.Use(policy);
            traits._trait._type result = traits._trait._type.Clumsy;
            traits_GetRandomTraitType.Postfix(ref result);
            Assert.Equal(traits._trait._type.Clumsy, result);
            Assert.Empty(Seams.RangesRolled);
        }
    }

    public class AppealTests
    {
        public static IEnumerable<object[]> Patches()
        {
            yield return new object[] { "show" };
            yield return new object[] { "single" };
        }

        private static float Run(string patch, resources.fanType fanType)
        {
            float appeal = 1f;
            if (patch == "show")
                Shows_GetBaseAppeal.Postfix(ref appeal, fanType);
            else
                singles__single_GetBaseAppeal.Postfix(ref appeal, fanType);
            return appeal;
        }

        public AppealTests()
        {
            Seams.Reset();
        }

        [Theory]
        [MemberData(nameof(Patches))]
        public void Rebellious_CostsAdultAppeal(string patch)
        {
            TestPolicies.Use(image_rebellious, dating_forbidden);
            foreach (resources.fanType fanType in Enum.GetValues(typeof(resources.fanType)))
                Assert.Equal(fanType == resources.fanType.adult ? 0.6f : 1f, Run(patch, fanType), 5);
        }

        [Theory]
        [MemberData(nameof(Patches))]
        public void DatingAllowed_CostsHardcoreAppeal(string patch)
        {
            TestPolicies.Use(image_neutral, dating_allowed);
            foreach (resources.fanType fanType in Enum.GetValues(typeof(resources.fanType)))
                Assert.Equal(fanType == resources.fanType.hardcore ? 0.75f : 1f, Run(patch, fanType), 5);
        }

        [Theory]
        [MemberData(nameof(Patches))]
        public void OtherPolicies_KeepAppeal(string patch)
        {
            foreach (policies._value image in new[] { image_neutral, image_orthodox })
            {
                foreach (policies._value dating in new[] { dating_forbidden, dating_ambiguous })
                {
                    TestPolicies.Use(image, dating);
                    foreach (resources.fanType fanType in Enum.GetValues(typeof(resources.fanType)))
                        Assert.Equal(1f, Run(patch, fanType));
                }
            }
        }
    }

    public class WeeklyStaminaTests
    {
        private static readonly DateTime Today = new(2025, 6, 15);

        public WeeklyStaminaTests()
        {
            Seams.Reset();
            staticVars.dateTime = Today;
            Use();
        }

        /// <summary>
        /// Starts from the policies that cost no stamina (the defaults all do).
        /// </summary>
        private static void Use(params policies._value[] selected) =>
            TestPolicies.Use(new[] { social_media_forbidden, streaming_forbidden, dating_allowed }.Concat(selected).ToArray());

        private static data_girls.girls Girl(int age = 20, data_girls._status status = data_girls._status.normal)
        {
            data_girls.girls girl = new() { birthday = Today.AddYears(-age), status = status };
            data_girls.girl = new List<data_girls.girls> { girl };
            return girl;
        }

        /// <summary>
        /// Each loss is 1 to (2 x the tooltip's "about" figure - 1) points, so it averages the tooltip's
        /// figure (e.g. "about 20" is 1-39). Fixed in 1.2.0: it was 1-38, averaging 19.5.
        /// </summary>
        [Theory]
        [InlineData(social_media_no_restrictions, 20)]
        [InlineData(social_media_premoderated, 10)]
        [InlineData(streaming_no_restrictions, 10)]
        [InlineData(streaming_controlled, 5)]
        [InlineData(dating_forbidden, 10)]
        [InlineData(dating_ambiguous, 5)]
        public void Policy_CostsAboutTheTooltipsMentalStamina(policies._value policy, int tooltip)
        {
            Use(policy);
            data_girls.girls girl = Girl();
            Seams.Chance = _ => true;
            Seams.Range = (_, max) => max - 1;

            Assert.False(data_girls_PoliciesStamina.Prefix());

            Assert.Equal((1, 2 * tooltip), Assert.Single(Seams.RangesRolled));
            Assert.Equal((girl, data_girls._paramType.mentalStamina, (float)-(2 * tooltip - 1)), Assert.Single(Seams.ParamsAdded));
            Assert.Single(Seams.Notifications);
        }

        [Fact]
        public void Chances_Are10PercentSocialMedia_5Streaming_10Dating()
        {
            Girl();
            data_girls_PoliciesStamina.Prefix();
            Assert.Equal(new[] { 10, 5, 10 }, Seams.ChancesRolled);
        }

        [Fact]
        public void NoRolls_NoLoss()
        {
            Use(social_media_no_restrictions, streaming_no_restrictions, dating_forbidden);
            Girl();
            data_girls_PoliciesStamina.Prefix();
            Assert.Empty(Seams.ParamsAdded);
            Assert.Empty(Seams.Notifications);
        }

        [Fact]
        public void Losses_AddUpIntoOneChange()
        {
            Use(social_media_no_restrictions, streaming_no_restrictions, dating_forbidden);
            data_girls.girls girl = Girl();
            Seams.Chance = _ => true;
            Seams.Range = (_, max) => max - 1;

            data_girls_PoliciesStamina.Prefix();

            Assert.Equal((girl, data_girls._paramType.mentalStamina, (float)-(39 + 19 + 19)), Assert.Single(Seams.ParamsAdded));
            Assert.Equal(3, Seams.Notifications.Count);
        }

        [Theory]
        [InlineData(15, false)]
        [InlineData(16, true)]
        public void DatingLoss_StartsAt16(int age, bool loses)
        {
            Use(dating_forbidden);
            Girl(age);
            Seams.Chance = _ => true;

            data_girls_PoliciesStamina.Prefix();

            Assert.Equal(loses, Seams.ParamsAdded.Count == 1);
        }

        [Theory]
        [InlineData(data_girls._status.injured)]
        [InlineData(data_girls._status.hiatus)]
        [InlineData(data_girls._status.announced_graduation)]
        [InlineData(data_girls._status.graduated)]
        public void InactiveIdols_AreSpared(data_girls._status status)
        {
            Use(social_media_no_restrictions, streaming_no_restrictions, dating_forbidden);
            Girl(status: status);
            Seams.Chance = _ => true;

            data_girls_PoliciesStamina.Prefix();

            Assert.Empty(Seams.ChancesRolled);
            Assert.Empty(Seams.ParamsAdded);
        }
    }

    public class WeeklyResourceTests
    {
        public WeeklyResourceTests()
        {
            Seams.Reset();
            Use();
        }

        /// <summary>
        /// Starts from the policies that pay nothing extra.
        /// </summary>
        private static void Use(params policies._value[] selected) =>
            TestPolicies.Use(new[] { social_media_forbidden, streaming_forbidden }.Concat(selected).ToArray());

        private static List<data_girls.girls> Girls(params int[] fameLevels)
        {
            List<data_girls.girls> girls = fameLevels.Select(_ => new data_girls.girls()).ToList();
            data_girls.girl = girls;
            Seams.FameLevel = g => fameLevels[girls.IndexOf(g)];
            return girls;
        }

        /// <summary>
        /// The game's own policy income (Controlled streaming money, No Restrictions fame) still runs.
        /// </summary>
        [Fact]
        public void VanillaPolicyIncome_StillRuns()
        {
            Girls(0);
            Assert.True(data_girls_PoliciesResources.Prefix());
        }

        /// <summary>
        /// No Restrictions streaming pays like vanilla Controlled streaming, ±20%. Fixed in 1.2.0: the
        /// top roll was 119%.
        /// </summary>
        [Theory]
        [InlineData(0, 100, 5000)]
        [InlineData(3, 100, 33500)]
        [InlineData(10, 100, 100000)]
        [InlineData(10, 80, 80000)]
        [InlineData(10, 120, 120000)]
        public void NoRestrictionsStreaming_PaysByFame(int fame, int roll, int expected)
        {
            Use(streaming_no_restrictions);
            Girls(fame);
            Seams.Chance = num => num == 5;
            Seams.Range = (_, _) => roll;

            data_girls_PoliciesResources.Prefix();

            Assert.Equal((80, 121), Assert.Single(Seams.RangesRolled));
            Assert.Equal((resources.type.money, (long)expected), Assert.Single(Seams.ResourcesAdded));
            Assert.Single(Seams.Notifications);
        }

        [Fact]
        public void StreamingMoney_IsPaidOnceForAllIdols()
        {
            Use(streaming_no_restrictions);
            Girls(0, 10);
            Seams.Chance = num => num == 5;
            Seams.Range = (_, _) => 100;

            data_girls_PoliciesResources.Prefix();

            Assert.Equal((resources.type.money, 105000L), Assert.Single(Seams.ResourcesAdded));
            Assert.Single(Seams.Notifications);
        }

        [Theory]
        [InlineData(streaming_forbidden)]
        [InlineData(streaming_controlled)]
        public void OtherStreamingPolicies_PayNothingExtra(policies._value policy)
        {
            Use(policy);
            Girls(10);
            Seams.Chance = _ => true;

            data_girls_PoliciesResources.Prefix();

            Assert.Empty(Seams.ResourcesAdded);
            Assert.DoesNotContain(5, Seams.ChancesRolled);
        }

        /// <summary>
        /// Tooltips: Controlled gains "2 to 22 fans", No Restrictions "10 to 110", depending on fame.
        /// Those are the middle roll; each gain varies from half to 1.5x. Fixed in 1.2.0: the top roll was 1.49x.
        /// </summary>
        [Theory]
        [InlineData(social_media_premoderated, 0, 100, 2)]
        [InlineData(social_media_premoderated, 10, 100, 22)]
        [InlineData(social_media_premoderated, 0, 50, 1)]
        [InlineData(social_media_premoderated, 10, 150, 33)]
        [InlineData(social_media_no_restrictions, 0, 100, 10)]
        [InlineData(social_media_no_restrictions, 10, 100, 110)]
        [InlineData(social_media_no_restrictions, 10, 50, 55)]
        [InlineData(social_media_no_restrictions, 10, 150, 165)]
        public void SocialMedia_GainsFansByFame(policies._value policy, int fame, int roll, int expected)
        {
            Use(policy);
            data_girls.girls girl = Girls(fame)[0];
            Seams.Chance = num => num == 10;
            Seams.Range = (_, _) => roll;

            data_girls_PoliciesResources.Prefix();

            Assert.Equal((50, 151), Assert.Single(Seams.RangesRolled));
            Assert.Equal((girl, (long)expected), Assert.Single(Seams.FansAdded));
            Assert.Contains(expected + "PT", Assert.Single(Seams.Notifications));
        }

        [Fact]
        public void SocialMedia_Forbidden_GainsNoFans()
        {
            Girls(10);
            Seams.Chance = _ => true;

            data_girls_PoliciesResources.Prefix();

            Assert.Empty(Seams.FansAdded);
            Assert.Empty(Seams.Notifications);
        }

        [Fact]
        public void SocialMedia_RollsTenPercent()
        {
            Use(social_media_no_restrictions);
            Girls(10);

            data_girls_PoliciesResources.Prefix();

            Assert.Equal(new[] { 10 }, Seams.ChancesRolled);
            Assert.Empty(Seams.FansAdded);
        }
    }

    public class SecurityTests
    {
        public SecurityTests()
        {
            Seams.Reset();
        }

        public static IEnumerable<object[]> Cases()
        {
            foreach (string item in new[] { "song", "mc" })
            {
                yield return new object[] { item, security_relaxed, 30f, 45f, 2f, 2.5f };
                yield return new object[] { item, security_relaxed, 20f, 30f, 1f, 1.25f };
                yield return new object[] { item, security_restrictive, 30f, 21f, 2f, 1.5f };
                yield return new object[] { item, security_restrictive, 10f, 7f, 1f, 0.75f };
                yield return new object[] { item, security_normal, 30f, 30f, 2f, 2f };
            }
        }

        /// <summary>
        /// Relaxed: 1.5x concert stamina and 1.25x hype. Restrictive: 0.7x stamina and 0.75x hype.
        /// </summary>
        [Theory]
        [MemberData(nameof(Cases))]
        public void Security_ScalesConcertStaminaAndHype(string item, policies._value policy, float stamina, float expectedStamina, float hype, float expectedHype)
        {
            TestPolicies.Use(policy);
            if (item == "song")
            {
                SEvent_Concerts__concert__song_GetStaminaCost.Postfix(ref stamina);
                SEvent_Concerts__concert__song_GetHype.Postfix(ref hype);
            }
            else
            {
                SEvent_Concerts__concert__mc_GetStaminaCost.Postfix(ref stamina);
                SEvent_Concerts__concert__mc_GetHype.Postfix(ref hype);
            }
            Assert.Equal(expectedStamina, stamina);
            Assert.Equal(expectedHype, hype, 5);
        }

        private static singles._param Marketing(int id) => new()
        {
            type = singles._param._type.marketing,
            id = id,
            appeal = new List<singles._param._appeal> { new() { type = resources.fanType.hardcore, val = new List<float> { 10f } } }
        };

        private static readonly MethodInfo GetAppealVal = AccessTools.Method(typeof(singles._param), nameof(singles._param.GetAppealVal));

        private static readonly Lazy<bool> Patched = new(() =>
        {
            // Not "tests.PoliciesThatMatter": PatchTargetTests unpatches everything under that ID
            new Harmony("tests.PoliciesThatMatter.Handshakes").CreateClassProcessor(typeof(singles__param_GetAppealVal)).Patch();
            return true;
        });

        private static float Appeal(int id, resources.fanType fanType = resources.fanType.hardcore)
        {
            _ = Patched.Value;
            return Marketing(id).GetAppealVal(fanType, 1);
        }

        /// <summary>
        /// Vanilla doubles (Relaxed) or halves (Restrictive) handshake appeal; with the mod it's 1.5x and 0.8x.
        /// The game's own method runs, with the mod's change to it.
        /// </summary>
        [Theory]
        [InlineData(1, security_relaxed, 15f)]
        [InlineData(2, security_relaxed, 15f)]
        [InlineData(1, security_restrictive, 8f)]
        [InlineData(2, security_restrictive, 8f)]
        [InlineData(1, security_normal, 10f)]
        public void Handshakes_UseTheModsMultipliers(int id, policies._value policy, float expected)
        {
            TestPolicies.Use(policy);
            Assert.Equal(expected, Appeal(id), 5);
        }

        [Fact]
        public void OtherMarketing_KeepsItsAppeal()
        {
            TestPolicies.Use(security_relaxed);
            Assert.Equal(10f, Appeal(3));
        }

        [Fact]
        public void FansTheHandshakeDoesntAppealTo_AreUntouched()
        {
            TestPolicies.Use(security_relaxed);
            Assert.Equal(0f, Appeal(1, resources.fanType.casual));
        }

        /// <summary>
        /// Only the game's two handshake multipliers change: 2 becomes 1.5 and 0.5 becomes 0.8.
        /// </summary>
        [Fact]
        public void Transpiler_ReplacesOnlyTheTwoMultipliers()
        {
            List<CodeInstruction> game = PatchProcessor.GetOriginalInstructions(GetAppealVal);
            List<CodeInstruction> mod = singles__param_GetAppealVal.Transpiler(game).ToList();

            Assert.Equal(game.Count, mod.Count);
            List<int> changed = Enumerable.Range(0, game.Count).Where(i => !Equals(game[i].operand, mod[i].operand) || game[i].opcode != mod[i].opcode).ToList();
            Assert.Equal(2, changed.Count);
            Assert.Equal((2f, 1.5f), ((float)game[changed[0]].operand, (float)mod[changed[0]].operand));
            Assert.Equal((0.5f, 0.8f), ((float)game[changed[1]].operand, (float)mod[changed[1]].operand));
        }

        /// <summary>
        /// On code that doesn't match, as after a game update, the code is left as it was and the log says so.
        /// </summary>
        [Fact]
        public void Transpilers_NotFound_LeaveTheCodeAndLog()
        {
            UnityEngine.Debug.unityLogger.logHandler = new Log();
            Log.Messages.Clear();
            List<CodeInstruction> other = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(policies), nameof(policies.GetSelectedPolicyValue)));

            Assert.Same(other, singles__param_GetAppealVal.Transpiler(other));
            Assert.Same(other, agency__room_DoGirlTraining.Transpiler(other));
            Assert.Equal(2, Log.Messages.Count);
            Assert.All(Log.Messages, m => Assert.StartsWith("[Policies That Matter] Couldn't find", m));
        }
    }

    /// <summary>
    /// Unity's logger writes through native code; the transpilers log when they can't find their place.
    /// </summary>
    public class Log : UnityEngine.ILogHandler
    {
        public static readonly List<string> Messages = new();

        public void LogFormat(UnityEngine.LogType logType, UnityEngine.Object context, string format, params object[] args) =>
            Messages.Add(string.Format(format, args));

        public void LogException(Exception exception, UnityEngine.Object context) =>
            Messages.Add(exception.ToString());
    }
}
