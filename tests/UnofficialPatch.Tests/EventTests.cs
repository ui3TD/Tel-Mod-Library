using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// Event and dialogue requirements on an idol: "influence" checks Influence, and "variable" honours "!".
    /// </summary>
    public class IdolRequirementTests
    {
        public IdolRequirementTests() => TestGame.Reset();

        private static readonly MethodInfo CheckGirl = AccessTools.Method(typeof(vn_requirements), nameof(vn_requirements.CheckGirl), new[] { typeof(data_girls.girls), typeof(string), typeof(string) });
        private static readonly MethodInfo CheckRelationship = AccessTools.Method(typeof(vn_requirements), "CheckRelationship", new[] { typeof(data_girls.girls), typeof(string), typeof(Relationships_Player._type) });

        /// <summary>
        /// The relationship type CheckGirl passes for this parameter: follows the switch's comparison for the
        /// parameter to its case, then reads the constant pushed just before the CheckRelationship call.
        /// </summary>
        private static Relationships_Player._type CheckedType(List<CodeInstruction> code, string parameter)
        {
            int marker = code.FindIndex(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == parameter);
            Assert.True(marker >= 0, parameter + " not found");
            int branch = code.FindIndex(marker, ci => ci.opcode == OpCodes.Brtrue || ci.opcode == OpCodes.Brtrue_S || ci.opcode == OpCodes.Brfalse || ci.opcode == OpCodes.Brfalse_S);
            // The game's compiler wrote the switch as a chain of ifs: "brfalse next" with the case right after.
            // A jump table would branch to the case instead.
            int caseStart = code[branch].opcode == OpCodes.Brfalse || code[branch].opcode == OpCodes.Brfalse_S
                ? branch + 1
                : code.FindIndex(ci => ci.labels.Contains((Label)code[branch].operand));
            int call = code.FindIndex(caseStart, ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && Equals(ci.operand, CheckRelationship));
            return (Relationships_Player._type)Constant(code[call - 1]);
        }

        private static int Constant(CodeInstruction ci)
        {
            if (ci.opcode == OpCodes.Ldc_I4_1) return 1;
            if (ci.opcode == OpCodes.Ldc_I4_2) return 2;
            if (ci.opcode == OpCodes.Ldc_I4_3) return 3;
            if (ci.opcode == OpCodes.Ldc_I4_S) return (sbyte)ci.operand;
            if (ci.opcode == OpCodes.Ldc_I4) return (int)ci.operand;
            Assert.Fail("not an int constant: " + ci);
            return 0;
        }

        [Fact]
        public void Game_ChecksFriendshipForInfluence()
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(CheckGirl);
            Assert.Equal(Relationships_Player._type.Friendship, CheckedType(original, "influence"));
        }

        [Theory]
        [InlineData("friendship", Relationships_Player._type.Friendship)]
        [InlineData("influence", Relationships_Player._type.Influence)]
        [InlineData("romance", Relationships_Player._type.Romance)]
        public void Patched_ChecksTheMatchingRelationship(string parameter, Relationships_Player._type expected)
        {
            // CheckGirl reads the camera, so it can't run outside the game; check the transpiled code instead
            List<CodeInstruction> patched = vn_requirements_CheckGirl.Transpiler(PatchProcessor.GetOriginalInstructions(CheckGirl)).ToList();
            Assert.Equal(expected, CheckedType(patched, parameter));
            Assert.Empty(Log.Messages);
        }

        [Fact]
        public void Patched_TranspilingTwiceChangesNothing()
        {
            List<CodeInstruction> once = vn_requirements_CheckGirl.Transpiler(PatchProcessor.GetOriginalInstructions(CheckGirl)).ToList();
            List<CodeInstruction> twice = vn_requirements_CheckGirl.Transpiler(once).ToList();
            Assert.Equal(Relationships_Player._type.Influence, CheckedType(twice, "influence"));
            Assert.Empty(Log.Messages);
        }

        /// <summary>
        /// The game's "variable" case, copied as it is in CheckGirl (same arguments, same code), because
        /// CheckGirl reads the camera and can't run here. The tests patch it with the mod's transpiler.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool GameVariableCase(data_girls.girls girl, string parameter, string formula)
        {
            bool flag = false;
            if (formula.Substring(0, 1) == "!")
            {
                flag = true;
                formula.Substring(1);
            }
            if (flag)
            {
                return !girl.IsVariable(formula);
            }
            return girl.IsVariable(formula);
        }

        private static readonly MethodInfo VariableCase = AccessTools.Method(typeof(IdolRequirementTests), nameof(GameVariableCase));
        private static readonly MethodInfo Substring = AccessTools.Method(typeof(string), nameof(string.Substring), new[] { typeof(int) });

        private static bool Variable(data_girls.girls girl, string formula, bool patched)
        {
            // Not "tests.UnofficialPatch": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.UnofficialPatch.VariableCase");
            if (patched)
                harmony.Patch(VariableCase, transpiler: new HarmonyMethod(typeof(vn_requirements_CheckGirl_Variable), nameof(vn_requirements_CheckGirl_Variable.Transpiler)));
            try
            {
                return (bool)VariableCase.Invoke(null, new object[] { girl, "variable", formula });
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }

        [Theory]
        [InlineData("met_fan", true)]
        [InlineData("MET_FAN", true)]
        [InlineData("other", false)]
        [InlineData("!met_fan", false)]
        [InlineData("!other", true)]
        public void Variable_HonoursNegation(string formula, bool expected)
        {
            data_girls.girls girl = TestGame.Idol();
            girl.Variables.Add("met_fan");

            Assert.Equal(expected, Variable(girl, formula, patched: true));
        }

        /// <summary>
        /// The game dropped the "!", so "!met_fan" checked a variable named "!met_fan" and was always true.
        /// </summary>
        [Fact]
        public void Game_IgnoresNegation()
        {
            data_girls.girls girl = TestGame.Idol();
            girl.Variables.Add("met_fan");

            Assert.True(Variable(girl, "!met_fan", patched: false));
        }

        /// <summary>
        /// In the real CheckGirl, the mod stores formula.Substring(1) back into formula (argument 2) and
        /// changes nothing else.
        /// </summary>
        [Fact]
        public void Patched_CheckGirlKeepsTheVariableName()
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(CheckGirl);
            List<CodeInstruction> patched = vn_requirements_CheckGirl_Variable.Transpiler(PatchProcessor.GetOriginalInstructions(CheckGirl)).ToList();

            int call = patched.FindIndex(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && Equals(ci.operand, Substring));
            Assert.True(call > 0, "Substring(1) not found");
            Assert.Equal(OpCodes.Pop, original[call + 1].opcode);
            Assert.Equal(OpCodes.Starg_S, patched[call + 1].opcode);
            Assert.Equal(2, Convert.ToInt32(patched[call + 1].operand));
            Assert.Equal(original.Count, patched.Count);
            Assert.Equal(1, Enumerable.Range(0, original.Count).Count(i => original[i].ToString() != patched[i].ToString()));
        }

        [Fact]
        public void Patched_VariableTranspilingTwiceChangesNothing()
        {
            List<CodeInstruction> once = vn_requirements_CheckGirl_Variable.Transpiler(PatchProcessor.GetOriginalInstructions(CheckGirl)).ToList();
            List<CodeInstruction> twice = vn_requirements_CheckGirl_Variable.Transpiler(once).ToList();

            Assert.Equal(once.Select(ci => ci.ToString()), twice.Select(ci => ci.ToString()));
            Assert.Empty(Log.Messages);
        }
    }

    /// <summary>
    /// "staff" requirements are met by a staffer of the right kind. The game nested the checks, so only
    /// "doctor" ever worked.
    /// </summary>
    public class StaffRequirementTests
    {
        public StaffRequirementTests() => TestGame.Reset();

        private static bool Check(string parameter, string formula, bool vanilla = false)
        {
            bool result = vanilla;
            vn_requirements_CheckMeta.Postfix(parameter, formula, ref result);
            return result;
        }

        [Theory]
        [InlineData("vocal", staff._type.voice_coach)]
        [InlineData("vocal", staff._type.music_producer)]
        [InlineData("dance", staff._type.dance_coach)]
        [InlineData("dance", staff._type.choreographer)]
        [InlineData("office", staff._type.production_manager)]
        [InlineData("office", staff._type.sales_manager)]
        [InlineData("style", staff._type.stylist_cute_pretty)]
        [InlineData("style", staff._type.stylist_cool_sexy)]
        public void Staffer_MeetsTheirRequirement(string formula, staff._type staffer)
        {
            staff.Staff.Add(new staff._staff { type = staffer });
            Assert.True(Check("staff", formula));
        }

        [Theory]
        [InlineData("vocal")]
        [InlineData("dance")]
        [InlineData("office")]
        [InlineData("style")]
        public void NoStaffer_DoesNotMeetIt(string formula)
        {
            staff.Staff.Add(new staff._staff { type = staff._type.physician });
            Assert.False(Check("staff", formula));
        }

        [Fact]
        public void WrongKindOfStaffer_DoesNotMeetIt()
        {
            staff.Staff.Add(new staff._staff { type = staff._type.voice_coach });
            Assert.False(Check("staff", "dance"));
        }

        [Fact]
        public void OtherRequirements_KeepTheGamesResult()
        {
            staff.Staff.Add(new staff._staff { type = staff._type.voice_coach });
            Assert.True(Check("staff", "doctor", vanilla: true));
            Assert.False(Check("staff", "doctor"));
            Assert.False(Check("route", "vocal"));
        }
    }

    /// <summary>
    /// The Performance activity's description shows the stamina it actually costs.
    /// </summary>
    public class ActivityDescriptionTests
    {
        public ActivityDescriptionTests()
        {
            TestGame.Reset();
            Language.Data["PT"] = "PT";
            Language.Data["STAMINA"] = "Stamina";
        }

        private static string Description(Activity._type type, string vanilla)
        {
            Activities._activity activity = new() { type = type, lvl = 1 };
            string result = vanilla;
            Activities__activity_GetDescription.Postfix(activity, ref result);
            return result;
        }

        private static float StaminaCost() => TestGame.Component<Activities>().GetStaminaCost();

        [Fact]
        public void Energetic_ShowsFour()
        {
            TestGame.SetPolicies(policies._value.performances_energy);
            Assert.Equal("-4PT stamina", Description(Activity._type.performance, "-3PT stamina"));
            Assert.Equal(-4f, StaminaCost());
        }

        [Theory]
        [InlineData(policies._value.performances_neutral)]
        [InlineData(policies._value.performances_quality)]
        public void OtherPolicies_KeepTheGamesThree(policies._value policy)
        {
            TestGame.SetPolicies(policy);
            Assert.Equal("-3PT stamina", Description(Activity._type.performance, "-3PT stamina"));
            Assert.Equal(-3f, StaminaCost());
        }

        [Fact]
        public void OtherActivities_AreUnchanged()
        {
            TestGame.SetPolicies(policies._value.performances_energy);
            Assert.Equal("game text", Description(Activity._type.promotion, "game text"));
        }
    }
}
