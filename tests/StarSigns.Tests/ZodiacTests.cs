using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static StarSigns.StarSigns;

namespace StarSigns.Tests
{
    /// <summary>
    /// An idol's sign comes from her birthday, using the usual Western zodiac dates.
    /// </summary>
    public class ZodiacTests
    {
        /// <summary>
        /// Each sign's first and last day.
        /// </summary>
        public static IEnumerable<object[]> SignDates() => new[]
        {
            new object[] { Zodiac.Capricorn, 12, 22, 1, 19 },
            new object[] { Zodiac.Aquarius, 1, 20, 2, 18 },
            new object[] { Zodiac.Pisces, 2, 19, 3, 20 },
            new object[] { Zodiac.Aries, 3, 21, 4, 19 },
            new object[] { Zodiac.Taurus, 4, 20, 5, 20 },
            new object[] { Zodiac.Gemini, 5, 21, 6, 21 },
            new object[] { Zodiac.Cancer, 6, 22, 7, 22 },
            new object[] { Zodiac.Leo, 7, 23, 8, 22 },
            new object[] { Zodiac.Virgo, 8, 23, 9, 22 },
            new object[] { Zodiac.Libra, 9, 23, 10, 23 },
            new object[] { Zodiac.Scorpio, 10, 24, 11, 21 },
            new object[] { Zodiac.Sagittarius, 11, 22, 12, 21 },
        };

        [Theory]
        [MemberData(nameof(SignDates))]
        public void FirstAndLastDay_AreInTheSign(Zodiac sign, int firstMonth, int firstDay, int lastMonth, int lastDay)
        {
            Assert.Equal(sign, DateToZodiac(new DateTime(2001, firstMonth, firstDay)));
            Assert.Equal(sign, DateToZodiac(new DateTime(2001, lastMonth, lastDay)));
        }

        [Theory]
        [MemberData(nameof(SignDates))]
        public void DaysOutside_AreOtherSigns(Zodiac sign, int firstMonth, int firstDay, int lastMonth, int lastDay)
        {
            Assert.NotEqual(sign, DateToZodiac(new DateTime(2001, firstMonth, firstDay).AddDays(-1)));
            Assert.NotEqual(sign, DateToZodiac(new DateTime(2001, lastMonth, lastDay).AddDays(1)));
        }

        /// <summary>
        /// Walks a leap year from 22 December: every day has a sign, the signs follow in order,
        /// and each lasts about a month.
        /// </summary>
        [Fact]
        public void EveryDay_HasOneSign_InOrder()
        {
            Zodiac[] order = SignDates().Select(d => (Zodiac)d[0]).ToArray();
            List<(Zodiac sign, int days)> runs = new();
            for (DateTime day = new(2003, 12, 22); day < new DateTime(2004, 12, 22); day = day.AddDays(1))
            {
                Zodiac sign = DateToZodiac(day);
                if (runs.Count > 0 && runs[runs.Count - 1].sign == sign)
                    runs[runs.Count - 1] = (sign, runs[runs.Count - 1].days + 1);
                else
                    runs.Add((sign, 1));
            }

            Assert.Equal(order, runs.Select(r => r.sign));
            Assert.All(runs, r => Assert.InRange(r.days, 29, 32));
        }

        [Fact]
        public void LeapDay_IsPisces()
        {
            Assert.Equal(Zodiac.Pisces, DateToZodiac(new DateTime(2004, 2, 29)));
        }

        /// <summary>
        /// The game's default date (an idol saved without a birthday) still has a sign, so no idol is ever None.
        /// </summary>
        [Fact]
        public void DefaultDate_IsCapricorn()
        {
            Assert.Equal(Zodiac.Capricorn, DateToZodiac(default));
        }

        [Fact]
        public void GirlsSign_ComesFromHerBirthday()
        {
            data_girls.girls girl = new() { birthday = new DateTime(2003, 8, 1) };
            Assert.Equal(Zodiac.Leo, GetGirlZodiac(girl));

            girl.birthday = new DateTime(2003, 8, 31);
            Assert.Equal(Zodiac.Virgo, GetGirlZodiac(girl));
        }

        /// <summary>
        /// The tests' own idol builder makes idols of the sign asked for.
        /// </summary>
        [Fact]
        public void TestIdols_HaveTheirSign()
        {
            TestGame.Reset();
            foreach (Zodiac sign in TestGame.Signs)
            {
                data_girls.girls girl = TestGame.Idol(sign);
                Assert.Equal(sign, GetGirlZodiac(girl));
                Assert.Equal(20, girl.GetAge());
            }
        }
    }
}
