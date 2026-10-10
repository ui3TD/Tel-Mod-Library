using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static resources.fanType;
using static Single_Marketing_Roll._result;
using static singles._param._special_type;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A PV scales the sales and new fans of the fan types it targets: the main type by the main
    /// coefficient, otherwise the secondary type by the secondary one. The game checked the wrong fan field
    /// for Edgy and Artsy (gender == teen, ...), so they changed nothing. The targets are the ones the
    /// marketing spinner shows.
    /// </summary>
    public class MarketingTests
    {
        public MarketingTests() => TestGame.Reset();

        private const float Main = 2f;
        private const float Secondary = 3f;
        private const long Sales = 100;

        /// <summary>
        /// Every fan segment: 2 genders × 2 hardcoreness × 3 ages.
        /// </summary>
        private static IEnumerable<resources._fan> Segments() =>
            from gender in new[] { male, female }
            from hardcoreness in new[] { casual, hardcore }
            from age in new[] { teen, youngAdult, adult }
            select new resources._fan { gender = gender, hardcoreness = hardcoreness, age = age };

        private static bool Is(resources._fan fan, resources.fanType type) =>
            fan.gender == type || fan.hardcoreness == type || fan.age == type;

        // ValueAfterMarketing scales both a segment's sales and its new fans.
        private static long Run(singles._param._special_type pv, Single_Marketing_Roll._result result, resources._fan fan) =>
            (long)TestGame.CallPrivate(TestGame.Component<singles>(), typeof(singles), nameof(singles.ValueAfterMarketing),
                Sales, Main, Secondary, fan, pv, result);

        [Theory]
        [InlineData(edgy_pv, success, teen, female)]
        [InlineData(edgy_pv, success_crit, teen, female)]
        [InlineData(edgy_pv, fail, adult, youngAdult)]
        [InlineData(edgy_pv, fail_crit, adult, youngAdult)]
        [InlineData(artsy_pv, success, adult, female)]
        [InlineData(artsy_pv, success_crit, adult, female)]
        [InlineData(artsy_pv, fail, teen, male)]
        [InlineData(artsy_pv, fail_crit, teen, male)]
        [InlineData(lewd_pv, success, male, hardcore)]
        [InlineData(lewd_pv, success_crit, male, hardcore)]
        [InlineData(lewd_pv, fail, female, casual)]
        [InlineData(lewd_pv, fail_crit, female, casual)]
        public void Pv_ScalesItsTargets(singles._param._special_type pv, Single_Marketing_Roll._result result,
            resources.fanType mainTarget, resources.fanType secondaryTarget)
        {
            foreach (resources._fan fan in Segments())
            {
                float coeff = Is(fan, mainTarget) ? Main : Is(fan, secondaryTarget) ? Secondary : 1f;
                Assert.True((long)(Sales * coeff) == Run(pv, result, fan),
                    $"{pv} {result}, {fan.gender}/{fan.hardcoreness}/{fan.age}: expected ×{coeff}, got {Run(pv, result, fan)}");
            }
        }

        [Fact]
        public void AdCampaign_ScalesEveryone()
        {
            Assert.All(Segments(), fan => Assert.Equal((long)(Sales * Main), Run(ad_campaign, success, fan)));
        }

        private static MethodInfo Original => AccessTools.Method(typeof(singles), nameof(singles.ValueAfterMarketing));

        private static List<(FieldInfo field, CodeInstruction compare)> FanChecks(IEnumerable<CodeInstruction> codes)
        {
            List<CodeInstruction> list = codes.ToList();
            return list.Take(list.Count - 1)
                .Select((ci, i) => (ci, next: list[i + 1]))
                .Where(p => p.ci.opcode == OpCodes.Ldfld && p.ci.operand is FieldInfo f && f.DeclaringType == typeof(resources._fan))
                .Select(p => ((FieldInfo)p.ci.operand, p.next))
                .ToList();
        }

        [Fact]
        public void Transpiler_ChangesOnlyTheEightWrongFields()
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(Original);
            List<CodeInstruction> patched = singles_ValueAfterMarketing.Transpiler(PatchProcessor.GetOriginalInstructions(Original)).ToList();

            Assert.Equal(original.Count, patched.Count);
            int changed = original.Zip(patched, (a, b) => Equals(a.operand, b.operand) ? 0 : 1).Sum();
            Assert.Equal(singles_ValueAfterMarketing.ExpectedFixes, changed);

            // Lewd's gender == male compiles to ldfld gender; brtrue, and stays
            Assert.Contains(FanChecks(patched), c => c.field.Name == nameof(resources._fan.gender) && (c.compare.opcode == OpCodes.Brtrue || c.compare.opcode == OpCodes.Brtrue_S));
            Assert.Empty(Log.Messages);
        }

        [Fact]
        public void Transpiler_TwiceChangesNothing()
        {
            List<CodeInstruction> once = singles_ValueAfterMarketing.Transpiler(PatchProcessor.GetOriginalInstructions(Original)).ToList();
            List<CodeInstruction> twice = singles_ValueAfterMarketing.Transpiler(once.Select(ci => ci.Clone()).ToList()).ToList();

            Assert.Equal(once.Select(ci => ci.operand), twice.Select(ci => ci.operand));
            Assert.Empty(Log.Messages);
        }

        [Fact]
        public void Transpiler_UnexpectedCode_WarnsAndLeavesItUnchanged()
        {
            // Edgy's success check already reads age (fan.age == teen): 7 wrong fields left
            List<CodeInstruction> codes = PatchProcessor.GetOriginalInstructions(Original);
            (FieldInfo field, CodeInstruction compare) first = FanChecks(codes)
                .First(c => c.field.Name == nameof(resources._fan.gender) && c.compare.opcode == OpCodes.Ldc_I4_4);
            codes.First(ci => ci.opcode == OpCodes.Ldfld && codes[codes.IndexOf(ci) + 1] == first.compare).operand =
                AccessTools.Field(typeof(resources._fan), nameof(resources._fan.age));
            List<object> before = codes.Select(ci => ci.operand).ToList();

            List<CodeInstruction> result = singles_ValueAfterMarketing.Transpiler(codes).ToList();

            Assert.Equal(before, result.Select(ci => ci.operand));
            Assert.Single(Log.Of(UnityEngine.LogType.Warning));
        }
    }
}
