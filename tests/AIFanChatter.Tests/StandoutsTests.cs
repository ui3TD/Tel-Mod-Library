using Xunit;
using static AIFanChatter.Tests.TestGame;
using P = data_girls._paramType;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// An idol's line names the stats where she alone is highest or lowest in the group.
    /// </summary>
    public class StandoutsTests
    {
        private const string M = Moment.MembersSection;

        public StandoutsTests() => TestEnv.Reset();

        private static data_girls.girls Member(string name, float vocal, float dance = 50, float cute = 50) =>
            Idol(name).With(P.vocal, vocal).With(P.dance, dance).With(P.cute, cute)
                .With(P.cool, 50).With(P.pretty, 50).With(P.funny, 50).With(P.smart, 50).With(P.sexy, 50);

        [Fact]
        public void TopAndPoorest_AreNamed_InTheGroup()
        {
            data_girls.girls ace = Member("Ace", vocal: 90, dance: 80, cute: 10);
            data_girls.girls mid = Member("Mid", vocal: 50);
            data_girls.girls low = Member("Low", vocal: 20, dance: 20);
            Group("Stars", Today.AddYears(-2), ace, mid, low);

            Assert.Equal("top vocal and dance skills in the group, but least cute",
                Members.Standouts(ace, ace.GetGroup(), null));
            Assert.Equal("", Members.Standouts(mid, mid.GetGroup(), null));
            Assert.Equal("poorest vocal and dance skills in the group",
                Members.Standouts(low, low.GetGroup(), null));
        }

        private static data_girls.girls AllStats(string name, float value) =>
            Idol(name).With(P.vocal, value).With(P.dance, value).With(P.cute, value).With(P.cool, value)
                .With(P.pretty, value).With(P.funny, value).With(P.smart, value).With(P.sexy, value);

        [Fact]
        public void EveryStatHasItsWording()
        {
            data_girls.girls top = AllStats("Top", 90);
            data_girls.girls bottom = AllStats("Bottom", 10);
            Group("Stars", Today.AddYears(-2), top, bottom, AllStats("Mid", 50));
            Assert.Equal("top vocal and dance skills, cutest, coolest, prettiest, sexiest, funniest and smartest in the group",
                Members.Standouts(top, top.GetGroup(), null));
            Assert.Equal("poorest vocal and dance skills and least cute, cool, pretty, sexy, funny and smart in the group",
                Members.Standouts(bottom, bottom.GetGroup(), null));
        }

        [Fact]
        public void OneSkill_AndMixedStats_ReadAsOnePhrase()
        {
            data_girls.girls a = Idol("A").With(P.vocal, 90).With(P.dance, 50).With(P.cute, 90)
                .With(P.cool, 50).With(P.pretty, 50).With(P.sexy, 50).With(P.funny, 10).With(P.smart, 50);
            Group("Stars", Today.AddYears(-2), a, Member("B", vocal: 50), Member("C", vocal: 40));
            Assert.Equal("top vocal skill and cutest in the group, but least funny", Members.Standouts(a, a.GetGroup(), null));
        }

        [Fact]
        public void ATie_NamesNoOne()
        {
            data_girls.girls a = Member("A", vocal: 90);
            data_girls.girls b = Member("B", vocal: 90);
            Group("Stars", Today.AddYears(-2), a, b, Member("C", vocal: 10));
            Assert.Equal("", Members.Standouts(a, a.GetGroup(), null));
            Assert.Equal("", Members.Standouts(b, b.GetGroup(), null));
        }

        [Fact]
        public void GroupsOfTwo_HaveNoStandouts()
        {
            data_girls.girls a = Member("A", vocal: 90);
            Group("Stars", Today.AddYears(-2), a, Member("B", vocal: 10));
            Assert.Equal("", Members.Standouts(a, a.GetGroup(), null));
        }

        [Fact]
        public void GraduatedMembers_DontCount_ButAGraduateIsComparedWithHerGroup()
        {
            data_girls.girls leaving = Member("Leaving", vocal: 95);
            data_girls.girls a = Member("A", vocal: 80);
            Group("Stars", Today.AddYears(-2), leaving, a, Member("B", vocal: 50), Member("Gone", vocal: 99).Graduated(Today.AddYears(-1)));
            leaving.Graduated(Today);
            Assert.Equal("top vocal skill in the group", Members.Standouts(leaving, leaving.GetGroup(), null));
            Assert.Equal("", Members.Standouts(a, a.GetGroup(), null));
        }

        [Fact]
        public void AnotherGroupsMember_IsComparedInHerOwnGroup_ByName()
        {
            data_girls.girls lead = Member("Lead", vocal: 99);
            data_girls.girls guest = Member("Guest", vocal: 60);
            Groups._group main = Group("Stars", Today.AddYears(-6), lead, Member("S2", vocal: 70), Member("S3", vocal: 80));
            Group("Moons", Today.AddDays(-540), guest, Member("M2", vocal: 30), Member("M3", vocal: 40));
            singles._single single = Single("Song", Today, 1, lead, guest).In(main);

            Moment moment = MomentContext.Single(single);
            Assert.Contains("Top vocal skill in Moons.", moment.Fact(M, "Guest"));
            Assert.Contains("Top vocal skill in the group.", moment.Fact(M, "Lead"));
        }
    }
}
