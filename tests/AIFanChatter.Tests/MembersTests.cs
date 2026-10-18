using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    public class MembersTests
    {
        public MembersTests() => TestEnv.Reset();

        private const string M = Moment.MembersSection;

        /// <summary>
        /// Her line in a moment of her own group.
        /// </summary>
        private static string LineOf(data_girls.girls girl, bool withTrivia = true) =>
            Members.Line(girl, withTrivia, Today, girl.GetGroup(), null, Members.RankIn);

        /// <summary>
        /// Who she is comes first, then how she's doing, then her place in the group. The group's own totals
        /// are in the agency section.
        /// </summary>
        [Fact]
        public void Line_HasEveryItem_InOrder()
        {
            data_girls.girls aya = Idol("Aya", fans: 5000, age: 19).WithTrait("Thick Skin")
                .With(data_girls._paramType.scandalPoints, 2)
                .With(data_girls._paramType.mentalStamina, 30.4f)
                .With(data_girls._paramType.physicalStamina, 45f);
            aya.Trivia.Add("Has a pet spider");
            aya.Hiring_Date = Today.AddYears(-2).AddDays(-5);
            data_girls.girls mei = Idol("Mei", fans: 9000);
            data_girls.girls rin = Idol("Rin", fans: 7000);
            Groups._group group = Group("Stars", Today.AddYears(-6).AddDays(-20), aya, mei, rin);
            Single("One", Today.AddDays(-300), 1, aya, mei).In(group);
            Single("Two", Today.AddDays(-200), 1, mei, aya).In(group);
            Single("Three", Today.AddDays(-100), 1, rin).In(group);
            Election(Today.AddDays(-50), 40, mei, aya, rin);

            Assert.Equal("Trait: Thick Skin. Has a pet spider. A minor scandal in her past. Age 19. Mental stamina 30/100, stamina 45/100. "
                + "A member for 2 years, 2nd in the last election, the fewest fans in the group, in 2 of the group's singles (center of 1).",
                LineOf(aya));
        }

        /// <summary>
        /// Trait titles come in every form, so each reads as a label.
        /// </summary>
        [Theory]
        [InlineData("Prodigy")]
        [InlineData("Clumsy")]
        [InlineData("Anxiety")]
        [InlineData("Live fast")]
        [InlineData("<color=#ff0000>Weak vocal chords</color>")]
        public void Trait_IsALabelledTitle(string title)
        {
            data_girls.girls aya = Idol("Aya").WithTrait(title);
            Assert.Equal("Trait: " + Moment.Clean(title), Members.Trait(aya));
            Assert.StartsWith("Trait: " + Moment.Clean(title) + ". ", LineOf(aya));
        }

        [Fact]
        public void Line_LeavesOutWhatTheGameCantGive()
        {
            data_girls.girls aya = Idol("Aya", age: 22);
            Assert.Equal("Age 22. Mental stamina 0/100, stamina 0/100. A member for 12 months.", LineOf(aya));
        }

        /// <summary>
        /// Stamina shows only at half or below, where fans would see signs, and a scandal record only when
        /// there is one.
        /// </summary>
        [Theory]
        [InlineData(51f, 100f, "Age 19. A member for 12 months.")]
        [InlineData(50f, 100f, "Age 19. Mental stamina 50/100. A member for 12 months.")]
        [InlineData(80f, 25f, "Age 19. Stamina 25/100. A member for 12 months.")]
        public void Stamina_ShowsOnlyWhenLow(float mental, float physical, string expected)
        {
            data_girls.girls aya = Idol("Aya")
                .With(data_girls._paramType.mentalStamina, mental)
                .With(data_girls._paramType.physicalStamina, physical);
            Assert.Equal(expected, LineOf(aya));
        }

        [Fact]
        public void Line_NeverGivesFameOrFanCounts()
        {
            data_girls.girls aya = Idol("Aya", fans: 123456).With(data_girls._paramType.famePoints, 777f);
            Group("Stars", Today.AddYears(-3), aya, Idol("Mei", fans: 10));
            string line = LineOf(aya);
            Assert.DoesNotContain("123456", line);
            Assert.DoesNotContain("fame", line, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("the most fans in the group", line);
        }

        [Fact]
        public void Tenure_CountsFromTheLatestOfHiringTransferAndTheGroupsStart()
        {
            data_girls.girls moved = Idol("Moved");
            moved.Hiring_Date = Today.AddYears(-5);
            moved.TransferDate = Today.AddDays(-90);
            Group("Stars", Today.AddYears(-8));
            Group("Moons", Today.AddYears(-2), moved);
            Assert.Equal("a member for 3 months", Members.Tenure(moved, Today, moved.GetGroup()));

            data_girls.girls founder = Idol("Founder");
            founder.Hiring_Date = Today.AddYears(-9);
            Group("Suns", Today.AddYears(-4).AddDays(-1), founder);
            Assert.Equal("a member for 4 years", Members.Tenure(founder, Today, founder.GetGroup()));
            Assert.Equal("a member of Suns for 4 of its 4 years", Members.Tenure(founder, Today, founder.GetGroup(), "Suns"));
        }

        [Fact]
        public void Election_UsesTheLatestFinishedElection_OrHerOwnLastPlace()
        {
            data_girls.girls aya = Idol("Aya");
            data_girls.girls mei = Idol("Mei");
            Election(Today.AddDays(-400), 10, aya);
            Election(Today.AddDays(-30), 25, mei);
            Assert.Equal("1st in the last election", Members.Election(mei));

            // How many her own last election ranked isn't kept, so only first and second are named
            aya.SSK_History.Add(2);
            Assert.Equal("2nd in her last election", Members.Election(aya));
            aya.SSK_History.Add(4);
            Assert.Equal("", Members.Election(aya));
            Assert.Equal("", Members.Election(Idol("New")));
        }

        /// <summary>
        /// Only the top two and bottom two places of the election are named, out of the idols it ranked.
        /// </summary>
        [Theory]
        [InlineData(1, "1st in the last election")]
        [InlineData(2, "2nd in the last election")]
        [InlineData(3, "")]
        [InlineData(10, "")]
        [InlineData(11, "second from bottom in the last election")]
        [InlineData(12, "bottom in the last election")]
        public void Election_NamesOnlyTheTopAndBottomTwo(int place, string expected)
        {
            data_girls.girls[] ranked = Enumerable.Range(1, 12).Select(i => Idol("Idol" + i)).ToArray();
            Election(Today.AddDays(-30), 12, ranked);
            Assert.Equal(expected, Members.Election(ranked[place - 1]));
        }

        /// <summary>
        /// Results past the idols ranked (place 0) don't count towards the bottom.
        /// </summary>
        [Fact]
        public void Election_BottomIsTheLastRankedPlace()
        {
            data_girls.girls[] ranked = Enumerable.Range(1, 6).Select(i => Idol("Idol" + i)).ToArray();
            SEvent_SSK._SSK election = Election(Today.AddDays(-30), 6, ranked);
            election.Results.Add(new SEvent_SSK._SSK._result { Girl = Idol("Unranked"), Place = 0 });
            Assert.Equal("bottom in the last election", Members.Election(ranked[5]));
        }

        [Fact]
        public void Singles_CountOnlyReleasedOnes()
        {
            data_girls.girls aya = Idol("Aya");
            Groups._group group = Group("Stars", Today.AddYears(-3), aya);
            Single("Out", Today.AddDays(-10), 1, aya).In(group);
            singles._single planned = Single("Planned", Today, 1, aya).In(group);
            planned.status = singles._single._status.working;
            Assert.Equal("in 1 of the group's singles (center of 1)", Members.Singles(aya, group));
        }

        [Theory]
        [InlineData(0, "")]
        [InlineData(1, "A minor scandal in her past")]
        [InlineData(2, "A minor scandal in her past")]
        [InlineData(3, "Several scandals behind her")]
        [InlineData(4, "Several scandals behind her")]
        [InlineData(5, "A serious scandal record")]
        public void ScandalRecord_IsInWords(int points, string expected)
        {
            Assert.Equal(expected, Members.ScandalRecord(points));
        }

        [Theory]
        [InlineData(0, "no scandals")]
        [InlineData(1, "a few scandals")]
        [InlineData(5, "a few scandals")]
        [InlineData(6, "a troubled reputation")]
        [InlineData(11, "a troubled reputation")]
        [InlineData(12, "a badly damaged reputation")]
        [InlineData(19, "a badly damaged reputation")]
        [InlineData(20, "on the brink of collapse from scandals")]
        public void AgencyReputation_IsInWords(long points, string expected)
        {
            Assert.Equal(expected, Members.AgencyReputation(points));
        }

        [Fact]
        public void ScandalNumbers_NeverAppear()
        {
            data_girls.girls aya = Idol("Aya").With(data_girls._paramType.scandalPoints, 7);
            string line = LineOf(aya);
            Assert.Contains("A serious scandal record.", line);
            Assert.DoesNotContain("7", line);
        }

        [Fact]
        public void Trivia_OnlyForTheFirstIdols_AndCut()
        {
            List<data_girls.girls> idols = Enumerable.Range(0, 3).Select(i => Idol("Idol" + i)).ToList();
            foreach (data_girls.girls idol in idols)
            {
                idol.Trivia.Add("Collects " + new string('x', 200));
            }
            Moment moment = new("x");
            Members.Add(moment, idols, Today, null);
            List<string> lines = moment.Facts(M).Select(f => f.Value).ToList();
            string cut = Members.Cut("Collects " + new string('x', 200), Members.TriviaLength);
            Assert.Equal(Members.TriviaLength, cut.Length);
            Assert.EndsWith("...", cut);
            Assert.StartsWith(cut + " Age 19.", lines[0]);
            Assert.StartsWith(cut + " Age 19.", lines[1]);
            Assert.StartsWith("Age 19.", lines[2]);
        }

        [Fact]
        public void Single_FollowsItsFormation_NotFans()
        {
            data_girls.girls center = Idol("Center", fans: 10);
            data_girls.girls front = Idol("Front", fans: 20);
            data_girls.girls back = Idol("Back", fans: 999999);
            singles._single single = Single("Song", Today, 1, center, front, null, back);
            Assert.Equal(new[] { "Center", "Front", "Back" }, Members.ForSingle(single).Select(g => g.nickname));
        }

        [Fact]
        public void Single_KeepsTheFiveFrontMost()
        {
            List<data_girls.girls> idols = Enumerable.Range(0, 10).Select(i => Idol("Idol" + i)).ToList();
            singles._single single = Single("Song", Today, 1, idols.ToArray());
            Assert.Equal(5, Members.SingleCap);
            Assert.Equal(idols.Take(5), Members.ForSingle(single));
        }

        [Fact]
        public void Concert_PutsTheMostFeaturedFirst()
        {
            data_girls.girls lead = Idol("Lead", fans: 1);
            data_girls.girls talker = Idol("Talker", fans: 1);
            data_girls.girls popular = Idol("Popular", fans: 100000);
            data_girls.girls once = Idol("Once", fans: 1);
            SEvent_Concerts._concert concert = Concert("Live", Today, 1);
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = once });
            concert.SetListItems.Add(new SEvent_Concerts._concert._mc { Girls = new List<data_girls.girls> { talker, popular, null } });
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = lead });
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = lead });
            concert.SetListItems.Add(new SEvent_Concerts._concert._mc { Girls = new List<data_girls.girls> { talker, null, null } });
            Assert.Equal(new[] { "Lead", "Once", "Talker", "Popular" }, Members.ForConcert(concert).Select(g => g.nickname));
        }

        [Fact]
        public void Concert_KeepsFive()
        {
            SEvent_Concerts._concert concert = Concert("Live", Today, 1);
            for (int i = 0; i < 8; i++)
            {
                concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = Idol("Idol" + i) });
            }
            Assert.Equal(Members.ConcertCap, Members.ForConcert(concert).Count);
        }

        [Fact]
        public void Event_KeepsItsActorOrder_AndOnlyIdols()
        {
            data_girls.girls subject = Idol("Subject", fans: 1);
            data_girls.girls other = Idol("Other", fans: 99999);
            Event_Manager._activeEvent active = Event("scandal", Today, subject, other);
            active.actors.Insert(1, new Event_Manager._activeEvent._actor { name = "A journalist" });
            Assert.Equal(new[] { "Subject", "Other" }, Members.ForEvent(active).Select(g => g.nickname));
        }

        /// <summary>
        /// Her rank is taken before she leaves, as the Graduate prefix does; the game then moves her fans.
        /// </summary>
        [Fact]
        public void Graduate_KeepsHerRankFromBeforeSheLeft()
        {
            data_girls.girls leaving = Idol("Leaving", fans: 8000);
            data_girls.girls other = Idol("Other", fans: 1000);
            Group("Stars", Today.AddYears(-3), leaving, other);
            leaving.Graduation_Trivia_Text = "Got a job as a broadcast anchor";

            Members.Rank before = Members.RankOf(leaving);
            leaving.Graduated(Today);
            other.Fans[0].people += 8000;
            leaving.Fans[0].people = 0;

            Moment moment = MomentContext.Graduation(leaving, before);
            Assert.Contains("had the most fans of 2 in the group", moment.Fact(M, "Leaving"));
            Assert.Equal("Got a job as a broadcast anchor", moment.Fact(Moment.FactsSection, "After graduating"));
            // After she left, and labelled so, where other moments say "Members in the group"
            Assert.Equal("1", moment.Fact(Moment.AgencySection, "Members left in the group"));
            Assert.Null(moment.Fact(Moment.AgencySection, "Members in the group"));
            Assert.DoesNotContain(moment.Facts, f => f.Key == "Her fans" || f.Key == "Her fame level" || f.Key == "Years in the group");
        }

        [Fact]
        public void Graduate_WithoutTrivia_LeavesItOut()
        {
            data_girls.girls leaving = Idol("Leaving").Graduated(Today);
            Moment moment = MomentContext.Graduation(leaving, null);
            Assert.Null(moment.Fact(Moment.FactsSection, "After graduating"));
        }

        /// <summary>
        /// "The group" is the event's group. A sister group's member in a main single has her time and rank
        /// given in her own group, by name, and her singles counted in the main group.
        /// </summary>
        [Fact]
        public void IdolFromAnotherGroup_IsDescribedInHerOwnGroup_ByName()
        {
            data_girls.girls lead = Idol("Lead", fans: 5000);
            data_girls.girls guest = Idol("Guest", fans: 100);
            data_girls.girls mate = Idol("Mate", fans: 50);
            guest.Hiring_Date = Today.AddYears(-3);
            Groups._group main = Group("Stars", Today.AddYears(-6), lead, Idol("Back", fans: 10));
            Group("Moons", Today.AddDays(-540), guest, mate);
            singles._single single = Single("Song", Today, 1, lead, guest).In(main);

            Moment moment = MomentContext.Single(single);
            Assert.EndsWith("A member of Moons for 18 of its 18 months, the most fans of 2 in Moons, in 1 of the group's singles.", moment.Fact(M, "Guest"));
            Assert.EndsWith("A member for 12 months, the most fans in the group, in 1 of the group's singles (center of 1).", moment.Fact(M, "Lead"));
            Assert.Null(moment.Fact(Moment.AgencySection, "Sister group of"));
        }

        [Fact]
        public void SisterGroupsSingle_IsAboutThatGroup()
        {
            data_girls.girls a = Idol("A", fans: 10);
            data_girls.girls b = Idol("B", fans: 20);
            Groups._group main = Group("Stars", Today.AddYears(-6), Idol("Main", fans: 999));
            Groups._group sister = Group("Moons", Today.AddYears(-1), a, b);
            singles._single single = Single("Song", Today, 1, a, b).In(sister);

            Moment moment = MomentContext.Single(single);
            Assert.Equal("Moons", moment.Fact(Moment.AgencySection, "Group"));
            Assert.Equal(Members.GroupName(main), moment.Fact(Moment.AgencySection, "Sister group of"));
            Assert.Equal("2", moment.Fact(Moment.AgencySection, "Members in the group"));
            Assert.Equal("B, A", moment.Fact(Moment.AgencySection, "Most popular members"));
            Assert.Contains("the fewest fans in the group", moment.Fact(M, "A"));
        }

        [Fact]
        public void Event_IsAboutItsMainSubjectsGroup()
        {
            Group("Stars", Today.AddYears(-6), Idol("Main"));
            data_girls.girls subject = Idol("Subject", fans: 5000);
            Group("Moons", Today.AddYears(-1), subject, Idol("Mate", fans: 10));
            Moment moment = MomentContext.Event(Event("scandal", Today, subject), new Event_Manager._randomEvent._reply());
            Assert.Equal("Moons", moment.Fact(Moment.AgencySection, "Group"));
            Assert.Contains("the most fans in the group", moment.Fact(M, "Subject"));
        }

        /// <summary>
        /// Fame isn't sent anywhere; the date names its day of the week.
        /// </summary>
        [Fact]
        public void Agency_HasNoFame_AndADateWithItsWeekday()
        {
            Moment moment = MomentContext.Single(Single("Song", Today, 1));
            Assert.DoesNotContain(moment.Facts(Moment.AgencySection), f => f.Key.IndexOf("fame", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain("fame", moment.Describe(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Sunday 24 March 2024", moment.Fact(Moment.AgencySection, "Date"));
        }

        [Fact]
        public void NameLists_SayHowManyAreLeftOut()
        {
            List<data_girls.girls> idols = Enumerable.Range(0, 10).Select(i => Idol("Idol" + i)).ToList();
            Assert.Equal("Idol0, Idol1, Idol2 and 7 more", MomentContext.Names(idols, 3));
            Assert.Equal("Idol0, Idol1", MomentContext.Names(idols.Take(2), 3));
        }

        [Fact]
        public void Agency_GivesTheGroupsSize_AndItsMostPopular()
        {
            data_girls.girls[] idols = Enumerable.Range(0, 5).Select(i => Idol("Idol" + i, fans: i * 100)).ToArray();
            Group("Stars", Today.AddYears(-3), idols);
            idols[0].Graduated(Today.AddDays(-5));
            singles._single single = Single("Song", Today, 1, idols[1]);
            Moment moment = MomentContext.Single(single);
            Assert.Equal("4", moment.Fact(Moment.AgencySection, "Members in the group"));
            Assert.Equal("Idol4, Idol3, Idol2", moment.Fact(Moment.AgencySection, "Most popular members"));
        }

        /// <summary>
        /// The group's totals that the idol lines leave out: its age, its singles and the election's size.
        /// </summary>
        [Fact]
        public void Agency_GivesTheGroupsTotals_ForTheIdolLines()
        {
            data_girls.girls aya = Idol("Aya");
            Groups._group stars = Group("Stars", Today.AddYears(-3).AddDays(-10), aya);
            Single("Old", Today.AddDays(-90), 1, aya).In(stars);
            singles._single single = Single("Song", Today, 1, aya).In(stars);
            Moment before = MomentContext.Single(single);
            Assert.Equal("3 years ago", before.Fact(Moment.AgencySection, "Group formed"));
            Assert.Equal("2", before.Fact(Moment.AgencySection, "Singles released by the group"));

            // The election's size isn't sent: an idol's place says first, second, second from bottom or bottom
            Election(Today.AddDays(-20), 12, aya);
            Moment after = MomentContext.Single(single);
            Assert.DoesNotContain(after.Facts(Moment.AgencySection), f => f.Key.Contains("election"));
            Assert.Contains("1st in the last election", after.Fact(M, "Aya"));
        }

        /// <summary>
        /// A huge roster doesn't make the message grow: only the moment's idols get lines, and lists are cut.
        /// </summary>
        [Fact]
        public void LargeGroups_KeepTheMessageSmall()
        {
            string Message(int size)
            {
                TestEnv.Reset();
                List<data_girls.girls> idols = Enumerable.Range(0, size).Select(i => Idol("Member" + i, fans: i)).ToList();
                foreach (data_girls.girls idol in idols)
                {
                    idol.Trivia.Add("Likes long walks on the beach and quiet evenings at home");
                }
                Groups._group group = Group("Stars", Today.AddYears(-5), idols.ToArray());
                singles._single single = Single("Song", Today, 1, idols.Take(16).ToArray()).In(group);
                return MomentContext.Single(single).Describe();
            }
            string small = Message(16);
            string large = Message(200);
            Assert.InRange(large.Length - small.Length, 0, 200);
            Assert.Equal(Members.SingleCap, large.Split('\n').Count(l => Regex.IsMatch(l, @"^Member\d+: ")));
        }

        [Theory]
        [InlineData(1, 5, false, null, "the most fans in the group")]
        [InlineData(2, 5, false, null, "the second most fans in the group")]
        [InlineData(3, 5, false, null, "")]
        [InlineData(4, 5, false, null, "the second fewest fans in the group")]
        [InlineData(5, 5, false, null, "the fewest fans in the group")]
        [InlineData(1, 5, true, null, "had the most fans of 5 in the group")]
        [InlineData(4, 5, true, null, "had the second fewest fans of 5 in the group")]
        [InlineData(2, 5, false, "Moons", "the second most fans of 5 in Moons")]
        [InlineData(5, 5, false, "Moons", "the fewest fans of 5 in Moons")]
        public void Rank_ReadsNaturally(int place, int of, bool past, string group, string expected)
        {
            Assert.Equal(expected, Members.RankText(new Members.Rank { Place = place, Of = of, Group = group }, past));
        }

        /// <summary>
        /// First and last are named from 2 idols; second and second to last only from 5, as in smaller groups
        /// they are the middle places. Alone, an idol has no place.
        /// </summary>
        [Theory]
        [InlineData(1, 1, Members.Standing.None)]
        [InlineData(1, 2, Members.Standing.First)]
        [InlineData(2, 2, Members.Standing.Last)]
        [InlineData(2, 3, Members.Standing.None)]
        [InlineData(3, 3, Members.Standing.Last)]
        [InlineData(2, 4, Members.Standing.None)]
        [InlineData(3, 4, Members.Standing.None)]
        [InlineData(4, 4, Members.Standing.Last)]
        [InlineData(2, 5, Members.Standing.Second)]
        [InlineData(3, 5, Members.Standing.None)]
        [InlineData(4, 5, Members.Standing.SecondLast)]
        [InlineData(5, 5, Members.Standing.Last)]
        [InlineData(0, 5, Members.Standing.None)]
        [InlineData(6, 5, Members.Standing.None)]
        public void Standing_NamesOnlyTheEnds(int place, int of, Members.Standing expected)
        {
            Assert.Equal(expected, Members.StandingOf(place, of));
        }

        [Fact]
        public void IdolAlone_HasNoPlaceByFans()
        {
            data_girls.girls solo = Idol("Solo", fans: 900);
            Group("Solo", Today.AddYears(-1), solo);
            Assert.DoesNotContain("fans", LineOf(solo));
        }

        [Fact]
        public void SmallGroup_NamesOnlyTheMostAndFewestFans()
        {
            data_girls.girls most = Idol("Most", fans: 900);
            data_girls.girls middle = Idol("Middle", fans: 500);
            data_girls.girls fewest = Idol("Fewest", fans: 100);
            Groups._group group = Group("Trio", Today.AddYears(-1), most, middle, fewest);
            singles._single single = Single("Song", Today, 1, middle, most, fewest).In(group);
            Moment moment = MomentContext.Single(single);
            Assert.Contains("the most fans in the group", moment.Fact(M, "Most"));
            Assert.DoesNotContain("fans in the group", moment.Fact(M, "Middle"));
            Assert.Contains("the fewest fans in the group", moment.Fact(M, "Fewest"));
        }
    }
}
