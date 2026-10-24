using System;
using System.Collections.Generic;
using TraitsExpansion;
using Xunit;
using static data_girls._paramType;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansionTests
{
    /// <summary>
    /// Traits Expansion alongside other mods: mods that skip the game's method or nest the stat
    /// calculations, missing idols, and the job-offer popup stamina that MBTI also lowers.
    /// </summary>
    public class OtherModsTests
    {
        public OtherModsTests() => TestGame.Reset();

        /// <summary>A stand-in for another mod that takes 1 off the popup stamina and puts back what it took off, as MBTI's ENFP does.</summary>
        private static int OtherPrefix(business._proposal proposal)
        {
            proposal.stamina -= 1;
            return 1;
        }

        /// <summary>
        /// Two mods lowering the popup stamina leave the offer where it started whichever order Harmony runs them in.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TwoModsLoweringThePopup_PutItBack(bool thespianFirst)
        {
            business._proposal proposal = new() { type = business._type.tv_drama, girl = TestGame.Idol(NewTraits.Thespian), stamina = 21 };
            int? thespian;
            int other;
            if (thespianFirst)
            {
                Business_Popup_Set.Prefix(proposal, out thespian);
                other = OtherPrefix(proposal);
            }
            else
            {
                other = OtherPrefix(proposal);
                Business_Popup_Set.Prefix(proposal, out thespian);
            }

            // Finalizers run in the same order as the prefixes
            if (thespianFirst)
            {
                Business_Popup_Set.Finalizer(proposal, thespian);
                proposal.stamina += other;
            }
            else
            {
                proposal.stamina += other;
                Business_Popup_Set.Finalizer(proposal, thespian);
            }

            Assert.Equal(21, proposal.stamina);
        }

        [Fact]
        public void PopupChangeByAnotherMod_Kept()
        {
            business._proposal proposal = new() { type = business._type.tv_drama, girl = TestGame.Idol(NewTraits.Thespian), stamina = 20 };
            Business_Popup_Set.Prefix(proposal, out int? state);
            Assert.Equal(10, proposal.stamina);
            proposal.stamina += 5;
            Business_Popup_Set.Finalizer(proposal, state);
            Assert.Equal(25, proposal.stamina);
        }

        [Fact]
        public void AcceptSkipped_NotLowered()
        {
            business biz = TestGame.Component<business>();
            biz.ActiveProposal = new business._proposal { type = business._type.tv_drama, girl = TestGame.Idol(NewTraits.Thespian), stamina = 20 };
            business_Accept.Prefix(biz, false);
            Assert.Equal(20, biz.ActiveProposal.stamina);
        }

        [Fact]
        public void InjuriesSkipped_NoRecklessRoll()
        {
            data_girls.girls girl = TestGame.Idol(NewTraits.Reckless);
            TestGame.SetStat(girl, physicalStamina, 0f);
            Seams.Chance = _ => true;

            data_girls_girls_Try_Injury.Postfix(girl, false);

            Assert.Empty(Seams.ChancesRolled);
            Assert.Empty(Seams.Injured);
        }

        [Fact]
        public void NestedStatContexts_KeepTheOuterOne()
        {
            patchGetVal = false;
            Shows__show_SenbatsuCalcParam.Prefix(out bool outer);
            Business__proposal_GetGirlCoeff.Prefix(out bool inner);
            Business__proposal_GetGirlCoeff.Finalizer(null, inner);
            Assert.True(patchGetVal);

            Shows__show_SenbatsuCalcParam.Finalizer(null, outer);
            Assert.False(patchGetVal);
        }

        private static SEvent_Concerts._concert Concert(data_girls.girls center)
        {
            SEvent_Concerts._concert concert = new();
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = center });
            concert.SetListItems.Add(null);
            return concert;
        }

        /// <summary>
        /// Stage Fright costs once, for the call that finishes the concert; not again if Finish runs on a finished concert.
        /// </summary>
        [Fact]
        public void StageFright_OncePerConcert()
        {
            SEvent_Concerts._concert concert = Concert(TestGame.Idol(NewTraits.Stage_Fright));
            for (int call = 0; call < 2; call++)
            {
                SEvent_Concerts__concert_Finish.Prefix(concert, out bool alreadyFinished);
                concert.Status = SEvent_Tour.tour._status.finished;
                SEvent_Concerts__concert_Finish.Postfix(concert, true, alreadyFinished);
            }

            Assert.Single(Seams.ParamsAdded);
        }

        [Fact]
        public void FinishSkipped_NoStageFright()
        {
            SEvent_Concerts._concert concert = Concert(TestGame.Idol(NewTraits.Stage_Fright));
            SEvent_Concerts__concert_Finish.Prefix(concert, out bool alreadyFinished);
            SEvent_Concerts__concert_Finish.Postfix(concert, false, alreadyFinished);

            Assert.Empty(Seams.ParamsAdded);
            Assert.Empty(Seams.Notifications);
        }

        private static Relationships._clique Clique(data_girls.girls victim, params data_girls.girls[] members)
        {
            Relationships._clique clique = new() { Leader = members[0], Members = new List<data_girls.girls>(members) };
            clique.Bullied_Girls.Add(victim);
            Relationships.Cliques.Add(clique);
            return clique;
        }

        [Fact]
        public void BullyingSkipped_NoSadisticDamage()
        {
            data_girls.girls victim = TestGame.Idol();
            Clique(victim, TestGame.Idol(NewTraits.Sadistic));

            Relationships_Do_Bullying.Postfix(false);

            Assert.Equal(100f, TestGame.Stat(victim, mentalStamina));
            Assert.Empty(Seams.Notifications);
        }

        [Fact]
        public void MissingMembers_Skipped()
        {
            data_girls.girls victim = TestGame.Idol();
            Relationships._clique clique = Clique(victim, TestGame.Idol(NewTraits.Sadistic), null);
            clique.Bullied_Girls.Add(null);
            Relationships.Cliques.Add(null);

            Relationships_Do_Bullying.Postfix(true);

            Assert.Equal(90f, TestGame.Stat(victim, mentalStamina));
        }

        /// <summary>
        /// An error would stop the rest of the game's week, so it's logged once and the week goes on.
        /// </summary>
        [Fact]
        public void BullyingError_LoggedOnce()
        {
            Clique(TestGame.Idol(), TestGame.Idol(NewTraits.Sadistic)).KnownBulliedGirls = null;
            UnityEngine.ILogHandler gameLog = UnityEngine.Debug.unityLogger.logHandler;
            List<string> logged = new();
            UnityEngine.Debug.unityLogger.logHandler = new Recorder(logged);
            try
            {
                Relationships_Do_Bullying.Postfix(true);
                Relationships_Do_Bullying.Postfix(true);
            }
            finally
            {
                UnityEngine.Debug.unityLogger.logHandler = gameLog;
            }

            string error = Assert.Single(logged);
            Assert.StartsWith("[Traits Expansion] Couldn't apply this week's Sadistic bullying", error);
        }

        [Fact]
        public void PackWithMissingParts_Skipped()
        {
            data_girls_textures_LoadAssetsData.Infix(null, SimpleJSON.JSON.Parse("{ \"trait\": \"Polyglot\" }"));
            data_girls_textures._textureAsset asset = new();
            data_girls_textures_LoadAssetsData.Infix(asset, null);
            Assert.Equal(default, asset.Trait);

            data_girls_textures_LoadAssetsData.WarnUnlessModTrait("Trait not found: x", null);
            Assert.Equal(new[] { "Trait not found: x" }, Seams.Warnings);
        }

        private class Recorder : UnityEngine.ILogHandler
        {
            private readonly List<string> messages;
            public Recorder(List<string> messages) => this.messages = messages;
            public void LogFormat(UnityEngine.LogType logType, UnityEngine.Object context, string format, params object[] args) => messages.Add(string.Format(format, args));
            public void LogException(Exception exception, UnityEngine.Object context) => messages.Add(exception.ToString());
        }
    }
}
