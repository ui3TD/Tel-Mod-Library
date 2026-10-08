using System;
using System.Linq;
using UnityEngine;
using Xunit;

namespace FanAttrition.Tests
{
    /// <summary>
    /// The fan tooltip lists each fan type's share and appeal, the fans a week from each source, the churn,
    /// and the total change a week.
    /// </summary>
    public class TooltipTests
    {
        public TooltipTests() => TestGame.Reset();

        private const int Lines = 15;

        private static string Green(string text) => ExtensionMethods.color(text, mainScript.green);
        private static string Red(string text) => ExtensionMethods.color(text, mainScript.red);

        private static tooltip_fans Tooltip()
        {
            tooltip_fans tooltip = TestGame.Component<tooltip_fans>();
            tooltip.prefab_line = TestGame.Component<GameObject>();
            tooltip.fan_change = TestGame.Component<GameObject>();
            return tooltip;
        }

        /// <summary>
        /// Starts the tooltip, which adds its lines, then renders it and returns each line's text.
        /// </summary>
        private static string[] Render()
        {
            tooltip_fans tooltip = Tooltip();
            Seams.StartPrefix(tooltip);
            Utility.RenderFanChangeDelegate = () => { };
            Assert.False(Seams.RenderPrefix(tooltip));
            return Seams.LineTexts(Seams.GameObjectOf(tooltip)).Select(Seams.TextOf).ToArray();
        }

        private static string Total()
        {
            tooltip_fans tooltip = Tooltip();
            tooltip_fans_RenderFanChange.Postfix(tooltip);
            return Seams.TextOf(tooltip.fan_change);
        }

        [Fact]
        public void Start_AddsTheLines()
        {
            tooltip_fans tooltip = Tooltip();

            Seams.StartPrefix(tooltip);

            Assert.Equal(Lines, Seams.Instantiated.Count);
            Assert.All(Seams.Instantiated, i => Assert.Same(tooltip.prefab_line, i.original));
            Assert.Equal(Seams.Instantiated.Select(i => Seams.TransformOf(i.copy)), Seams.Parented.Select(p => p.child), new SameObject());
            Assert.All(Seams.Parented, p => Assert.Same(Seams.TransformOf(Seams.GameObjectOf(tooltip)), p.parent));
            Assert.All(Seams.Parented, p => Assert.False(p.worldPositionStays));
            Assert.Equal(Seams.Instantiated.Select(i => Seams.ComponentOf<tooltip_fans_line>(i.copy)), Seams.LinesSet.Select(l => l.line), new SameObject());
            Assert.All(Seams.LinesSet, l => Assert.Equal("", l.text));
        }

        /// <summary>
        /// Render calls the tooltip's own RenderFanChange() to show the total.
        /// </summary>
        [Fact]
        public void Start_HooksUpTheTotal()
        {
            tooltip_fans tooltip = Tooltip();

            Seams.StartPrefix(tooltip);

            Assert.Same(tooltip, Utility.RenderFanChangeDelegate.Target);
            Assert.Equal("RenderFanChange", Utility.RenderFanChangeDelegate.Method.Name);
            Assert.Empty(Utility.RenderFanChangeDelegate.Method.GetParameters());
        }

        [Fact]
        public void Render_FillsEveryLineStartAdded()
        {
            string[] lines = Render();

            Assert.Equal(Lines, lines.Length);
            Assert.All(lines, Assert.NotNull);
        }

        [Fact]
        public void Render_ReplacesTheGamesAndRebuilds()
        {
            tooltip_fans tooltip = Tooltip();
            Seams.StartPrefix(tooltip);
            int totals = 0;
            Utility.RenderFanChangeDelegate = () => totals++;

            Assert.False(Seams.RenderPrefix(tooltip));

            Assert.Equal(1, totals);
            Assert.Same(Seams.ComponentOf<RectTransform>(Seams.GameObjectOf(tooltip)), Assert.Single(Seams.LayoutsRebuilt));
        }

        [Fact]
        public void Render_NoTooltip_LeavesItToTheGame()
        {
            Assert.True(Seams.RenderPrefix(null));
            Assert.Empty(Seams.LayoutsRebuilt);
        }

        /// <summary>
        /// Each fan type's share of all fans, and the active idols' average appeal to it. Shares of gender
        /// and hardcoreness are green from 60% and red to 40%; age shares from 40% and to 25%. Appeal is
        /// green from 40% and red below 30%.
        /// </summary>
        [Fact]
        public void FanTypeLines()
        {
            data_girls.girls idol = TestGame.Idol(0.5f,
                TestGame.Fans(resources.fanType.male, resources.fanType.hardcore, resources.fanType.teen, 450),
                TestGame.Fans(resources.fanType.male, resources.fanType.casual, resources.fanType.adult, 150),
                TestGame.Fans(resources.fanType.female, resources.fanType.casual, resources.fanType.youngAdult, 300),
                TestGame.Fans(resources.fanType.female, resources.fanType.hardcore, resources.fanType.adult, 100));
            idol.GetFanAppeal(resources.fanType.hardcore).ratio = 0.4f;
            idol.GetFanAppeal(resources.fanType.casual).ratio = 0.25f;
            idol.GetFanAppeal(resources.fanType.male).ratio = 0.35f;

            string[] lines = Render();

            Assert.Equal(new[]
            {
                "Hardcore: 55% of Total (" + Green("40%") + " Appeal)",
                "Casual: 45% of Total (" + Red("25%") + " Appeal)",
                "Male: " + Green("60%") + " of Total (35% Appeal)",
                "Female: " + Red("40%") + " of Total (" + Green("50%") + " Appeal)",
                "Teen: " + Green("45%") + " of Total (" + Green("50%") + " Appeal)",
                "Young Adult: 30% of Total (" + Green("50%") + " Appeal)",
                "Adult: " + Red("25%") + " of Total (" + Green("50%") + " Appeal)",
            }, lines.Take(7));
        }

        /// <summary>
        /// Appeal is averaged over active idols; shares count the fans of every idol who hasn't graduated.
        /// </summary>
        [Fact]
        public void FanTypeLines_CountActiveIdolsAppeal()
        {
            TestGame.Idol(0.2f, TestGame.Fans(resources.fanType.male, resources.fanType.hardcore, resources.fanType.teen, 100));
            TestGame.Idol(0.6f, TestGame.Fans(resources.fanType.female, resources.fanType.casual, resources.fanType.teen, 100));
            TestGame.Idol(0f, TestGame.Fans(resources.fanType.male, resources.fanType.casual, resources.fanType.teen, 200)).status = data_girls._status.hiatus;
            TestGame.Idol(0f, TestGame.Fans(resources.fanType.female, resources.fanType.hardcore, resources.fanType.teen, 1000)).status = data_girls._status.graduated;

            string[] lines = Render();

            Assert.Equal("Hardcore: " + Red("25%") + " of Total (" + Green("40%") + " Appeal)", lines[0]);
        }

        [Fact]
        public void FanTypeLines_WorkOutMissingAppeal()
        {
            data_girls.girls idol = TestGame.Idol();
            idol.FanAppeal.Clear();

            Render();

            Assert.Equal(Enum.GetValues(typeof(resources.fanType)).Cast<resources.fanType>(), idol.FanAppeal.Select(a => a.type));
        }

        [Fact]
        public void FanTypeLines_NoIdols()
        {
            string[] lines = Render();

            Assert.Equal("Hardcore: " + Red("0%") + " of Total (" + Red("0%") + " Appeal)", lines[0]);
            Assert.Equal("Adult: " + Red("0%") + " of Total (" + Red("0%") + " Appeal)", lines[6]);
        }

        [Fact]
        public void Separator()
        {
            Assert.Equal(mainScript.separator_no_linebreaks, Render()[7]);
        }

        [Fact]
        public void SourceLines()
        {
            Utility.adFans = 1500;
            Utility.dramaFans = 600;
            Utility.netFans = 70;
            Utility.tvFans = 2000;
            Utility.radioFans = 300;
            Utility.cafeFans = 40;

            string[] lines = Render();

            Assert.Equal(new[]
            {
                "Ad Contracts: " + Green("+1,500 /w"),
                "Drama Contracts: " + Green("+600 /w"),
                "Internet Shows: " + Green("+70 /w"),
                "TV Shows: " + Green("+2,000 /w"),
                "Radio Shows: " + Green("+300 /w"),
                "Cafe: " + Green("+40 /w"),
            }, lines.Skip(8).Take(6));
        }

        [Fact]
        public void SourceLines_NoFans()
        {
            Assert.Equal(new[]
            {
                "Ad Contracts: 0 /w",
                "Drama Contracts: 0 /w",
                "Internet Shows: 0 /w",
                "TV Shows: 0 /w",
                "Radio Shows: 0 /w",
                "Cafe: 0 /w",
            }, Render().Skip(8).Take(6));
        }

        [Fact]
        public void ChurnLine_ShowsAWeek()
        {
            resources.FansChange = -68;

            Assert.Equal("Churn Rate: " + Red("-476 /w"), Render()[14]);
        }

        [Fact]
        public void ChurnLine_NoChurn()
        {
            Assert.Equal("Churn Rate: 0 /w", Render()[14]);
        }

        /// <summary>
        /// The total a week adds every source and a week of churn: 2,150 - 476.
        /// </summary>
        [Fact]
        public void Total_AddsEverySourceAndTheChurn()
        {
            Utility.adFans = 1000;
            Utility.dramaFans = 500;
            Utility.netFans = 200;
            Utility.tvFans = 300;
            Utility.radioFans = 100;
            Utility.cafeFans = 50;
            resources.FansChange = -68;

            Assert.Equal("Total: " + Green("+1,674 /w"), Total());
        }

        /// <summary>
        /// Fixed in 1.1.0: the cafe line was left out of the total.
        /// </summary>
        [Fact]
        public void Total_IncludesCafes()
        {
            Utility.cafeFans = 50;

            Assert.Equal("Total: " + Green("+50 /w"), Total());
        }

        [Fact]
        public void Total_Losing()
        {
            Utility.adFans = 100;
            resources.FansChange = -68;

            Assert.Equal("Total: " + Red("-376 /w"), Total());
        }

        [Fact]
        public void Total_NoChange()
        {
            Assert.Equal("Total: 0 /w", Total());
        }
    }
}
