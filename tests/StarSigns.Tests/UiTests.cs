using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Xunit;
using static StarSigns.StarSigns;

namespace StarSigns.Tests
{
    /// <summary>
    /// The profile's Extras tab adds the idol's sign and what it does under her trait.
    /// </summary>
    public class ProfileTests
    {
        public ProfileTests() => TestGame.Reset();

        private const string TraitText = "<color=#777bba>Trait: </color>Trait description.";

        public static IEnumerable<object[]> AllSigns() => TestGame.Signs.Select(s => new object[] { s });

        /// <summary>
        /// Renders the Extras tab on top of the game's first text block (the trait), and returns that block.
        /// </summary>
        private static string Render(data_girls.girls girl, out Profile_Popup popup)
        {
            popup = TestGame.Component<Profile_Popup>();
            popup.Girl = girl;
            popup.Extras_Container = TestGame.Component<GameObject>();
            TextMeshProUGUI text = TraitBlock(popup);
            Seams.SetText(text, TraitText);

            Seams.ProfileExtrasPostfix(popup);
            return Seams.TextOf(text);
        }

        private static TextMeshProUGUI TraitBlock(Profile_Popup popup) =>
            Seams.ComponentOf<TextMeshProUGUI>(Seams.Child(Seams.TransformOf(popup.Extras_Container), "Text(Clone)"));

        private static string Label(string text) => ExtensionMethods.color(text, mainScript.black);

        [Theory]
        [MemberData(nameof(AllSigns))]
        public void ShowsTheSignAndItsDescription(Zodiac sign)
        {
            string text = Render(TestGame.Idol(sign), out _);

            string name = sign.ToString().ToUpper();
            string expected = "\n" + Label(Language.Data["STARSIGN__TITLE_" + name] + ": ") + Language.Data["STARSIGN__DESC_" + name];
            Assert.EndsWith(expected, text);
        }

        [Fact]
        public void Capricorn_Line()
        {
            Assert.Equal(
                Label("Trait: ") + "Trait description.\n" + Label("Capricorn: ") + "Meshes well with serious girls who don't date",
                Render(TestGame.Idol(Zodiac.Capricorn), out _));
        }

        /// <summary>
        /// The sign goes into the trait's text block, whose blue labels turn black like the sign's.
        /// </summary>
        [Fact]
        public void TraitLabel_TurnsBlackToo()
        {
            string text = Render(TestGame.Idol(Zodiac.Leo), out _);

            Assert.StartsWith(Label("Trait: ") + "Trait description.\n", text);
            Assert.DoesNotContain(mainScript.blue, text);
            Assert.Equal(new[] { "Text(Clone)" }, Seams.FoundChildren);
        }

        [Fact]
        public void RebuildsTheLayout()
        {
            Render(TestGame.Idol(Zodiac.Leo), out Profile_Popup popup);

            RectTransform layout = Assert.Single(Seams.LayoutsRebuilt);
            Assert.Same(Seams.ComponentOf<RectTransform>(popup.Extras_Container), layout);
        }
    }

    /// <summary>
    /// Audition cards show the idol's sign after her age, whether they animate in or appear at once.
    /// </summary>
    public class AuditionCardTests
    {
        public AuditionCardTests() => TestGame.Reset();

        private static string Show(data_girls.girls girl, bool fast)
        {
            Audition_Data_Card card = TestGame.Component<Audition_Data_Card>();
            card.Girl = new Auditions.data._girl { girl = girl };
            card.Age = TestGame.Component<GameObject>();
            TextMeshProUGUI text = Seams.ComponentOf<TextMeshProUGUI>(card.Age);
            Seams.SetText(text, "Age: 20");

            if (fast)
                Audition_Data_Card_Show_Fast.Postfix(ref card);
            else
                Audition_Data_Card_Show.Postfix(ref card);
            return Seams.TextOf(text);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AgeShowsTheSign(bool fast)
        {
            Assert.Equal("Age: 20 (Capricorn)", Show(TestGame.Idol(Zodiac.Capricorn), fast));
            Assert.Equal("Age: 20 (Sagittarius)", Show(TestGame.Idol(Zodiac.Sagittarius), fast));
        }
    }
}
