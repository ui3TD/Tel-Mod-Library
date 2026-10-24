using System.Runtime.Serialization;
using Xunit;
using static MBTIPersonalities.MBTIPersonalities;
using static MBTIPersonalities.Tests.TestGirls;

namespace MBTIPersonalities.Tests
{
    /// <summary>
    /// MBTI alongside other mods: missing idols, mods that skip the game's method or nest the stat
    /// calculations, and the job-offer popup stamina that Traits Expansion also lowers.
    /// </summary>
    [Collection(MBTICollection.Name)]
    public class OtherModsTests
    {
        [Fact]
        public void NoIdol_HasNoType()
        {
            Assert.Equal(MBTI.None, GetGirlMBTI(null));
            SetGirlMBTI(null, MBTI.INTJ);
        }

        [Fact]
        public void OfferWithNoIdol_Unchanged()
        {
            business._proposal proposal = new() { girl = null, stamina = 20 };
            Business_Popup_Set.Prefix(proposal, out int? state);
            Business_Popup_Set.Finalizer(proposal, state);
            Assert.Equal(20, proposal.stamina);

            Business_Popup_Set.Prefix(null, out state);
            Business_Popup_Set.Finalizer(null, state);

            business biz = (business)FormatterServices.GetUninitializedObject(typeof(business));
            biz.ActiveProposal = proposal;
            business_Accept.Prefix(biz, true);
            Assert.Equal(20, proposal.stamina);
            biz.ActiveProposal = null;
            business_Accept.Prefix(biz, true);
        }

        [Fact]
        public void DishWhoseIdolLeft_Unchanged()
        {
            int val = -10;
            Cafes__cafe__dish_AddNovelty.Prefix(new Cafes._cafe._dish { Girl = 987654 }, ref val);
            Assert.Equal(-10, val);
        }

        [Fact]
        public void StatWithNoIdol_Unchanged()
        {
            float duration = 100f;
            data_girls_girls_param_GetDuration.Postfix(new data_girls.girls.param { type = data_girls._paramType.vocal }, ref duration);
            Assert.Equal(100f, duration);
        }

        /// <summary>
        /// Another mod skipped the game's Accept: the offer isn't taken, so it isn't lowered.
        /// </summary>
        [Fact]
        public void AcceptSkipped_NotLowered()
        {
            business biz = (business)FormatterServices.GetUninitializedObject(typeof(business));
            biz.ActiveProposal = new business._proposal { girl = Make(MBTI.ENFP), stamina = 20 };
            business_Accept.Prefix(biz, false);
            Assert.Equal(20, biz.ActiveProposal.stamina);
        }

        /// <summary>A stand-in for another mod that halves the popup stamina and puts back what it took off.</summary>
        private static int OtherPrefix(business._proposal proposal)
        {
            int lowered = proposal.stamina / 2;
            int taken = proposal.stamina - lowered;
            proposal.stamina = lowered;
            return taken;
        }

        /// <summary>
        /// Two mods lowering the popup stamina, as MBTI's ENFP and Traits Expansion's Thespian do, leave
        /// the offer where it started whichever order Harmony runs them in.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TwoModsLoweringThePopup_PutItBack(bool mbtiFirst)
        {
            business._proposal proposal = new() { girl = Make(MBTI.ENFP), stamina = 21 };
            int? mbti;
            int other;
            if (mbtiFirst)
            {
                Business_Popup_Set.Prefix(proposal, out mbti);
                other = OtherPrefix(proposal);
            }
            else
            {
                other = OtherPrefix(proposal);
                Business_Popup_Set.Prefix(proposal, out mbti);
            }

            // Finalizers run in the same order as the prefixes
            if (mbtiFirst)
            {
                Business_Popup_Set.Finalizer(proposal, mbti);
                proposal.stamina += other;
            }
            else
            {
                proposal.stamina += other;
                Business_Popup_Set.Finalizer(proposal, mbti);
            }

            Assert.Equal(21, proposal.stamina);
        }

        /// <summary>
        /// Another mod working out a proposal inside a show's calculation: when the inner one ends, the
        /// show's bonuses are still on, and they're off once the show's ends.
        /// </summary>
        [Fact]
        public void NestedStatContexts_KeepTheOuterOnes()
        {
            patchGetVal = isShow = isRisky = false;
            Shows__show_SenbatsuCalcParam.Prefix(out StatFlags outer);
            Business__proposal_GetGirlCoeff.Prefix(out StatFlags inner);
            Business__proposal_GetGirlCoeff.Finalizer(inner);

            Assert.True(patchGetVal);
            Assert.True(isShow);

            Shows__show_SenbatsuCalcParam.Finalizer(outer);
            Assert.False(patchGetVal);
            Assert.False(isShow);
        }

        [Fact]
        public void NestedElections_KeepTheOuterBonus()
        {
            patchGetFan_Count_ENFJ = false;
            SEvent_SSK__SSK_GenerateResults.Prefix(out bool outer);
            SEvent_SSK__SSK_GenerateResults.Prefix(out bool inner);
            SEvent_SSK__SSK_GenerateResults.Finalizer(inner);
            Assert.True(patchGetFan_Count_ENFJ);
            SEvent_SSK__SSK_GenerateResults.Finalizer(outer);
            Assert.False(patchGetFan_Count_ENFJ);
        }

        [Fact]
        public void NestedBirthdayStats_KeepTheOuterBonus()
        {
            patchSetVal_INTJ = patchSet_INTJ = false;
            Birthday_Popup popup = (Birthday_Popup)FormatterServices.GetUninitializedObject(typeof(Birthday_Popup));
            popup.Girl = Make(MBTI.INTJ);

            Birthday_Popup_DoParam.Prefix(data_girls._paramType.smart, ref popup, out IntjFlags outer);
            Birthday_Popup_DoParam.Prefix(data_girls._paramType.cute, ref popup, out IntjFlags inner);
            Birthday_Popup_DoParam.Finalizer(inner);
            Assert.True(patchSetVal_INTJ && patchSet_INTJ);

            Birthday_Popup_DoParam.Finalizer(outer);
            Assert.False(patchSetVal_INTJ || patchSet_INTJ);
        }

        /// <summary>
        /// While her smart stat is set for her birthday, a stat another mod sets meanwhile isn't doubled.
        /// </summary>
        [Fact]
        public void BirthdayDoublesOnlySmart()
        {
            WithFlag(on => patchSetVal_INTJ = on, () =>
            {
                float smart = 45f;
                data_girls_girls_param_setVal.Prefix(ref smart, new data_girls.girls.param { type = data_girls._paramType.smart, _val = 40f });
                Assert.Equal(50f, smart);

                float cute = 45f;
                data_girls_girls_param_setVal.Prefix(ref cute, new data_girls.girls.param { type = data_girls._paramType.cute, _val = 40f });
                Assert.Equal(45f, cute);
            });
        }

        /// <summary>
        /// The audition card's type is added once, even if another mod skipped the game's rewrite of the text.
        /// </summary>
        [Fact]
        public void CardText_AddedOnce()
        {
            string once = Audition_Data_Card_ShowTrait.WithAddition("Polyglot", " / ISTJ");
            Assert.Equal("Polyglot / ISTJ", once);
            Assert.Equal("Polyglot / ISTJ", Audition_Data_Card_ShowTrait.WithAddition(once, " / ISTJ"));
        }

        [Fact]
        public void GeneratedNothing_LeftAlone()
        {
            data_girls.girls girl = null;
            data_girls_GenerateGirl.Postfix(ref girl, true);
            Assert.Null(girl);
        }

        [Fact]
        public void PackWithNoBodyAsset_RecordsNothing()
        {
            int before = UniqueIdolTypes.Count;
            data_girls_textures_LoadAssetsData.Infix(SimpleJSON.JSON.Parse("{ \"mbti\": \"INTJ\" }"), null);
            data_girls_textures_LoadAssetsData.Infix(null, new data_girls_textures._textureAsset());
            Assert.Equal(before, UniqueIdolTypes.Count);
        }
    }
}
