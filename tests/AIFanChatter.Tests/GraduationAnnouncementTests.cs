using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// An idol's graduation announcement has the graduation's facts about her, but reads as news of a
    /// departure 3 months ahead; the graduation itself says whether fans had that notice.
    /// </summary>
    public class GraduationAnnouncementTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string H = Moment.HistorySection;
        private const string A = Moment.AgencySection;

        private readonly List<Func<Moment>> sent = new();

        public GraduationAnnouncementTests()
        {
            TestEnv.Reset();
            GraduationScene.Send = sent.Add;
            GraduationScene.CurrentDialogue = () => null;
            Date_Popup_OnClick_Fire_Immediately.Firing = null;
        }

        public void Dispose()
        {
            GraduationScene.Send = Moments.Trigger;
            GraduationScene.Clear();
            Date_Popup_OnClick_Fire_Immediately.Firing = null;
        }

        /// <summary>
        /// Kaho in Stars with Rin and Aoi, center of one single, friends with Rin, bullying Aoi, and an earlier
        /// graduate; her announcement as the game confirms it, 3 months ahead.
        /// </summary>
        private static data_girls.girls Kaho()
        {
            data_girls.girls kaho = Idol("Kaho", age: 21);
            data_girls.girls rin = Idol("Rin");
            data_girls.girls aoi = Idol("Aoi");
            data_girls.girls mio = Idol("Mio").Graduated(Today.AddMonths(-5));
            Group("Stars", Today.AddYears(-2), kaho, rin, aoi, mio);
            Single("Bloom", Today.AddDays(-40), 3000, kaho, rin).Styled("Pop", "Love", "Cute");
            Relationships._clique clique = Clique(kaho, rin);
            clique.Bullied_Girls.Add(aoi);
            clique.KnownBulliedGirls.Add(aoi);
            return kaho;
        }

        private static void Announce(data_girls.girls girl)
        {
            girl.Graduation_Date = Today.AddMonths(3);
            girl.status = data_girls._status.announced_graduation;
        }

        [Fact]
        public void Announcement_SaysSheLeavesIn3Months_AndStaysUntilThen()
        {
            data_girls.girls kaho = Kaho();
            Announce(kaho);

            Moment moment = MomentContext.GraduationAnnouncement(kaho);
            Assert.Equal("an idol announced her graduation: she leaves the group in 3 months and stays a member until then", moment.Kind);
            Assert.Equal("Kaho's graduation announcement", moment.Label);
            Assert.Equal("Monday 24 June 2024, 3 months from now", moment.Fact(F, "Graduation date"));
        }

        /// <summary>
        /// The facts about her are the graduation's: her line, the singles she centered, the bullying fans
        /// suspect, her friends by group and the earlier graduates.
        /// </summary>
        [Fact]
        public void Announcement_HasTheGraduationsFactsAboutHer()
        {
            data_girls.girls kaho = Kaho();
            Announce(kaho);
            Moment announced = MomentContext.GraduationAnnouncement(kaho);
            Moment graduated = MomentContext.Graduation(kaho, null);

            foreach (string name in new[] { "Idol", "Suspected to be bullied by", "Suspected to have bullied", "Singles she was the center of" })
            {
                Assert.Equal(graduated.Fact(F, name), announced.Fact(F, name));
            }
            Assert.Equal("Aoi", announced.Fact(F, "Suspected to have bullied"));
            Assert.Contains("\"Bloom\"", announced.Fact(F, "Singles she was the center of"));
            Assert.Equal("Rin", announced.Fact(A, "Friends in " + Members.GroupName(kaho.GetGroup())));
            Assert.Null(announced.Fact(A, "Most popular members"));
            Assert.Equal(graduated.Lines(H), announced.Lines(H));
            Assert.Equal("Mio", announced.Fact(H, "Previous graduate"));
            Assert.Contains("Kaho", announced.Lines(M));
        }

        /// <summary>
        /// Nothing that exists only once she has left: what she does after, how she left, and the group's
        /// smaller size.
        /// </summary>
        [Fact]
        public void Announcement_LeavesOutWhatOnlyFollowsTheGraduation()
        {
            data_girls.girls kaho = Kaho();
            kaho.Graduation_Trivia_Text = "She opened a bakery.";
            Announce(kaho);

            Moment moment = MomentContext.GraduationAnnouncement(kaho);
            foreach (string name in new[] { "After graduating", "Stays at the agency as", "Joined in her place", "How she left", "Announced her graduation" })
            {
                Assert.Null(moment.Fact(F, name));
            }
            Assert.DoesNotContain(moment.Facts(A), f => f.Key.StartsWith("Members in "));
        }

        [Fact]
        public void Announcement_WithAnUnknownDate_LeavesTheDateOut()
        {
            data_girls.girls kaho = Kaho();
            kaho.status = data_girls._status.announced_graduation;
            kaho.Graduation_Date = new DateTime(1900, 1, 1);

            Assert.Null(MomentContext.GraduationAnnouncement(kaho).Fact(F, "Graduation date"));
        }

        [Theory]
        [InlineData(data_girls._status.normal, data_girls._status.announced_graduation, true, true)]
        [InlineData(data_girls._status.injured, data_girls._status.announced_graduation, true, true)]
        // Already announced: a second confirmation isn't news
        [InlineData(data_girls._status.announced_graduation, data_girls._status.announced_graduation, true, false)]
        [InlineData(data_girls._status.graduated, data_girls._status.announced_graduation, true, false)]
        // Another mod skipped the game's method, or kept her
        [InlineData(data_girls._status.normal, data_girls._status.announced_graduation, false, false)]
        [InlineData(data_girls._status.normal, data_girls._status.normal, true, false)]
        public void Announcement_PostsOnlyWhenSheJustAnnounced(data_girls._status before, data_girls._status after, bool ran, bool posts)
        {
            data_girls.girls kaho = Idol("Kaho");
            kaho.status = before;
            data_girls_girls_Graduation_Announce_Confirm.Prefix(kaho, out data_girls._status state);
            kaho.status = after;

            Assert.Equal(posts, data_girls_girls_Graduation_Announce_Confirm.ShouldPost(state, kaho, ran));
            Assert.False(data_girls_girls_Graduation_Announce_Confirm.ShouldPost(state, null, ran));
        }

        /// <summary>The game's Graduate without a goodbye scene, through the mod's patch.</summary>
        private Moment Graduate(data_girls.girls girl)
        {
            data_girls_girls_Graduate.Prefix(girl, out data_girls_girls_Graduate.Before before);
            girl.Graduated(Today);
            data_girls_girls_Graduate.Postfix(girl, false, before, true);
            Assert.Single(sent);
            return sent[0]();
        }

        /// <summary>The date popup's Fire now, which graduates her itself; throws as a broken click would.</summary>
        private Moment FireNow(data_girls.girls girl, bool throws = false)
        {
            Date_Popup popup = (Date_Popup)FormatterServices.GetUninitializedObject(typeof(Date_Popup));
            popup.Girl = girl;
            Date_Popup_OnClick_Fire_Immediately.Prefix(popup);
            Moment moment = throws ? null : Graduate(girl);
            Exception error = throws ? new InvalidOperationException() : null;
            Assert.Same(error, Date_Popup_OnClick_Fire_Immediately.Finalizer(error));
            return moment;
        }

        [Fact]
        public void Graduation_AfterAnAnnouncement_SaysWhenFansHeard()
        {
            data_girls.girls kaho = Kaho();
            kaho.status = data_girls._status.announced_graduation;
            kaho.Graduation_Date = Today;

            Moment moment = Graduate(kaho);
            Assert.Equal("3 months ago", moment.Fact(F, "Announced her graduation"));
            Assert.Null(moment.Fact(F, "How she left"));
            // Right after her name
            Assert.Equal(new[] { "Idol", "Announced her graduation" }, moment.Facts(F).Take(2).Select(f => f.Key));
        }

        [Fact]
        public void Graduation_AfterAnAnnouncementWithAnUnknownDate_SaysBeforehand()
        {
            data_girls.girls kaho = Kaho();
            kaho.status = data_girls._status.announced_graduation;
            kaho.Graduation_Date = new DateTime(1900, 1, 1);

            Assert.Equal("beforehand", Graduate(kaho).Fact(F, "Announced her graduation"));
        }

        [Fact]
        public void Graduation_FiredByTheProducer_SaysTheAgencyDismissedHer()
        {
            Moment moment = FireNow(Kaho());
            Assert.Equal("dismissed by the agency with immediate effect, with no announcement beforehand", moment.Fact(F, "How she left"));
            Assert.Null(moment.Fact(F, "Announced her graduation"));
            Assert.Null(Date_Popup_OnClick_Fire_Immediately.Firing);
        }

        /// <summary>A story, a quit or bankruptcy: no announcement, and not the producer's firing.</summary>
        [Fact]
        public void Graduation_WithoutNotice_SaysSheLeftAtOnce()
        {
            Moment moment = Graduate(Kaho());
            Assert.Equal("with immediate effect, with no announcement beforehand", moment.Fact(F, "How she left"));
            Assert.Equal(new[] { "Idol", "How she left" }, moment.Facts(F).Take(2).Select(f => f.Key));
        }

        /// <summary>A click that failed, or one that graduated someone else, marks no later graduation as a firing.</summary>
        [Fact]
        public void Graduation_AfterAFailedClick_OrOfAnotherIdol_IsNotAFiring()
        {
            data_girls.girls kaho = Kaho();
            FireNow(kaho, throws: true);
            Assert.Null(Date_Popup_OnClick_Fire_Immediately.Firing);

            Date_Popup popup = (Date_Popup)FormatterServices.GetUninitializedObject(typeof(Date_Popup));
            popup.Girl = Idol("Other");
            Date_Popup_OnClick_Fire_Immediately.Prefix(popup);
            Assert.Equal("with immediate effect, with no announcement beforehand", Graduate(kaho).Fact(F, "How she left"));
        }

        /// <summary>A goodbye scene follows only an announcement, and its graduation says so.</summary>
        [Fact]
        public void Graduation_AfterItsGoodbyeScene_SaysWhenFansHeard()
        {
            data_dialogues._dialogue goodbye = new();
            GraduationScene.CurrentDialogue = () => goodbye;
            data_girls.girls kaho = Kaho();
            kaho.status = data_girls._status.announced_graduation;
            kaho.Graduation_Date = Today;
            data_girls_girls_Graduate.Prefix(kaho, out data_girls_girls_Graduate.Before before);
            kaho.Graduated(Today);
            data_girls_girls_Graduate.Postfix(kaho, true, before, true);
            GraduationScene.Post(GraduationScene.Take());

            Assert.Equal("3 months ago", Assert.Single(sent)().Fact(F, "Announced her graduation"));
        }
    }
}
