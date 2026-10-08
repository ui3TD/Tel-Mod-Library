using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static staticVars._playerData;

namespace FanAttrition.Tests
{
    public class McFameTests
    {
        private static int Run(int? mcFame, int fans)
        {
            Shows._show show = new();
            if (mcFame.HasValue)
                show.mc = new Shows._param { fame = mcFame.Value };
            return Shows__show_SetSales_MC.Infix(show, fans);
        }

        /// <summary>
        /// Show fans scale by 1 + fame²/10, plus 5 at max fame.
        /// </summary>
        [Theory]
        [InlineData(0, 1000)]
        [InlineData(1, 1100)]
        [InlineData(2, 1400)]
        [InlineData(3, 1900)]
        [InlineData(5, 3500)]
        [InlineData(9, 9100)]
        [InlineData(10, 16000)]
        public void FansScaleWithMcFame(int fame, int expected)
        {
            Assert.Equal(expected, Run(fame, 1000));
        }

        [Fact]
        public void NoMc_LeavesFansUnchanged()
        {
            Assert.Equal(1000, Run(null, 1000));
        }
    }

    /// <summary>
    /// The show patches, run inside the game's SetSales: fatigue cuts the audience, and the MC's fame
    /// multiplies the new fans.
    /// </summary>
    public class ShowSalesTests
    {
        public ShowSalesTests() => TestGame.Reset();

        /// <summary>
        /// Airs an episode of a show at fame 12 (agency fame level 2 plus 10 from the show) whose earlier episodes
        /// made 60,000 fans. A TV show's audience is (12 * 0.05 * 500,000 + 60,000) / 12 = 30,000, and a radio or
        /// internet show's is (12 * 0.05 * 100,000 + 60,000) / 12 = 10,000. Its one fan group's appeal ratios
        /// sum to 1 and the audience roll gives 100%, so that group is the whole audience.
        /// </summary>
        private static Shows._show Air(Shows._param._media_type medium, float fatigue, int? mcFame = null)
        {
            resources.resource[(int)resources.type.fame] = (long)resources.FameLevelToPoints(2);
            resources.Fans.Add(TestGame.Fans(resources.fanType.male, resources.fanType.casual, resources.fanType.teen, 0));

            Shows._show show = new()
            {
                medium = new Shows._param { media_type = medium },
                castType = Shows._show._castType.rotatingCast,
                episodeCount = 5,
            };
            show.fame.Add(10);
            show.fans.Add(60000);
            show.fatigue.Add(fatigue);
            show.FanAppeal.Add(new singles._fanAppeal { type = resources.fanType.male, ratio = 0.5f });
            show.FanAppeal.Add(new singles._fanAppeal { type = resources.fanType.casual, ratio = 0.25f });
            show.FanAppeal.Add(new singles._fanAppeal { type = resources.fanType.teen, ratio = 0.25f });
            if (mcFame.HasValue)
                show.mc = new Shows._param { fame = mcFame.Value };

            // The audience roll is 90-109% for TV and radio, and 100-114% for internet shows
            Seams.Range = (min, max) => medium == Shows._param._media_type.internet ? 0 : 10;
            TestGame.CallPrivate(show, typeof(Shows._show), "SetSales");
            return show;
        }

        private static long Audience(Shows._show show) => show.audience.Last();
        private static int NewFans(Shows._show show) => show.fans.Last();

        /// <summary>
        /// The audience, including the earlier episodes' fans, is multiplied by 1 - fatigue²/20,000 in Normal,
        /// and 1 - fatigue²/12,500 in Unfair.
        /// </summary>
        [Theory]
        [InlineData(_difficulty.normal, 0f, 30000)]
        [InlineData(_difficulty.normal, 50f, 26250)]
        [InlineData(_difficulty.normal, 100f, 15000)]
        [InlineData(_difficulty.hard, 0f, 30000)]
        [InlineData(_difficulty.hard, 50f, 24000)]
        [InlineData(_difficulty.hard, 100f, 6000)]
        [InlineData(_difficulty.easy, 100f, 30000)]
        public void TvAudience_FallsWithFatigueSquared(_difficulty difficulty, float fatigue, long expected)
        {
            TestGame.SetDifficulty(difficulty);
            Assert.Equal(expected, Audience(Air(Shows._param._media_type.tv, fatigue)));
        }

        [Fact]
        public void RadioAudience_FallsWithFatigue()
        {
            Assert.Equal(5000, Audience(Air(Shows._param._media_type.radio, 100f)));
        }

        /// <summary>
        /// Fixed in 1.2.0: internet shows lost audience to fatigue too, though the Steam description has
        /// always said they aren't affected.
        /// </summary>
        [Theory]
        [InlineData(_difficulty.normal)]
        [InlineData(_difficulty.hard)]
        public void InternetAudience_IgnoresFatigue(_difficulty difficulty)
        {
            TestGame.SetDifficulty(difficulty);
            Assert.Equal(10000, Audience(Air(Shows._param._media_type.internet, 100f)));
        }

        /// <summary>
        /// The game gives a show 0.1% of its audience as new fans, less its fatigue.
        /// </summary>
        [Fact]
        public void NoMc_GetsTheGamesNewFans()
        {
            Shows._show show = Air(Shows._param._media_type.tv, 0f);

            Assert.Equal(30, NewFans(show));
            Assert.Equal(30, Assert.Single(Seams.FansAddedEqually).fans);
        }

        [Fact]
        public void McFame_MultipliesNewFans_NotAudience()
        {
            Shows._show show = Air(Shows._param._media_type.tv, 0f, mcFame: 10);

            Assert.Equal(30000, Audience(show));
            Assert.Equal(30 * 16, NewFans(show));
            (resources._fan fan, long fans) added = Assert.Single(Seams.FansAddedEqually);
            Assert.Same(resources.Fans.Single(), added.fan);
            Assert.Equal(30 * 16, added.fans);
        }

        /// <summary>
        /// 26,250 viewers at 50% fatigue in Normal, times 16 for a max-fame MC, of whom 0.1% * 50% become fans.
        /// </summary>
        [Fact]
        public void McFame_StacksWithFatigue()
        {
            Shows._show show = Air(Shows._param._media_type.tv, 50f, mcFame: 10);

            Assert.Equal(26250, Audience(show));
            Assert.Equal(210, NewFans(show));
        }
    }

    /// <summary>
    /// Single PVs succeed as often as the formation's average in their two stats, crit successes are a tenth
    /// of that, and crit success bonuses change.
    /// </summary>
    public class MarketingTests
    {
        public MarketingTests() => TestGame.Reset();

        private const Single_Marketing_Roll._result Success = Single_Marketing_Roll._result.success;
        private const Single_Marketing_Roll._result Crit = Single_Marketing_Roll._result.success_crit;
        private const Single_Marketing_Roll._result Fail = Single_Marketing_Roll._result.fail;
        private const Single_Marketing_Roll._result CritFail = Single_Marketing_Roll._result.fail_crit;

        private static readonly Single_Marketing_Roll._result[] Rolls = { Success, Crit, Fail, CritFail };

        public static IEnumerable<object[]> PVs() => new[]
        {
            new object[] { singles._param._special_type.lewd_pv },
            new object[] { singles._param._special_type.edgy_pv },
            new object[] { singles._param._special_type.artsy_pv },
        };

        public static IEnumerable<object[]> AllTypes() =>
            new[]
            {
                singles._param._special_type.ad_campaign,
                singles._param._special_type.viral_campaign,
                singles._param._special_type.fake_scandal,
                singles._param._special_type.lewd_pv,
                singles._param._special_type.edgy_pv,
                singles._param._special_type.artsy_pv,
            }.Select(t => new object[] { t });

        /// <summary>
        /// The game's marketing option of this type, which it tells apart by id.
        /// </summary>
        public static singles._param Marketing(singles._param._special_type type)
        {
            singles._param param = new()
            {
                type = singles._param._type.marketing,
                id = type switch
                {
                    singles._param._special_type.ad_campaign => 3,
                    singles._param._special_type.viral_campaign => 4,
                    singles._param._special_type.fake_scandal => 5,
                    singles._param._special_type.lewd_pv => 6,
                    singles._param._special_type.edgy_pv => 7,
                    _ => 8,
                },
            };
            Assert.Equal(type, param.Special_Type);
            return param;
        }

        /// <summary>
        /// A single whose formation has these stats; the rest are 0.
        /// </summary>
        public static singles._single Formation(params (data_girls._paramType type, float value)[] stats)
        {
            foreach ((data_girls._paramType type, float value) in stats)
                Seams.SenbatsuStats[type] = value;
            return new singles._single();
        }

        /// <summary>
        /// A formation whose two stats for this PV average to this.
        /// </summary>
        public static singles._single Formation(singles._param._special_type pv, float average) => pv switch
        {
            singles._param._special_type.lewd_pv => Formation((data_girls._paramType.sexy, average - 10), (data_girls._paramType.cute, average + 10)),
            singles._param._special_type.edgy_pv => Formation((data_girls._paramType.cool, average - 10), (data_girls._paramType.funny, average + 10)),
            _ => Formation((data_girls._paramType.pretty, average - 10), (data_girls._paramType.smart, average + 10)),
        };

        /// <summary>
        /// The success chance with the mod's postfix applied to the game's.
        /// </summary>
        public static float Chance(singles._param param, Single_Marketing_Roll._result roll, singles._single single, int level = 1)
        {
            float chance = param.GetSuccessChance(roll, level, single);
            singles__param_GetSuccessChance.Postfix(ref chance, param, roll, single);
            return chance;
        }

        public static float Modifier(singles._param param, Single_Marketing_Roll._result roll, bool secondary, int level)
        {
            float modifier = param.GetSuccessModifier(roll, secondary, level);
            singles__param_GetSuccessModifier.Postfix(ref modifier, param, roll, secondary, level);
            return modifier;
        }

        /// <summary>
        /// Each stat is a different power of two, so each pair of stats has its own average:
        /// lewd (sexy 8, cute 2) averages 5, edgy (cool 4, funny 32) 18, artsy (pretty 16, smart 64) 40.
        /// </summary>
        [Theory]
        [InlineData(singles._param._special_type.lewd_pv, 5f)]
        [InlineData(singles._param._special_type.edgy_pv, 18f)]
        [InlineData(singles._param._special_type.artsy_pv, 40f)]
        public void PvChances_FollowTheirTwoStats(singles._param._special_type pv, float average)
        {
            singles._single single = Formation(
                (data_girls._paramType.dance, 1), (data_girls._paramType.cute, 2), (data_girls._paramType.cool, 4),
                (data_girls._paramType.sexy, 8), (data_girls._paramType.pretty, 16), (data_girls._paramType.funny, 32),
                (data_girls._paramType.smart, 64), (data_girls._paramType.vocal, 128));
            singles._param param = Marketing(pv);

            Assert.Equal(average * 0.9f, Chance(param, Success, single), 3);
            Assert.Equal(average * 0.1f, Chance(param, Crit, single), 3);
            Assert.Equal((100 - average) * 0.9f, Chance(param, Fail, single), 3);
            Assert.Equal((100 - average) * 0.1f, Chance(param, CritFail, single), 3);
        }

        [Theory]
        [MemberData(nameof(PVs))]
        public void PvChances_AddUpTo100(singles._param._special_type pv)
        {
            singles._single single = Formation(pv, 63);
            singles._param param = Marketing(pv);

            Assert.Equal(100f, Rolls.Sum(roll => Chance(param, roll, single)), 3);
        }

        /// <summary>
        /// The game gives success half the average and crit success a quarter; the mod gives them all of it.
        /// </summary>
        [Theory]
        [MemberData(nameof(PVs))]
        public void PvSuccess_IsAThirdMoreLikelyThanTheGame(singles._param._special_type pv)
        {
            singles._single single = Formation(pv, 60);
            singles._param param = Marketing(pv);

            float game = param.GetSuccessChance(Success, 1, single) + param.GetSuccessChance(Crit, 1, single);
            float mod = Chance(param, Success, single) + Chance(param, Crit, single);

            Assert.Equal(45f, game);
            Assert.Equal(60f, mod, 3);
        }

        [Theory]
        [MemberData(nameof(PVs))]
        public void PvChances_DontChangeWithLevel(singles._param._special_type pv)
        {
            singles._single single = Formation(pv, 60);
            singles._param param = Marketing(pv);

            foreach (Single_Marketing_Roll._result roll in Rolls)
                Assert.Equal(Chance(param, roll, single, level: 1), Chance(param, roll, single, level: 10));
        }

        /// <summary>
        /// Without a single the game can't rate a PV, and fails it.
        /// </summary>
        [Theory]
        [MemberData(nameof(PVs))]
        public void PvChances_NoSingle_AreTheGames(singles._param._special_type pv)
        {
            singles._param param = Marketing(pv);

            foreach (Single_Marketing_Roll._result roll in Rolls)
                Assert.Equal(param.GetSuccessChance(roll, 1, null), Chance(param, roll, null));
            Assert.Equal(100f, Chance(param, Fail, null));
        }

        [Theory]
        [InlineData(singles._param._special_type.ad_campaign)]
        [InlineData(singles._param._special_type.viral_campaign)]
        [InlineData(singles._param._special_type.fake_scandal)]
        public void OtherMarketingChances_AreTheGames(singles._param._special_type type)
        {
            singles._single single = Formation(singles._param._special_type.lewd_pv, 60);
            singles._param param = Marketing(type);

            foreach (Single_Marketing_Roll._result roll in Rolls)
                foreach (int level in new[] { 1, 5, 10 })
                    Assert.Equal(param.GetSuccessChance(roll, level, single), Chance(param, roll, single, level));
        }

        /// <summary>
        /// Fake scandal crit successes give 100 + 290 per level, up from the game's 100 + 100.
        /// </summary>
        [Theory]
        [InlineData(1, 390f)]
        [InlineData(5, 1550f)]
        [InlineData(10, 3000f)]
        public void FakeScandalCrit_Bonus(int level, float expected)
        {
            singles._param param = Marketing(singles._param._special_type.fake_scandal);

            Assert.Equal(expected, Modifier(param, Crit, secondary: false, level));
            Assert.True(expected > param.GetSuccessModifier(Crit, false, level));
        }

        [Fact]
        public void FakeScandalCrit_SecondaryIsTheGames()
        {
            singles._param param = Marketing(singles._param._special_type.fake_scandal);

            Assert.Equal(0f, Modifier(param, Crit, secondary: true, 5));
        }

        /// <summary>
        /// PV crit successes give 100 + 80 per level to their main fan type and 50 + 30 to the second,
        /// down from the game's 100 + 100 and 50 + 50.
        /// </summary>
        [Theory]
        [MemberData(nameof(PVs))]
        public void PvCrit_Bonus(singles._param._special_type pv)
        {
            singles._param param = Marketing(pv);

            foreach (int level in Enumerable.Range(1, 10))
            {
                Assert.Equal(100f + 80f * level, Modifier(param, Crit, secondary: false, level));
                Assert.Equal(50f + 30f * level, Modifier(param, Crit, secondary: true, level));
                Assert.True(Modifier(param, Crit, false, level) < param.GetSuccessModifier(Crit, false, level));
                Assert.True(Modifier(param, Crit, true, level) < param.GetSuccessModifier(Crit, true, level));
            }
        }

        [Theory]
        [MemberData(nameof(AllTypes))]
        public void OtherModifiers_AreTheGames(singles._param._special_type type)
        {
            singles._param param = Marketing(type);

            foreach (Single_Marketing_Roll._result roll in new[] { Success, Fail, CritFail })
                foreach (bool secondary in new[] { false, true })
                    foreach (int level in new[] { 1, 5, 10 })
                        Assert.Equal(param.GetSuccessModifier(roll, secondary, level), Modifier(param, roll, secondary, level));
        }

        [Theory]
        [InlineData(singles._param._special_type.ad_campaign)]
        [InlineData(singles._param._special_type.viral_campaign)]
        public void AdAndViralCrits_AreTheGames(singles._param._special_type type)
        {
            singles._param param = Marketing(type);

            foreach (bool secondary in new[] { false, true })
                foreach (int level in new[] { 1, 5, 10 })
                    Assert.Equal(param.GetSuccessModifier(Crit, secondary, level), Modifier(param, Crit, secondary, level));
        }
    }

    /// <summary>
    /// Every day the fan base loses the churn worked out the day before, and works out the next day's.
    /// </summary>
    public class ChurnTests
    {
        public ChurnTests() => TestGame.Reset();

        private static long FansTotal() => resources.GetFansTotal();

        /// <summary>
        /// Normal churn is fans^0.75 * 0.012 a day; Unfair is fans^0.83 * 0.012 + 2. Both round up.
        /// </summary>
        [Theory]
        [InlineData(_difficulty.normal, 0, 0)]
        [InlineData(_difficulty.normal, 1000, -3)]
        [InlineData(_difficulty.normal, 100000, -68)]
        [InlineData(_difficulty.normal, 500000, -226)]
        [InlineData(_difficulty.hard, 0, -2)]
        [InlineData(_difficulty.hard, 1000, -6)]
        [InlineData(_difficulty.hard, 100000, -172)]
        [InlineData(_difficulty.hard, 500000, -647)]
        public void Churn_GrowsWithFans(_difficulty difficulty, long fans, long expected)
        {
            TestGame.SetDifficulty(difficulty);
            TestGame.FanBase(fans);

            Utility.DailyFanChurn();

            Assert.Equal(expected, resources.FansChange);
        }

        [Fact]
        public void Churn_IsLostTheNextDay()
        {
            TestGame.FanBase(100000);

            Utility.DailyFanChurn();
            Assert.Equal(100000, FansTotal());

            Utility.DailyFanChurn();
            Assert.Equal(100000 - 68, FansTotal());
            Assert.Equal(new[] { (resources.type.fans, 0L), (resources.type.fans, -68L) }, Seams.ResourcesAdded);
        }

        /// <summary>
        /// The next day's churn comes from the fan base after today's loss. 10,001 fans churn 12.0009 a day,
        /// rounded up to 13; the 9,988 left churn 11.99, rounded up to 12.
        /// </summary>
        [Fact]
        public void Churn_FollowsTheShrinkingFanBase()
        {
            TestGame.FanBase(10001);

            Utility.DailyFanChurn();
            Assert.Equal(-13, resources.FansChange);

            Utility.DailyFanChurn();
            Assert.Equal(10001 - 13, FansTotal());
            Assert.Equal(-12, resources.FansChange);
        }

        [Fact]
        public void Easy_HasNoChurn()
        {
            TestGame.SetDifficulty(_difficulty.easy);
            TestGame.FanBase(100000);

            Utility.DailyFanChurn();
            Utility.DailyFanChurn();

            Assert.Equal(100000, FansTotal());
            Assert.Equal(0, resources.FansChange);
            Assert.Empty(Seams.ResourcesAdded);
        }

        /// <summary>
        /// The game sets FansChange to 0 at the start of every day; the mod removes that, so the churn
        /// survives to the next day.
        /// </summary>
        [Fact]
        public void NewDay_NoLongerClearsTheChurn()
        {
            MethodInfo onNewDay = AccessTools.Method(typeof(resources), nameof(resources.OnNewDay));
            FieldInfo fansChange = AccessTools.Field(typeof(resources), nameof(resources.FansChange));
            List<CodeInstruction> game = PatchProcessor.GetOriginalInstructions(onNewDay);
            List<CodeInstruction> mod = resources_OnNewDay.Transpiler(PatchProcessor.GetOriginalInstructions(onNewDay)).ToList();

            // FansChange = 0L is the day's only write to it, and all three of its instructions go
            int store = game.FindIndex(i => i.opcode == OpCodes.Stsfld && Equals(i.operand, fansChange));
            Assert.Single(game, i => Equals(i.operand, fansChange));
            Assert.Equal(new[] { OpCodes.Ldc_I4_0, OpCodes.Conv_I8, OpCodes.Stsfld }, game.Skip(store - 2).Take(3).Select(i => i.opcode));

            Assert.Equal(game.Count, mod.Count);
            for (int i = 0; i < game.Count; i++)
            {
                if (i >= store - 2 && i <= store)
                {
                    Assert.Equal(OpCodes.Nop, mod[i].opcode);
                }
                else
                {
                    Assert.Equal(game[i].opcode, mod[i].opcode);
                    Assert.Equal(game[i].operand, mod[i].operand);
                }
            }
        }

        /// <summary>
        /// Each new day recounts the fans from every source for the tooltip, then takes the churn.
        /// </summary>
        [Fact]
        public void NewDay_CountsFansThenTakesChurn()
        {
            TestGame.FanBase(100000);
            resources.FansChange = -68;
            TestGame.Business.ActiveProposals.Add(new business.active_proposal { Type = business._type.ad, Fans_per_week = 300 });

            resources_OnNewDay.Postfix();

            Assert.Equal(300, Utility.adFans);
            Assert.Equal(100000 - 68, FansTotal());
            Assert.Equal(-68, resources.FansChange);
        }
    }

    /// <summary>
    /// The fans a week from contracts, shows and cafes, which the tooltip lists.
    /// </summary>
    public class FanCountTests
    {
        public FanCountTests() => TestGame.Reset();

        private static void Contract(business._type type, int fansPerWeek) =>
            TestGame.Business.ActiveProposals.Add(new business.active_proposal { Type = type, Fans_per_week = fansPerWeek });

        private static void Show(Shows._param._media_type medium, Shows._show._status status, params int[] newFans)
        {
            Shows._show show = new() { medium = new Shows._param { media_type = medium }, status = status };
            show.fans.AddRange(newFans);
            Shows.shows.Add(show);
        }

        private static void Cafe(params int[] newFansByDay)
        {
            Cafes._cafe cafe = new();
            cafe.Stats.AddRange(newFansByDay.Select(fans => new Cafes._cafe._stat { New_Fans = fans }));
            Cafes.Cafes_.Add(cafe);
        }

        [Fact]
        public void Contracts_AddUpByType()
        {
            Contract(business._type.ad, 100);
            Contract(business._type.ad, 250);
            Contract(business._type.tv_drama, 500);
            Contract(business._type.photoshoot, 1000);
            Contract(business._type.variety, 1000);

            Seams.UpdateFanCount();

            Assert.Equal(350, Utility.adFans);
            Assert.Equal(500, Utility.dramaFans);
        }

        [Fact]
        public void Contracts_WithoutFans_AreLeftOut()
        {
            Contract(business._type.ad, 0);
            Contract(business._type.ad, -50);
            Contract(business._type.tv_drama, -50);

            Seams.UpdateFanCount();

            Assert.Equal(0, Utility.adFans);
            Assert.Equal(0, Utility.dramaFans);
        }

        [Fact]
        public void Shows_AddTheirLatestEpisodeByMedium()
        {
            Show(Shows._param._media_type.tv, Shows._show._status.released, 10, 400);
            Show(Shows._param._media_type.tv, Shows._show._status.relaunching, 30);
            Show(Shows._param._media_type.radio, Shows._show._status.relaunching_working, 50, 20);
            Show(Shows._param._media_type.internet, Shows._show._status.released, 5, 7);

            Seams.UpdateFanCount();

            Assert.Equal(430, Utility.tvFans);
            Assert.Equal(20, Utility.radioFans);
            Assert.Equal(7, Utility.netFans);
        }

        /// <summary>
        /// Shows still in production, or cancelled, aren't airing.
        /// </summary>
        [Theory]
        [InlineData(Shows._show._status.normal)]
        [InlineData(Shows._show._status.working)]
        [InlineData(Shows._show._status.canceled)]
        public void Shows_NotAiring_AreLeftOut(Shows._show._status status)
        {
            Show(Shows._param._media_type.tv, status, 400);

            Seams.UpdateFanCount();

            Assert.Equal(0, Utility.tvFans);
        }

        [Fact]
        public void Cafes_AddTheirLastWeek()
        {
            Cafe(1000, 1, 2, 3, 4, 5, 6, 7);
            Cafe(10, 20);

            Seams.UpdateFanCount();

            Assert.Equal(28 + 30, Utility.cafeFans);
        }

        [Fact]
        public void Recount_StartsFromZero()
        {
            Contract(business._type.ad, 100);
            Contract(business._type.tv_drama, 200);
            Show(Shows._param._media_type.tv, Shows._show._status.released, 300);
            Show(Shows._param._media_type.radio, Shows._show._status.released, 400);
            Show(Shows._param._media_type.internet, Shows._show._status.released, 500);
            Cafe(600);

            Seams.UpdateFanCount();
            Seams.UpdateFanCount();

            Assert.Equal(new long[] { 100, 200, 300, 400, 500, 600 },
                new[] { Utility.adFans, Utility.dramaFans, Utility.tvFans, Utility.radioFans, Utility.netFans, Utility.cafeFans });
        }
    }
}
