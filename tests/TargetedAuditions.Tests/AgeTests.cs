using CustomAuditions;
using System;
using System.Collections.Generic;
using Xunit;
using static CustomAuditions.CustomAuditions;

namespace TargetedAuditions.Tests
{
    /// <summary>
    /// Candidate ages fall within the Mod Menu range, inclusive, on every possible birthday.
    /// </summary>
    public class AgeTests : IDisposable
    {
        private readonly DateTime today = staticVars.dateTime;

        public AgeTests()
        {
            Seams.Reset();
        }

        public void Dispose()
        {
            staticVars.dateTime = today;
        }

        private static data_girls.girls GenerateBirthday(bool inAudition)
        {
            if (inAudition)
                BeginAuditionGeneration();
            data_girls.girls girl = Seams.NewGirl();
            girl.birthday = new DateTime(2000, 6, 15);
            data_girls_girls_GenerateBirthday.Postfix(ref girl);
            return girl;
        }

        /// <summary>
        /// Rolls every age in the range and every day of each age's birthday window.
        /// </summary>
        [Theory]
        [InlineData("2025-06-15", 12, 23)]
        [InlineData("2024-02-29", 12, 23)]
        [InlineData("2025-03-01", 16, 16)]
        [InlineData("2025-12-31", 5, 100)]
        [InlineData("2025-01-01", 18, 20)]
        public void EveryRoll_GivesAnAgeInRange(string date, int min, int max)
        {
            staticVars.dateTime = DateTime.Parse(date);
            minAge = min;
            maxAge = max;
            HashSet<int> agesSeen = new();
            HashSet<DateTime> birthdays = new();

            for (int age = min; age <= max; age++)
            {
                int days = 0;
                for (int day = 0; ; day++)
                {
                    int ageRoll = age;
                    int dayRoll = day;
                    bool lastDay = false;
                    Seams.Range = (lo, hi) =>
                    {
                        if (lo == min)
                        {
                            Assert.Equal(max + 1, hi);
                            return ageRoll;
                        }
                        Assert.Equal(0, lo);
                        days = hi;
                        lastDay = dayRoll == hi - 1;
                        return dayRoll;
                    };

                    data_girls.girls girl = GenerateBirthday(inAudition: true);
                    EndAuditionGeneration();

                    Assert.Equal(age, girl.GetAge());
                    agesSeen.Add(girl.GetAge());
                    Assert.True(birthdays.Add(girl.birthday), $"Duplicate birthday {girl.birthday:d}");
                    if (lastDay)
                        break;
                }
                Assert.InRange(days, 365, 366);
            }

            Assert.Equal(max - min + 1, agesSeen.Count);
        }

        /// <summary>
        /// The old formula could produce max + 1; check the edges directly.
        /// </summary>
        [Fact]
        public void OldestRoll_IsMaxAgeNotOneOver()
        {
            staticVars.dateTime = new DateTime(2025, 6, 15);
            minAge = 12;
            maxAge = 23;
            Seams.Range = (lo, hi) => hi - 1;

            data_girls.girls girl = GenerateBirthday(inAudition: true);

            Assert.Equal(23, girl.GetAge());
            Assert.Equal(new DateTime(2002, 6, 15), girl.birthday);
        }

        [Fact]
        public void YoungestRoll_IsMinAge()
        {
            staticVars.dateTime = new DateTime(2025, 6, 15);
            minAge = 12;
            maxAge = 23;
            Seams.Range = (lo, hi) => lo;

            data_girls.girls girl = GenerateBirthday(inAudition: true);

            Assert.Equal(12, girl.GetAge());
            Assert.Equal(new DateTime(2012, 6, 16), girl.birthday);
        }

        /// <summary>
        /// Rivals, unique idols and scripted girls keep the game's own birthday.
        /// </summary>
        [Fact]
        public void OtherGirls_KeepTheirBirthday()
        {
            Seams.Rolls();
            Assert.Equal(new DateTime(2000, 6, 15), GenerateBirthday(inAudition: false).birthday);
        }

        [Fact]
        public void NullGirl_IsIgnored()
        {
            Seams.Rolls();
            ApplyRandomBirthdayInConfiguredRange(null);
        }
    }
}
