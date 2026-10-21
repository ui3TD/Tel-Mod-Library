using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// Staffers the player fires through the game's firing flow, with scandals blamed on them or budget
    /// problems announced.
    /// </summary>
    public class StaffFiringTests : IDisposable
    {
        private const string F = Moment.FactsSection;

        private readonly List<staff._staff> staffBefore = staff.Staff.ToList();
        private readonly bool acceptedBefore = Staff_Fire.Accepted;
        private readonly bool severanceBefore = Staff_Fire.IsSeverance;

        public StaffFiringTests()
        {
            TestEnv.Reset();
            staff.Staff.Clear();
            Language.Data["STAFF__CHOREO"] = "Choreographer";
            Language.Data["STAFF__DANCE_COACH"] = "Dance Coach";
            Language.Data["STAFF__SALES_MANAGER"] = "Sales Manager";
            Language.Data["STAFF__PHYSICIAN"] = "Physician";
        }

        public void Dispose()
        {
            staff.Staff.Clear();
            staff.Staff.AddRange(staffBefore);
            Staff_Fire.Accepted = acceptedBefore;
            Staff_Fire.IsSeverance = severanceBefore;
            foreach (string key in new[] { "STAFF__CHOREO", "STAFF__DANCE_COACH", "STAFF__SALES_MANAGER", "STAFF__PHYSICIAN" })
            {
                Language.Data.Remove(key);
            }
        }

        /// <summary>
        /// A staffer with a professional's starting skills (staff.cs): the role's main skill at 4, another at 1.
        /// </summary>
        private static staff._staff Staffer(staff._type type, staff._skill_type main, staff._skill_type other, DateTime hired, int mainLevel = 4, int otherLevel = 1)
        {
            staff._staff staffer = new()
            {
                nickname = "Kenji",
                type = type,
                HireDate = hired,
                skills = new List<staff._staff._skill>
                {
                    new() { skill_type = main, level = mainLevel, primary = true },
                    new() { skill_type = other, level = otherLevel },
                },
            };
            staff.Staff.Add(staffer);
            return staffer;
        }

        private static staff._staff Choreographer(DateTime hired) =>
            Staffer(staff._type.choreographer, staff._skill_type.production, staff._skill_type.coaching, hired);

        /// <summary>The game's Fire: the staffer leaves the staff list.</summary>
        private static (bool post, staff_staff_Fire.Before before) Fire(staff._staff staffer, bool ran = true, bool removed = true)
        {
            staff_staff_Fire.Prefix(staffer, out staff_staff_Fire.Before before);
            if (removed)
            {
                staff.Staff.Remove(staffer);
            }
            return (staff_staff_Fire.ShouldPost(before, staffer, ran), before);
        }

        /// <summary>
        /// A short firing (2 points or fewer) goes straight to the popup and posts as blamed.
        /// </summary>
        [Fact]
        public void ShortFiring_Posts_AsBlamed()
        {
            staff._staff staffer = Choreographer(Today.AddMonths(-3));
            Staff_Fire_Fire.Postfix(staffer);
            var (post, before) = Fire(staffer);
            Assert.True(post);
            Assert.False(before.Budget);
        }

        /// <summary>
        /// After the talk (more than 2 points): accepting the staffer's budget deal announces budget problems;
        /// paying severance or declining blames the staffer.
        /// </summary>
        [Theory]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)]
        public void FiringAfterTheTalk_Posts(bool accepted, bool severance, bool budget)
        {
            staff._staff staffer = Choreographer(Today.AddYears(-3));
            Staff_Fire_Fire.Postfix(staffer);
            Staff_Fire.Accepted = accepted;
            Staff_Fire.IsSeverance = severance;
            Staff_Fire_DoComplete.Prefix();
            var (post, before) = Fire(staffer);
            Assert.True(post);
            Assert.Equal(budget, before.Budget);
        }

        /// <summary>
        /// Story firings (without the game's firing flow), a cancelled popup, a skipped method, or a staffer
        /// who stays get no posts.
        /// </summary>
        [Fact]
        public void OtherFirings_DoNotPost()
        {
            staff._staff story = Choreographer(Today.AddYears(-1));
            Assert.False(Fire(story).post);

            staff._staff cancelled = Choreographer(Today.AddYears(-1));
            Staff_Fire_Fire.Postfix(cancelled);
            ScandalPoints_Popup_OnCancel.Postfix();
            Assert.False(Fire(cancelled).post);

            staff._staff skipped = Choreographer(Today.AddYears(-1));
            Staff_Fire_Fire.Postfix(skipped);
            Assert.False(Fire(skipped, ran: false).post);

            staff._staff stays = Choreographer(Today.AddYears(-1));
            Staff_Fire_Fire.Postfix(stays);
            Assert.False(Fire(stays, removed: false).post);
        }

        /// <summary>
        /// The role with what the staffer mostly did by their strongest skill; skills grow, so a dance coach
        /// can become production staff. On a tie, the role's main skill.
        /// </summary>
        [Theory]
        [InlineData(staff._type.choreographer, staff._skill_type.production, staff._skill_type.coaching, 4, 1, "Choreographer (production staff)")]
        [InlineData(staff._type.dance_coach, staff._skill_type.coaching, staff._skill_type.production, 4, 1, "Dance Coach (trainer)")]
        [InlineData(staff._type.dance_coach, staff._skill_type.coaching, staff._skill_type.production, 3, 5, "Dance Coach (production staff)")]
        [InlineData(staff._type.dance_coach, staff._skill_type.coaching, staff._skill_type.production, 3, 3, "Dance Coach (trainer)")]
        [InlineData(staff._type.sales_manager, staff._skill_type.deals, staff._skill_type.production, 4, 1, "Sales Manager (dealmaker)")]
        [InlineData(staff._type.physician, staff._skill_type.physical_health, staff._skill_type.mental_health, 4, 1, "Physician (physical health care)")]
        public void Role_SaysWhatTheyMostlyDid(staff._type type, staff._skill_type main, staff._skill_type other, int mainLevel, int otherLevel, string role)
        {
            staff._staff staffer = Staffer(type, main, other, Today.AddYears(-1), mainLevel, otherLevel);
            Assert.Equal(role, MomentContext.StaffFiring(staffer, false, null).Fact(F, "Role"));
        }

        [Theory]
        [InlineData(true, false, false, "budget problems (ostensibly)")]
        [InlineData(false, true, false, "blamed for scandals involving Aya")]
        [InlineData(false, true, true, "blamed for scandals involving Aya and the group itself")]
        [InlineData(false, false, false, "blamed for the agency's scandals")]
        public void Moment_GivesTheStafferAndTheAnnouncedReason(bool budget, bool idol, bool group, string reason)
        {
            data_girls.girls aya = Idol("Aya");
            staff._staff staffer = Choreographer(Today.AddMonths(-18));
            List<PolicyChange.Cleared> cleared = new();
            if (idol)
            {
                cleared.Add(new PolicyChange.Cleared { Girl = aya, Points = 1 });
            }
            if (group)
            {
                cleared.Add(new PolicyChange.Cleared { Group = true, Points = 1 });
            }
            Moment moment = MomentContext.StaffFiring(staffer, budget, cleared);
            Assert.Equal("the agency fired a staff member", moment.Kind);
            Assert.Equal("Kenji", moment.Fact(F, "Staff member"));
            Assert.Equal("18 months", moment.Fact(F, "Time at the agency"));
            Assert.Equal(reason, moment.Fact(F, "Announced reason"));
            // A budget firing keeps whose scandals were behind it, so fans can doubt the announcement
            Assert.Equal(idol, moment.Lines(Moment.MembersSection).Contains("Aya:"));
        }

        /// <summary>
        /// Production staff get the agency's latest 3 singles released while they worked there, latest first;
        /// other staff get none.
        /// </summary>
        [Fact]
        public void ProductionStaff_GetTheirLatestSingles()
        {
            Single("Before", Today.AddYears(-2), 1000);
            Single("One", Today.AddDays(-300), 1000);
            Single("Two", Today.AddDays(-200), 1000);
            Single("Three", Today.AddDays(-100), 1000);
            Single("Four", Today.AddDays(-10), 1000);
            string producer = MomentContext.StaffFiring(Choreographer(Today.AddYears(-1)), false, null).Lines(F);
            Assert.Contains("Latest singles released while they worked there:\n- \"Four\"", producer);
            Assert.Contains("\"Two\"", producer);
            Assert.DoesNotContain("\"One\"", producer);
            Assert.DoesNotContain("\"Before\"", producer);

            staff._staff coach = Staffer(staff._type.dance_coach, staff._skill_type.coaching, staff._skill_type.production, Today.AddYears(-1));
            Assert.DoesNotContain("singles", MomentContext.StaffFiring(coach, false, null).Lines(F));
        }

        /// <summary>
        /// Dealmakers get the agency's 3 biggest running contracts by weekly pay, without the pay; other staff
        /// get none.
        /// </summary>
        [Fact]
        public void Dealmakers_GetTheBiggestContracts()
        {
            data_girls.girls aya = Idol("Aya");
            List<business.active_proposal> contracts = new()
            {
                new() { Type = business._type.ad, Product_Name = "Small", Agent_Name = "A", Girl = aya, Payment_per_week = 100, EndDate = Today.AddDays(70) },
                new() { Type = business._type.ad, Product_Name = "Big", Agent_Name = "B", isGroup = true, Payment_per_week = 900, EndDate = Today.AddDays(14) },
                new() { Type = business._type.ad, Product_Name = "Middle", Agent_Name = "C", Girl = aya, Payment_per_week = 500, EndDate = Today.AddDays(7) },
                new() { Type = business._type.ad, Product_Name = "Tiny", Agent_Name = "D", Girl = aya, Payment_per_week = 10, EndDate = Today.AddDays(7) },
            };
            staff._staff dealer = Staffer(staff._type.sales_manager, staff._skill_type.deals, staff._skill_type.production, Today.AddYears(-1));
            string lines = MomentContext.StaffFiring(dealer, false, null, null, contracts).Lines(F);
            Assert.Contains("Biggest business contracts running now:", lines);
            Assert.True(lines.IndexOf("for Big from B, with the whole group, 2 weeks left") < lines.IndexOf("for Middle from C, with Aya, 1 week left"), lines);
            Assert.Contains("for Small from A, with Aya, 10 weeks left", lines);
            Assert.DoesNotContain("Tiny", lines);
            Assert.DoesNotContain("900", lines);

            Assert.DoesNotContain("contracts", MomentContext.StaffFiring(Choreographer(Today.AddYears(-1)), false, null, null, contracts).Lines(F));
        }
    }
}
