using TraitsExpansion;
using Xunit;
using static business._type;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansionTests
{
    /// <summary>
    /// Wooden Acting takes 0.2 off an idol's drama reward multiplier, and Quick Wit adds 0.5 to her
    /// variety show multiplier. The multiplier is 1.0 at 50 in the job's skill.
    /// </summary>
    public class RewardMultiplierTests
    {
        public RewardMultiplierTests() => TestGame.Reset(patched: true);

        private static float Coeff(NewTraits trait, business._type type, float skill)
        {
            business._proposal proposal = new() { type = type, skill = data_girls._paramType.smart };
            return proposal.GetGirlCoeff(TestGame.Idol(trait, skill));
        }

        [Theory]
        [InlineData(0f, 0.55f)]
        [InlineData(50f, 0.8f)]
        [InlineData(100f, 1.3f)]
        public void WoodenActing_DramaLoses02(float skill, float expected)
        {
            Assert.Equal(expected, Coeff(NewTraits.Wooden_Acting, tv_drama, skill), 3);
        }

        [Theory]
        [InlineData(0f, 1.25f)]
        [InlineData(50f, 1.5f)]
        [InlineData(100f, 2f)]
        public void QuickWit_VarietyGains05(float skill, float expected)
        {
            Assert.Equal(expected, Coeff(NewTraits.Quick_Wit, variety, skill), 3);
        }

        [Theory]
        [InlineData(NewTraits.Wooden_Acting, variety)]
        [InlineData(NewTraits.Wooden_Acting, photoshoot)]
        [InlineData(NewTraits.Quick_Wit, tv_drama)]
        [InlineData(NewTraits.Quick_Wit, photoshoot)]
        [InlineData(NewTraits.Thespian, tv_drama)]
        [InlineData(NewTraits.none, tv_drama)]
        [InlineData(NewTraits.none, variety)]
        public void OtherJobsAndTraits_Unchanged(NewTraits trait, business._type type)
        {
            Assert.Equal(1f, Coeff(trait, type, 50f), 3);
        }

        /// <summary>
        /// The bonus is added after vanilla's award bonuses: a Variety Queen at 50 skill gets 1 + 4 + 0.5.
        /// </summary>
        [Fact]
        public void QuickWit_AddsToAwardBonus()
        {
            business._proposal proposal = new() { type = variety, skill = data_girls._paramType.smart };
            data_girls.girls girl = TestGame.Idol(NewTraits.Quick_Wit, 50f);
            Awards._Awards.Add(new Awards._award { Type = Awards._type.variety_queen, Year = TestGame.Today.Year, Girl = girl });

            Assert.Equal(5.5f, proposal.GetGirlCoeff(girl), 3);
        }
    }

    /// <summary>
    /// Thespian halves the stamina cost of drama proposals, both on the proposal popup and when accepted.
    /// </summary>
    public class ThespianTests
    {
        public ThespianTests() => TestGame.Reset();

        [Theory]
        [InlineData(20, 10)]
        [InlineData(30, 15)]
        [InlineData(25, 12)]
        [InlineData(1, 0)]
        public void Popup_ShowsHalfStamina_ThenRestores(int stamina, int shown)
        {
            business._proposal proposal = new() { type = tv_drama, girl = TestGame.Idol(NewTraits.Thespian), stamina = stamina };
            int state = 0;

            Business_Popup_Set.Prefix(ref proposal, ref state);
            Assert.Equal(shown, proposal.stamina);

            Business_Popup_Set.Postfix(ref proposal, ref state);
            Assert.Equal(stamina, proposal.stamina);
        }

        [Theory]
        [InlineData(NewTraits.Thespian, variety)]
        [InlineData(NewTraits.Thespian, photoshoot)]
        [InlineData(NewTraits.Wooden_Acting, tv_drama)]
        [InlineData(NewTraits.none, tv_drama)]
        public void Popup_OtherJobsAndTraits_Unchanged(NewTraits trait, business._type type)
        {
            business._proposal proposal = new() { type = type, girl = TestGame.Idol(trait), stamina = 20 };
            int state = 0;

            Business_Popup_Set.Prefix(ref proposal, ref state);
            Assert.Equal(20, proposal.stamina);
            Assert.Equal(0, state);

            Business_Popup_Set.Postfix(ref proposal, ref state);
            Assert.Equal(20, proposal.stamina);
        }

        [Theory]
        [InlineData(20, 10)]
        [InlineData(25, 12)]
        public void Accept_CostsHalfStamina(int stamina, int expected)
        {
            business biz = TestGame.Component<business>();
            biz.ActiveProposal = new business._proposal { type = tv_drama, girl = TestGame.Idol(NewTraits.Thespian), stamina = stamina };

            business_Accept.Prefix(ref biz);
            Assert.Equal(expected, biz.ActiveProposal.stamina);
        }

        [Theory]
        [InlineData(NewTraits.Thespian, variety)]
        [InlineData(NewTraits.none, tv_drama)]
        public void Accept_OtherJobsAndTraits_Unchanged(NewTraits trait, business._type type)
        {
            business biz = TestGame.Component<business>();
            biz.ActiveProposal = new business._proposal { type = type, girl = TestGame.Idol(trait), stamina = 20 };

            business_Accept.Prefix(ref biz);
            Assert.Equal(20, biz.ActiveProposal.stamina);
        }
    }
}
