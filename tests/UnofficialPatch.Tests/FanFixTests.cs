using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static resources.fanType;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// Idols with the game's 12 fan segments, each holding this many fans.
    /// </summary>
    internal static class FanIdols
    {
        public static data_girls.girls WithFans(long perSegment, float famePoints = 100f)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol());
            girl.getParam(data_girls._paramType.famePoints)._val = famePoints;
            girl.CreateFans();
            foreach (resources._fan fan in girl.Fans)
                fan.people = perSegment;
            return girl;
        }

        public static long Fans(data_girls.girls girl, resources.fanType? type = null) =>
            girl.Fans.Where(fan => fan.IsType(type)).Sum(fan => fan.people);

        /// <summary>
        /// A fan's recorded opinion, as text so lists and NaN ratios compare by value.
        /// </summary>
        public static string Opinion(resources._fan fan) => $"[{string.Join(",", fan.Vals)}] temp {fan.Temp} ratio {fan.Ratio}";

        public static List<CodeInstruction> Original(MethodBase method) => PatchProcessor.GetOriginalInstructions(method);

        /// <summary>
        /// The transpiler's result, as text, run on the game's code and then on its own output.
        /// </summary>
        public static (List<string> once, List<string> twice) Twice(MethodBase method, Func<IEnumerable<CodeInstruction>, ILGenerator, IEnumerable<CodeInstruction>> transpiler)
        {
            ILGenerator generator = new DynamicMethod("test", typeof(void), Type.EmptyTypes).GetILGenerator();
            List<CodeInstruction> once = transpiler(Original(method), generator).ToList();
            // As text before the second run, which may change the same instructions
            List<string> onceText = once.Select(ci => ci.ToString()).ToList();
            List<CodeInstruction> twice = transpiler(once, generator).ToList();
            return (onceText, twice.Select(ci => ci.ToString()).ToList());
        }
    }

    /// <summary>
    /// Fans of one type (business deals, cafes, fans switching idols) land in that type's segments, and none are lost.
    /// </summary>
    public class TypedFansTests
    {
        public TypedFansTests()
        {
            TestGame.Reset();
            Seams.Range = (min, max) => min;
        }

        /// <summary>
        /// The game gave the male segments their share of all 12 weights, about half, and spread the rest over every
        /// segment: 75% of 1,500 male fans were male in game. Only the rounding remainder (under 6) can land elsewhere.
        /// </summary>
        [Fact]
        public void TypedGain_LandsInItsType()
        {
            data_girls.girls girl = FanIdols.WithFans(0);

            girl.AddFans(1500, male);

            Assert.Equal(1500, FanIdols.Fans(girl));
            Assert.InRange(FanIdols.Fans(girl, male), 1500 - 6, 1500);
        }

        /// <summary>
        /// The game handed out the remainder for at most 1,000 rounds of 12 fans: 100,000 hardcore fans lost 37,741.
        /// </summary>
        [Fact]
        public void LargeTypedGain_LosesNothing()
        {
            data_girls.girls girl = FanIdols.WithFans(0);

            girl.AddFans(100000, hardcore);

            Assert.Equal(100000, FanIdols.Fans(girl));
            Assert.InRange(FanIdols.Fans(girl, hardcore), 100000 - 6, 100000);
        }

        [Fact]
        public void UntypedFans_KeepTheGamesTotal()
        {
            data_girls.girls girl = FanIdols.WithFans(0);
            girl.RecalcFanAppeal();

            Assert.Equal(girl.GetAppeal_Total(), data_girls_girls_AddFans_Typed.MatchingTotal(girl.GetAppeal_Total(), girl, null));
        }

        /// <summary>
        /// Segments of the type that weigh nothing keep the game's total, so the game's split decides.
        /// </summary>
        [Fact]
        public void WeightlessType_KeepsTheGamesTotal()
        {
            data_girls.girls girl = FanIdols.WithFans(0);
            foreach (resources._fan fan in girl.Fans)
            {
                fan.Ratio = 0f;
                fan.appeal = 0f;
            }

            Assert.Equal(7f, data_girls_girls_AddFans_Typed.MatchingTotal(7f, girl, male));
        }

        [Fact]
        public void Transpiler_AddsTheHelperOnce()
        {
            MethodInfo method = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.AddFans), new[] { typeof(long), typeof(resources.fanType?) });
            (List<string> once, List<string> twice) = FanIdols.Twice(method, (ci, _) => data_girls_girls_AddFans_Typed.Transpiler(ci));

            Assert.Equal(FanIdols.Original(method).Count + 3, once.Count);
            Assert.Single(once, ci => ci.Contains(nameof(data_girls_girls_AddFans_Typed.MatchingTotal)));
            Assert.Equal(once, twice);
        }
    }

    /// <summary>
    /// Fans who switch idols after a scandal or event are moved once.
    /// </summary>
    public class SwitchingFansTests
    {
        private readonly data_girls.girls scandal;
        private readonly data_girls.girls other;

        /// <summary>
        /// Normal mode, so the loss isn't multiplied; the lowest switch shares: 5% of casual fans and 50% of
        /// hardcore fans switch. Each segment of the scandal idol holds 1,000 fans, so -2 takes 200 from each.
        /// </summary>
        public SwitchingFansTests()
        {
            TestGame.Reset();
            Seams.Range = (min, max) => min;
            scandal = FanIdols.WithFans(1000);
            other = FanIdols.WithFans(0);
        }

        /// <summary>
        /// The dating scandal's two calls: 1,200 hardcore fans lost, 600 switch; 1,200 casual lost, 60 switch.
        /// The game moved the 600 hardcore switchers again with the casual call: 1,260 in all.
        /// </summary>
        [Fact]
        public void HardcoreThenCasual_MovesEachSwitcherOnce()
        {
            scandal.AddFans(hardcore, -2f, oshihen: true);
            scandal.AddFans(casual, -2f, oshihen: true);

            Assert.Equal(600 + 60, FanIdols.Fans(other));
            Assert.Equal(12000 - 2400, FanIdols.Fans(scandal));
            // What the scandal's result text shows is left as the game counts it
            Assert.Equal(new long[] { 600, 600, 60, 1140 },
                new[] { scandal.Fans_Oshihened_Hardcore, scandal.Fans_Quit_Hardcore, scandal.Fans_Oshihened_Casual, scandal.Fans_Quit_Casual });
        }

        /// <summary>
        /// The counters aren't reset by a call for the other type, so the game moved them again later.
        /// </summary>
        [Fact]
        public void LaterCall_DoesntMoveEarlierSwitchers()
        {
            scandal.AddFans(hardcore, -2f, oshihen: true);
            long afterScandal = FanIdols.Fans(other);

            scandal.AddFans(casual, -1f, oshihen: true);

            Assert.Equal(600, afterScandal);
            Assert.Equal(600 + 30, FanIdols.Fans(other));
        }

        [Fact]
        public void WithoutSwitching_NobodyMoves()
        {
            scandal.AddFans(hardcore, -2f);

            Assert.Equal(0, FanIdols.Fans(other));
        }

        [Fact]
        public void Transpiler_ChangesBothMovesOnce()
        {
            MethodInfo method = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.AddFans), new[] { typeof(resources.fanType?), typeof(float), typeof(bool) });
            (List<string> once, List<string> twice) = FanIdols.Twice(method, data_girls_girls_AddFans_Oshihen.Transpiler);

            // 6 to keep the counters, 2 for each move
            Assert.Equal(FanIdols.Original(method).Count + 6 + 4, once.Count);
            Assert.Equal(once, twice);
        }
    }

    /// <summary>
    /// Every idol in a show cast with the entire group gets the same opinion vote: a quarter of the episode's.
    /// </summary>
    public class EntireGroupShowTests
    {
        public EntireGroupShowTests() => TestGame.Reset();

        private static Shows._show Show(int episode)
        {
            Shows._show show = new() { castType = Shows._show._castType.entireGroup, episodeCount = episode };
            float ratio = 0f;
            foreach (resources.fanType type in Enum.GetValues(typeof(resources.fanType)))
                show.FanAppeal.Add(new singles._fanAppeal { type = type, ratio = ratio++ });
            return show;
        }

        /// <summary>
        /// Male is the bottom type and adult the top: every idol gets -1/4 male and +1/4 adult on episode 1.
        /// The game gave 1/4, 1/16, 1/64 in roster order.
        /// </summary>
        [Theory]
        [InlineData(1, 0.25f)]
        [InlineData(2, 0.0625f)]
        public void EveryIdol_GetsAQuarter(int episode, float vote)
        {
            List<data_girls.girls> cast = Enumerable.Range(0, 4).Select(_ => FanIdols.WithFans(0)).ToList();
            data_girls.girls expected = FanIdols.WithFans(0);
            expected.AddAppeal(male, -vote);
            expected.AddAppeal(adult, vote);
            data_girls.girl.Remove(expected);

            TestGame.CallPrivate(Show(episode), typeof(Shows._show), "SetOpinion");

            Assert.All(cast, girl => Assert.Equal(expected.Fans.Select(FanIdols.Opinion), girl.Fans.Select(FanIdols.Opinion)));
        }

        [Fact]
        public void PermanentCast_GetsTheWholeVote()
        {
            data_girls.girls girl = FanIdols.WithFans(0);
            data_girls.girls expected = FanIdols.WithFans(0);
            expected.AddAppeal(male, -1f);
            expected.AddAppeal(adult, 1f);
            Shows._show show = Show(1);
            show.castType = Shows._show._castType.permanentCast;
            show.girls = new[] { girl };

            TestGame.CallPrivate(show, typeof(Shows._show), "SetOpinion");

            Assert.Equal(expected.Fans.Select(FanIdols.Opinion), girl.Fans.Select(FanIdols.Opinion));
        }

        [Fact]
        public void Transpiler_ChangesTheDivisionOnce()
        {
            MethodInfo method = AccessTools.Method(typeof(Shows._show), "SetOpinion");
            (List<string> once, List<string> twice) = FanIdols.Twice(method, Shows__show_SetOpinion.Transpiler);

            Assert.Equal(FanIdols.Original(method).Count + 2, once.Count);
            Assert.Equal(once, twice);
        }
    }

    /// <summary>
    /// A single's opinion votes follow its song quality for the genre, as its sales do, and the first single's
    /// votes follow its own strongest and weakest fan types.
    /// </summary>
    public class SingleOpinionTests
    {
        public SingleOpinionTests() => TestGame.Reset();

        /// <summary>
        /// A single whose genre appeals to each fan type by these values (male, female, casual, hardcore, teen,
        /// young adult, adult), with this song quality.
        /// </summary>
        private static singles._single Single(float song, params float[] genre)
        {
            singles._single single = new();
            // The 15 senbatsu slots, empty
            single.girls.AddRange(new data_girls.girls[15]);
            single.parameters.Add(new singles._single._param { type = singles._single._param._type.song, val = song });
            single.parameters.Add(new singles._single._param { type = singles._single._param._type.lyrics, val = 50f });
            single.parameters.Add(new singles._single._param { type = singles._single._param._type.choreography, val = 50f });
            single.genre = new singles._param();
            resources.fanType[] types = (resources.fanType[])Enum.GetValues(typeof(resources.fanType));
            for (int i = 0; i < genre.Length; i++)
                single.genre.appeal.Add(new singles._param._appeal { type = types[i], val = Enumerable.Repeat(genre[i], 10).ToList() });
            return single;
        }

        private static float Appeal(singles._single single, resources.fanType type) =>
            single.GetAppealForOpinion().First(appeal => appeal.type == type).ratio;

        /// <summary>
        /// The song's quality modifier is 0.5 at 0 and 2 at 100, as for sales: a genre appeal of 10 is 5 or 20.
        /// The game ignored it.
        /// </summary>
        [Fact]
        public void SongQuality_ScalesTheGenre()
        {
            float weak = Appeal(Single(0f, 10f), male);
            float strong = Appeal(Single(100f, 10f), male);

            Assert.Equal(20f - 5f, strong - weak, 3);
        }

        [Fact]
        public void Transpiler_UsesTheSongQualityOnce()
        {
            MethodInfo method = AccessTools.Method(typeof(singles._single), nameof(singles._single.GetAppealForOpinion));
            (List<string> once, List<string> twice) = FanIdols.Twice(method, singles__single_GetAppealForOpinion.Transpiler);

            Assert.Equal(FanIdols.Original(method).Count + 2, once.Count);
            Assert.DoesNotContain(once, ci => ci == "pop");
            Assert.Equal(once, twice);
        }

        /// <summary>
        /// The first single's votes: +1 for its two strongest types (casual and hardcore here), -1 for its two
        /// weakest (male and female). The game always voted +male +female -young adult -adult.
        /// </summary>
        [Fact]
        public void FirstSingle_VotesForItsOwnStrongestAndWeakest()
        {
            data_girls.girls girl = FanIdols.WithFans(0);
            data_girls.girls expected = FanIdols.WithFans(0);
            expected.AddAppeal(casual, 1f);
            expected.AddAppeal(hardcore, 1f);
            expected.AddAppeal(male, -1f);
            expected.AddAppeal(female, -1f);
            singles._single single = Single(50f, 100f, 200f, 700f, 600f, 500f, 400f, 300f);
            single.girls[0] = girl;

            TestGame.CallPrivate(TestGame.Component<singles>(), typeof(singles), nameof(singles.AddOpinion), single);

            Assert.Equal(expected.Fans.Select(FanIdols.Opinion), girl.Fans.Select(FanIdols.Opinion));
        }

        [Fact]
        public void Transpiler_ReadsOnlyTheSortedList()
        {
            MethodInfo method = AccessTools.Method(typeof(singles), nameof(singles.AddOpinion));
            (List<string> once, List<string> twice) = FanIdols.Twice(method, (ci, _) => singles_AddOpinion.Transpiler(ci));

            Assert.Equal(6, once.Count(ci => ci.Contains(nameof(singles_AddOpinion.Sorted))));
            Assert.DoesNotContain(once, ci => ci.StartsWith("ldfld") && ci.Contains("_single::FanAppeal"));
            Assert.Equal(once, twice);
        }
    }

    /// <summary>
    /// Without Fan Attrition, the fans tooltip shows a segment's real opinion on the idol profile's scale.
    /// The tooltip draws through Unity, so the helpers and the transpiled code are checked directly.
    /// </summary>
    public class FansTooltipTests
    {
        private static tooltip_fans_line Line(float positive, float negative)
        {
            tooltip_fans_line line = TestGame.Component<tooltip_fans_line>();
            AccessTools.Field(typeof(tooltip_fans_line), nameof(tooltip_fans_line.Positive)).SetValue(line, positive);
            AccessTools.Field(typeof(tooltip_fans_line), nameof(tooltip_fans_line.Negative)).SetValue(line, negative);
            return line;
        }

        /// <summary>
        /// The game showed 50% unless there were both positive and negative votes.
        /// </summary>
        [Theory]
        [InlineData(5f, 0f, 1f)]
        [InlineData(0f, 5f, 0f)]
        [InlineData(3f, 1f, 0.75f)]
        public void Share_IsPositiveOverAllVotes(float positive, float negative, float expected)
        {
            Assert.Equal(expected, tooltip_fans_line_Set.Ratio(0.5f, Line(positive, negative)));
        }

        [Fact]
        public void NoVotes_KeepTheGamesValue()
        {
            Assert.Equal(0.5f, tooltip_fans_line_Set.Ratio(0.5f, Line(0f, 0f)));
        }

        [Theory]
        [InlineData(1f, 100)]
        [InlineData(0.5f, 0)]
        [InlineData(0f, -100)]
        [InlineData(0.75f, 50)]
        public void Shown_OnTheProfilesScale(float ratio, int expected)
        {
            Assert.Equal(expected, tooltip_fans_line_Set.Shown(ratio));
        }

        [Fact]
        public void Transpiler_ChangesTheOpinionOnly_AndOnlyOnce()
        {
            MethodInfo method = AccessTools.Method(typeof(tooltip_fans_line), nameof(tooltip_fans_line.Set), new[] { typeof(resources._fan) });
            (List<string> once, List<string> twice) = FanIdols.Twice(method, (ci, _) => tooltip_fans_line_Set.Transpiler(ci));

            Assert.Equal(FanIdols.Original(method).Count + 4, once.Count);
            Assert.Single(once, ci => ci.Contains(nameof(tooltip_fans_line_Set.Shown)));
            // The appeal column keeps the game's percent
            Assert.Single(once, ci => ci.Contains(nameof(ExtensionMethods.toPercent)));
            Assert.Equal(once, twice);
        }
    }

    /// <summary>
    /// Dating scandal responses cost the idol the mental stamina the game lists, except Thick Skin and Shameless idols.
    /// </summary>
    public class DatingMentalDamageTests
    {
        public DatingMentalDamageTests()
        {
            TestGame.Reset();
            Language.Data["MENTAL_STAMINA"] = "Mental stamina";
            Language.Data["PT"] = "pt";
        }

        private static string Damage(data_girls.girls girl)
        {
            Event_Templates._active_template template = new();
            template.Actors.Add(new Event_Manager._activeEvent._actor { girl = girl });
            return (string)TestGame.CallPrivate(template, typeof(Event_Templates._active_template), "MentalDamageDating", -25f, false);
        }

        [Fact]
        public void Idol_LosesMentalStamina()
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol());

            string text = Damage(girl);

            Assert.Contains("-25", text);
            Assert.Equal(new[] { (girl, data_girls._paramType.mentalStamina, -25f) }, Seams.ParamsAdded);
        }

        [Theory]
        [InlineData(traits._trait._type.Thick_Skin)]
        [InlineData(traits._trait._type.Shameless)]
        public void ThickSkinAndShameless_AreSpared(traits._trait._type trait)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol());
            girl.trait = trait;

            Assert.Equal("", Damage(girl));
            Assert.Empty(Seams.ParamsAdded);
        }

        [Fact]
        public void Transpiler_FlipsBothTestsOnce()
        {
            MethodInfo method = AccessTools.Method(typeof(Event_Templates._active_template), "MentalDamageDating");
            (List<string> once, List<string> twice) = FanIdols.Twice(method, (ci, _) => Event_Templates__active_template_MentalDamageDating.Transpiler(ci));

            Assert.Equal(FanIdols.Original(method).Count, once.Count);
            Assert.Equal(once, twice);
        }
    }

    /// <summary>
    /// Code another mod (or a game update) changed so the pattern isn't there is left as it is.
    /// </summary>
    public class FanFixTranspilerTests
    {
        public FanFixTranspilerTests() => TestGame.Reset();

        public static IEnumerable<object[]> Transpilers()
        {
            yield return new object[] { "typed fans", (Func<List<CodeInstruction>, IEnumerable<CodeInstruction>>)(ci => data_girls_girls_AddFans_Typed.Transpiler(ci)) };
            yield return new object[] { "switching fans", (Func<List<CodeInstruction>, IEnumerable<CodeInstruction>>)(ci => data_girls_girls_AddFans_Oshihen.Transpiler(ci, Generator())) };
            yield return new object[] { "show opinion", (Func<List<CodeInstruction>, IEnumerable<CodeInstruction>>)(ci => Shows__show_SetOpinion.Transpiler(ci, Generator())) };
            yield return new object[] { "song quality", (Func<List<CodeInstruction>, IEnumerable<CodeInstruction>>)(ci => singles__single_GetAppealForOpinion.Transpiler(ci, Generator())) };
            yield return new object[] { "first single", (Func<List<CodeInstruction>, IEnumerable<CodeInstruction>>)(ci => singles_AddOpinion.Transpiler(ci)) };
            yield return new object[] { "tooltip", (Func<List<CodeInstruction>, IEnumerable<CodeInstruction>>)(ci => tooltip_fans_line_Set.Transpiler(ci)) };
            yield return new object[] { "mental damage", (Func<List<CodeInstruction>, IEnumerable<CodeInstruction>>)(ci => Event_Templates__active_template_MentalDamageDating.Transpiler(ci)) };
        }

        private static ILGenerator Generator() => new DynamicMethod("test", typeof(void), Type.EmptyTypes).GetILGenerator();

        [Theory]
        [MemberData(nameof(Transpilers))]
        public void UnknownCode_IsLeftAlone(string fix, Func<List<CodeInstruction>, IEnumerable<CodeInstruction>> transpiler)
        {
            List<CodeInstruction> code = new() { new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Pop), new CodeInstruction(OpCodes.Ret) };

            Assert.Equal(code.Select(ci => ci.ToString()), transpiler(code).Select(ci => ci.ToString()));
            Assert.NotNull(fix);
        }
    }

    /// <summary>
    /// A fan segment with no opinion votes reads as neutral, not as not-a-number.
    /// </summary>
    public class NoVotesTests
    {
        public NoVotesTests() => TestGame.Reset();

        [Fact]
        public void NoVotes_AreNeutral()
        {
            resources._fan fan = new();

            fan.Recalc();

            Assert.Equal(0.5f, fan.Ratio);
        }

        [Fact]
        public void Votes_KeepTheGamesRatio()
        {
            resources._fan fan = new();
            fan.Vals.AddRange(new[] { 1, 1, 1, -1 });

            fan.Recalc();

            Assert.Equal(0.75f, fan.Ratio);
        }
    }
}
