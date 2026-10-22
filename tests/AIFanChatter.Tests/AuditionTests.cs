using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;
using P = data_girls._paramType;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// Idols hired from an audition the player launched and paid for, posted once its popup closes.
    /// </summary>
    public class AuditionTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string H = Moment.HistorySection;

        private static readonly Dictionary<string, string> Texts = new()
        {
            ["AUDITION__LOCAL"] = "Local",
            ["AUDITION__NATIONWIDE"] = "Nationwide",
        };

        public AuditionTests()
        {
            TestEnv.Reset();
            foreach (KeyValuePair<string, string> text in Texts)
            {
                Language.Data[text.Key] = text.Value;
            }
        }

        public void Dispose()
        {
            foreach (string key in Texts.Keys)
            {
                Language.Data.Remove(key);
            }
            AuditionHires.Clear();
        }

        private static void Launch(Auditions.type scale, bool paid = true) =>
            Auditions_GenerateAudition.Prefix(new Auditions.data { Type = scale }, paid);

        /// <summary>The game's Hire: she joins the agency's list (Idol does that), then the postfix runs.</summary>
        private static data_girls.girls Hire(string name)
        {
            data_girls.girls girl = Idol(name, fans: 0);
            girl.Hiring_Date = Today;
            data_girls_Hire.Postfix(girl, true);
            return girl;
        }

        /// <summary>The audition's close: whether it posts, and the audition it took.</summary>
        private static (bool post, AuditionHires.Audition audition) Close(bool ran = true)
        {
            AuditionHires.Audition audition = AuditionHires.Pending;
            bool post = Popup_Audition_Close.ShouldPost(audition, ran);
            Popup_Audition_Close.Postfix(ran);
            return (post, audition);
        }

        [Fact]
        public void PaidAudition_WithHires_PostsOnce()
        {
            Launch(Auditions.type.nationwide);
            data_girls.girls rin = Hire("Rin");
            data_girls.girls aoi = Hire("Aoi");
            var (post, audition) = Close();
            Assert.True(post);
            Assert.Equal(new[] { rin, aoi }, audition.Hired);
            Assert.Equal(Auditions.type.nationwide, audition.Scale);
            // The close took it, so a second close has nothing to post
            Assert.Null(AuditionHires.Pending);
            Assert.False(Close().post);
        }

        /// <summary>
        /// No hires, the unpaid copycat story audition, a hire with no audition open (the story's rival
        /// idols), or a close another mod skipped: no posts.
        /// </summary>
        [Fact]
        public void OtherCloses_DoNotPost()
        {
            Launch(Auditions.type.local);
            Assert.False(Close().post);

            Launch(Auditions.type.local, paid: false);
            Hire("Copycat");
            Assert.False(Close().post);

            Hire("Rival");
            Assert.False(Close().post);

            Launch(Auditions.type.local);
            Hire("Skipped");
            Assert.False(Close(ran: false).post);
            Assert.Null(AuditionHires.Pending);
        }

        /// <summary>
        /// A launch that never closed is dropped by the next one, so its hires don't leak into it.
        /// </summary>
        [Fact]
        public void NewLaunch_DropsAStaleAudition()
        {
            Launch(Auditions.type.local);
            Hire("Old");
            Launch(Auditions.type.regional);
            data_girls.girls fresh = Hire("Fresh");
            var (post, audition) = Close();
            Assert.True(post);
            Assert.Equal(new[] { fresh }, audition.Hired);
        }

        /// <summary>
        /// The main group, where the game puts them, gives its size and how many joined.
        /// </summary>
        [Fact]
        public void MainGroupsSize_SaysHowManyJoined()
        {
            data_girls.girls rin = Idol("Rin"), aoi = Idol("Aoi");
            Group("Stars", Today.AddYears(-2), Idol("Old1"), Idol("Old2"), rin, aoi);
            staticVars.PlayerData.GroupName = "Stars";
            Moment moment = MomentContext.Audition(new List<data_girls.girls> { rin, aoi }, Auditions.type.local);
            Assert.Equal("4 now, up from 2", moment.Fact(Moment.AgencySection, "Members in Stars"));
        }

        [Fact]
        public void OneIdol_IsNamedInTheKindAndLabel()
        {
            Moment moment = MomentContext.Audition(new List<data_girls.girls> { Idol("Rin") }, Auditions.type.local);
            Assert.Equal("a new idol joined the group", moment.Kind);
            Assert.Equal("Rin joining", moment.Label);
            Assert.Equal("Rin", moment.Fact(F, "New members"));
            Assert.Equal("Local", moment.Fact(F, "Audition"));
        }

        /// <summary>
        /// Several idols: every name up to 10, then how many more; up to 5 lines. No rarity is sent.
        /// </summary>
        [Fact]
        public void ManyIdols_AreListed_AndCapped()
        {
            List<data_girls.girls> hired = Enumerable.Range(1, 12).Select(i => Idol("New" + i)).ToList();
            Moment moment = MomentContext.Audition(hired, Auditions.type.nationwide);
            Assert.Equal("new idols joined the group", moment.Kind);
            Assert.Equal("the new members", moment.Label);
            Assert.Equal(string.Join(", ", Enumerable.Range(1, 10).Select(i => "New" + i)) + " and 2 more", moment.Fact(F, "New members"));
            Assert.Equal("Nationwide", moment.Fact(F, "Audition"));
            Assert.Equal(5, moment.Facts(M).Count);
            // No group was set up, so no group's size is given
            string text = moment.Describe().ToLowerInvariant();
            foreach (string rarity in new[] { "silver", "gold", "platinum", "rarity" })
            {
                Assert.DoesNotContain(rarity, text);
            }
        }

        /// <summary>
        /// A newcomer's line keeps who she is and how she compares with the group, without the time,
        /// election, fans and singles that are all empty on her first day.
        /// </summary>
        [Fact]
        public void NewcomerLines_LeaveOutHerEmptyRecord()
        {
            data_girls.girls ace = Idol("Ace", fans: 0).With(P.vocal, 95).With(P.dance, 50).With(P.cute, 50)
                .With(P.cool, 50).With(P.pretty, 50).With(P.funny, 50).With(P.smart, 50).With(P.sexy, 50).WithTrait("Prodigy")
                // A new hire's stamina is full
                .With(P.physicalStamina, 100).With(P.mentalStamina, 100);
            ace.Hiring_Date = Today;
            data_girls.girls a = Idol("A", fans: 5000).With(P.vocal, 40).With(P.dance, 50).With(P.cute, 50)
                .With(P.cool, 50).With(P.pretty, 50).With(P.funny, 50).With(P.smart, 50).With(P.sexy, 50);
            data_girls.girls b = Idol("B", fans: 4000).With(P.vocal, 30).With(P.dance, 50).With(P.cute, 50)
                .With(P.cool, 50).With(P.pretty, 50).With(P.funny, 50).With(P.smart, 50).With(P.sexy, 50);
            Group("Stars", Today.AddYears(-2), a, b, ace);
            Single("Song", Today.AddDays(-30), 1000, a, b).In(Groups.Groups_[0]);
            Election(Today.AddDays(-60), 2, a, b);

            ace.getParam(P.vocal).potential = 97;
            ace.getParam(P.dance).potential = 99;

            string line = MomentContext.Audition(new List<data_girls.girls> { ace }, Auditions.type.local).Fact(M, "Ace");
            Assert.Equal("Trait: Prodigy. Age 19. Top vocal skill in the group. Highest potential: dance.", line);
        }

        /// <summary>
        /// Every stat tied for her highest potential is named, by the words the standouts use; sexy counts only
        /// from 16, as in the standouts.
        /// </summary>
        [Theory]
        [InlineData(19, "Highest potential: dance, cute and sexy")]
        [InlineData(15, "Highest potential: dance and cute")]
        public void HighestPotential_NamesEveryTiedStat(int age, string expected)
        {
            data_girls.girls girl = Idol("New", age: age).With(P.vocal, 40).With(P.dance, 40).With(P.cute, 40).With(P.sexy, 40);
            girl.getParam(P.vocal).potential = 70;
            girl.getParam(P.dance).potential = 90;
            girl.getParam(P.cute).potential = 90;
            girl.getParam(P.sexy).potential = 90;
            Assert.Equal(expected, Members.HighestPotential(girl));
        }

        /// <summary>
        /// Without a rolled potential nothing is said, and none is rolled: the game's GetPotential would set one.
        /// </summary>
        [Fact]
        public void HighestPotential_IsLeftOut_WithoutOne()
        {
            data_girls.girls girl = Idol("New").With(P.vocal, 40);
            Assert.Equal("", Members.HighestPotential(girl));
            Assert.Equal(0, girl.getParam(P.vocal).potential);
        }

        /// <summary>
        /// Only newcomers' lines give their potential.
        /// </summary>
        [Fact]
        public void HighestPotential_OnlyInNewcomerLines()
        {
            data_girls.girls girl = Idol("Rin").With(P.vocal, 40);
            girl.getParam(P.vocal).potential = 90;
            Moment moment = new("x");
            Members.Add(moment, new List<data_girls.girls> { girl }, Today, null);
            Assert.DoesNotContain("potential", moment.Fact(M, "Rin"));
        }

        /// <summary>
        /// The idols who joined last before these, all from the same day, graduates too; not these idols.
        /// </summary>
        [Fact]
        public void History_NamesThePreviousNewMembers()
        {
            data_girls.girls yui = Idol("Yui");
            yui.Hiring_Date = Today.AddMonths(-4);
            data_girls.girls rena = Idol("Rena").Graduated(Today.AddMonths(-1));
            rena.Hiring_Date = Today.AddMonths(-4);
            Idol("Founder").Hiring_Date = Today.AddYears(-2);
            data_girls.girls fresh = Idol("Fresh");
            fresh.Hiring_Date = Today;

            Moment moment = MomentContext.Audition(new List<data_girls.girls> { fresh }, Auditions.type.local);
            Assert.Equal("Yui and Rena, 4 months ago", moment.Fact(H, "Previous new members"));
        }

        [Fact]
        public void History_IsLeftOut_WhenNoOneJoinedBefore()
        {
            data_girls.girls fresh = Idol("Fresh");
            fresh.Hiring_Date = Today;
            Moment moment = MomentContext.Audition(new List<data_girls.girls> { fresh }, Auditions.type.local);
            Assert.Empty(moment.Facts(H));
        }
    }
}
