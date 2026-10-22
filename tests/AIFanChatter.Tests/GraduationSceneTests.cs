using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// A regular graduation waits for its goodbye scene to end, so its posts can say who joined in her place
    /// and whether she stays at the agency as staff.
    /// </summary>
    public class GraduationSceneTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string A = Moment.AgencySection;

        private readonly List<staff._staff> staffBefore = staff.Staff.ToList();
        private readonly List<Func<Moment>> sent = new();
        private readonly data_dialogues._dialogue goodbye = new();
        private readonly string groupNameBefore = staticVars.PlayerData.GroupName;

        public GraduationSceneTests()
        {
            TestEnv.Reset();
            staff.Staff.Clear();
            GraduationScene.Send = sent.Add;
            GraduationScene.CurrentDialogue = () => goodbye;
            Language.Data["STAFF__CHOREO"] = "Choreographer";
            staticVars.PlayerData.GroupName = "Stars";
        }

        public void Dispose()
        {
            GraduationScene.Send = Moments.Trigger;
            GraduationScene.CurrentDialogue = () => null;
            GraduationScene.Clear();
            ActiveDialogueController.ShowingDialogue = false;
            staff.Staff.Clear();
            staff.Staff.AddRange(staffBefore);
            Language.Data.Remove("STAFF__CHOREO");
            staticVars.PlayerData.GroupName = groupNameBefore;
        }

        /// <summary>The game's Graduate, with or without the goodbye scene.</summary>
        private static void Graduate(data_girls.girls girl, bool scene)
        {
            data_girls_girls_Graduate.Prefix(girl, out data_girls_girls_Graduate.Before before);
            girl.status = data_girls._status.graduated;
            girl.Graduation_Date = Today;
            data_girls_girls_Graduate.Postfix(girl, scene, before, true);
        }

        /// <summary>The end of a dialogue, as the game's dialogue controller ends it.</summary>
        private static void Hide(data_dialogues._dialogue dialogue)
        {
            ActiveDialogueController controller = (ActiveDialogueController)FormatterServices.GetUninitializedObject(typeof(ActiveDialogueController));
            controller.dialogue = dialogue;
            ActiveDialogueController_Hide.Postfix(controller);
        }

        /// <summary>The game's Find_Successor: the new idol joins the main group.</summary>
        private static data_girls.girls FindSuccessor(data_girls.girls graduate, bool sister, string name = "Hana", bool ran = true)
        {
            Date_Graduation_Find_Successor.Prefix(out int count);
            data_girls.girls successor = ran ? Idol(name, fans: 0, age: 14) : null;
            if (ran)
            {
                successor.Hiring_Date = Today;
                Groups.Groups_[0].Girls.Add(successor);
            }
            Date_Graduation_Find_Successor.Postfix(graduate, sister, count, ran);
            return successor;
        }

        /// <summary>The game's Hire_As_Staffer: a staffer with her name, a choreographer by her dance.</summary>
        private static void HireAsStaffer(data_girls.girls graduate, bool ran = true)
        {
            Date_Graduation_Hire_As_Staffer.Prefix(out int count);
            if (ran)
            {
                staff.Staff.Add(new staff._staff
                {
                    nickname = graduate.nickname,
                    type = staff._type.choreographer,
                    skills = new List<staff._staff._skill> { new() { skill_type = staff._skill_type.production, level = 2, primary = true } },
                });
            }
            Date_Graduation_Hire_As_Staffer.Postfix(graduate, count, ran);
        }

        private Moment Sent()
        {
            Assert.Single(sent);
            return sent[0]();
        }

        private static data_girls.girls Graduate(params string[] others)
        {
            data_girls.girls kaho = Idol("Kaho", age: 21);
            Group("Stars", Today.AddYears(-2), new[] { kaho }.Concat(others.Select(o => Idol(o))).ToArray());
            return kaho;
        }

        [Fact]
        public void WithoutTheScene_PostsAtOnce()
        {
            Graduate(Graduate("Rin", "Aoi"), scene: false);
            Assert.Single(sent);
            Assert.Null(GraduationScene.Pending);
        }

        /// <summary>
        /// With the scene, nothing is sent until that scene ends; another dialogue ending changes nothing, and
        /// the graduation is sent once.
        /// </summary>
        [Fact]
        public void WithTheScene_PostsOnceWhenItEnds()
        {
            data_girls.girls kaho = Graduate("Rin", "Aoi");
            Graduate(kaho, scene: true);
            Assert.Empty(sent);

            Hide(new data_dialogues._dialogue());
            Assert.Empty(sent);

            Hide(goodbye);
            Moment moment = Sent();
            Assert.Equal("Kaho's graduation", moment.Label);
            Assert.Null(moment.Fact(F, "Joined in her place"));
            Assert.Null(moment.Fact(F, "Stays at the agency as"));

            Hide(goodbye);
            Assert.Single(sent);
        }

        /// <summary>
        /// Her younger sister or her chosen successor, in the facts and with a newcomer's line after hers; the
        /// members left don't count her.
        /// </summary>
        [Theory]
        [InlineData(true, "Hana, her younger sister")]
        [InlineData(false, "Hana, the successor she chose")]
        public void Successor_JoinsThePosts(bool sister, string expected)
        {
            data_girls.girls kaho = Graduate("Rin", "Aoi", "Mei");
            Graduate(kaho, scene: true);
            FindSuccessor(kaho, sister);
            Hide(goodbye);

            Moment moment = Sent();
            Assert.Equal(expected, moment.Fact(F, "Joined in her place"));
            Assert.Equal(new[] { "Kaho", "Hana" }, moment.Facts(M).Select(f => f.Key));
            string line = moment.Fact(M, "Hana");
            Assert.Contains("Age 14, youngest in the group.", line);
            Assert.DoesNotContain("member for", line);
            // She joined the group her sister left, so its size didn't change
            Assert.DoesNotContain(moment.Facts(A), f => f.Key.StartsWith("Members in", StringComparison.Ordinal));
        }

        /// <summary>
        /// The game puts her in the main group, so for a sister group's graduate the group she joined is named.
        /// </summary>
        [Fact]
        public void Successor_OfASisterGroupsGraduate_NamesTheMainGroup()
        {
            Group("Stars", Today.AddYears(-2), Idol("Rin"), Idol("Aoi"));
            data_girls.girls kaho = Idol("Kaho");
            Group("Moons", Today.AddYears(-1), kaho, Idol("Mio"), Idol("Sora"));
            Graduate(kaho, scene: true);
            FindSuccessor(kaho, sister: false);
            Hide(goodbye);

            Moment moment = Sent();
            Assert.Equal("Hana, the successor she chose, in the main group Stars", moment.Fact(F, "Joined in her place"));
            // One line per group whose size changed, hers first
            Assert.Equal(new[] { "Members in Moons", "Members in Stars" },
                moment.Facts(A).Where(f => f.Key.StartsWith("Members in", StringComparison.Ordinal)).Select(f => f.Key));
            Assert.Equal("2 now, down from 3", moment.Fact(A, "Members in Moons"));
            Assert.Equal("3 now, up from 2", moment.Fact(A, "Members in Stars"));
        }

        /// <summary>
        /// Her staff job, by title and what she does, as for a firing.
        /// </summary>
        [Fact]
        public void StaffJob_JoinsThePosts()
        {
            data_girls.girls kaho = Graduate("Rin", "Aoi");
            Graduate(kaho, scene: true);
            HireAsStaffer(kaho);
            Hide(goodbye);

            Assert.Equal("Choreographer (production staff)", Sent().Fact(F, "Stays at the agency as"));
        }

        /// <summary>
        /// A successor or staffer for another idol, or one the game didn't hire, isn't recorded.
        /// </summary>
        [Fact]
        public void OtherHires_AreNotRecorded()
        {
            data_girls.girls kaho = Graduate("Rin", "Aoi");
            data_girls.girls rin = data_girls.girl.First(g => g.nickname == "Rin");
            Graduate(kaho, scene: true);
            FindSuccessor(rin, sister: true, name: "Other");
            FindSuccessor(kaho, sister: true, ran: false);
            HireAsStaffer(rin);
            HireAsStaffer(kaho, ran: false);
            Hide(goodbye);

            Moment moment = Sent();
            Assert.Null(moment.Fact(F, "Joined in her place"));
            Assert.Null(moment.Fact(F, "Stays at the agency as"));
        }

        /// <summary>
        /// A scene that never ended the usual way: the first clock tick with no dialogue showing sends it.
        /// </summary>
        [Fact]
        public void ClockTick_SendsAWaitingGraduation_OnceNoDialogueShows()
        {
            Graduate(Graduate("Rin", "Aoi"), scene: true);
            ActiveDialogueController.ShowingDialogue = true;
            ActiveDialogueController_Hide.OnTimeTick();
            Assert.Empty(sent);

            ActiveDialogueController.ShowingDialogue = false;
            ActiveDialogueController_Hide.OnTimeTick();
            Assert.Single(sent);
            Assert.Null(GraduationScene.Pending);
        }

        /// <summary>
        /// A new regular graduation sends the one still waiting first.
        /// </summary>
        [Fact]
        public void NextGraduation_SendsTheWaitingOneFirst()
        {
            data_girls.girls kaho = Graduate("Rin", "Aoi", "Mei");
            Graduate(kaho, scene: true);
            staticVars.dateTime = Today.AddDays(1);
            Graduate(data_girls.girl.First(g => g.nickname == "Rin"), scene: true);

            Assert.Single(sent);
            Assert.Equal("Kaho's graduation", sent[0]().Label);
            Assert.Equal("Rin", GraduationScene.Pending.Girl.nickname);
        }
    }
}
