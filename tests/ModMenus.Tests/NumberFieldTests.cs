using SimpleJSON;
using TMPro;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.1.0: a text field with "inputType": "integer" or "decimal" takes only a number, between its
    /// minValue and maxValue where they're set. The game's field stops other characters being typed; leaving the
    /// field moves the number inside its limits, and anything that isn't a number goes back to the last number.
    /// </summary>
    public class NumberFieldTests
    {
        public NumberFieldTests() => TestGame.Reset();

        private const string Seats = "A_Seats";

        /// <summary>
        /// A whole-number field from 1 to 99, starting at 10, and a decimal field with no limits.
        /// </summary>
        private static Menu OpenMenu()
        {
            TestGame.AddMod("Mod A", """
                [
                    { "type": "input", "varID": "A_Seats", "labelID": "TEST__MOTTO", "inputType": "integer", "minValue": 1, "maxValue": 99, "defaultValue": "10" },
                    { "type": "input", "varID": "A_Rate", "labelID": "TEST__MOTTO", "inputType": "decimal" }
                ]
                """);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        [Theory]
        [InlineData("12", true, null, null, "12")]
        [InlineData("  7 ", true, null, null, "7")]
        [InlineData("12.6", true, null, null, "13")]
        [InlineData("-3", true, null, null, "-3")]
        [InlineData("abc", true, null, null, "fallback")]
        [InlineData("", true, null, null, "fallback")]
        [InlineData(null, true, null, null, "fallback")]
        [InlineData("-", true, null, null, "fallback")]
        [InlineData("1,5", false, null, null, "1.5")]
        [InlineData("1.5", false, null, null, "1.5")]
        [InlineData("1e3", false, null, null, "1000")]
        [InlineData("0.1234567", false, null, null, "0.123457")]
        [InlineData("250", true, 1f, 99f, "99")]
        [InlineData("-5", true, 1f, null, "1")]
        [InlineData("-5", true, null, 10f, "-5")]
        [InlineData("500", false, null, 10.5f, "10.5")]
        [InlineData("1", true, 1.5f, null, "2")]          // a whole number inside the limits
        [InlineData("123456789", true, null, null, "123456789")]
        public void KeepsTheNumberInsideItsLimits(string typed, bool integer, float? min, float? max, string expected)
        {
            Assert.Equal(expected, ValidNumber(typed, integer, min, max, "fallback"));
        }

        [Theory]
        [InlineData("", null, null, "0")]
        [InlineData("", 5f, 10f, "5")]
        [InlineData("", -10f, -5f, "-5")]
        [InlineData("7", null, null, "7")]
        [InlineData("abc", 1f, null, "1")]
        [InlineData("50", 1f, 10f, "10")]
        public void DefaultIsItsDefaultValueOrZeroInsideItsLimits(string def, float? min, float? max, string expected)
        {
            Assert.Equal(expected, NumberDefault(def, true, min, max));
        }

        [Theory]
        [InlineData("""{ "inputType": "integer" }""", "integer")]
        [InlineData("""{ "inputType": "decimal" }""", "decimal")]
        [InlineData("""{ "inputType": "email" }""", null)]
        [InlineData("""{ }""", null)]
        public void OnlyIntegerAndDecimalMakeANumberField(string json, string expected)
        {
            Assert.Equal(expected != null, GetNumberField(JSON.Parse(json), out string inputType, out _, out _));
            Assert.Equal(expected, inputType);
        }

        [Fact]
        public void EachLimitWorksOnItsOwn()
        {
            GetNumberField(JSON.Parse("""{ "inputType": "integer", "minValue": 3 }"""), out _, out float? min, out float? max);
            Assert.Equal((3f, (float?)null), (min, max));

            GetNumberField(JSON.Parse("""{ "inputType": "integer", "maxValue": 8 }"""), out _, out min, out max);
            Assert.Equal(((float?)null, 8f), (min, max));
        }

        /// <summary>
        /// The game's field lets only a number be typed. A password option can't apply as well, so it's ignored.
        /// </summary>
        [Fact]
        public void TheGamesFieldTakesOnlyANumber()
        {
            TestGame.AddMod("Mod A", """
                [
                    { "type": "input", "varID": "A_Int", "labelID": "TEST__MOTTO", "inputType": "integer" },
                    { "type": "input", "varID": "A_Dec", "labelID": "TEST__MOTTO", "inputType": "decimal" },
                    { "type": "input", "varID": "A_Pin", "labelID": "TEST__MOTTO", "inputType": "integer", "password": true },
                    { "type": "input", "varID": "A_Text", "labelID": "TEST__MOTTO", "inputType": "words" }
                ]
                """);
            Menu menu = TestGame.BuildMenu();

            Assert.Equal(TMP_InputField.ContentType.IntegerNumber, menu.Field("A_Int").contentType);
            Assert.Equal(TMP_InputField.ContentType.DecimalNumber, menu.Field("A_Dec").contentType);
            Assert.Equal(TMP_InputField.ContentType.IntegerNumber, menu.Field("A_Pin").contentType);
            Assert.Equal(TMP_InputField.ContentType.Standard, menu.Field("A_Text").contentType);
            Assert.Contains(Log.Messages, m => m == "[ModMenus] The text field A_Pin takes a number, so its password option is ignored.");
        }

        [Fact]
        public void OpensAtItsDefault()
        {
            Menu menu = OpenMenu();

            Assert.Equal(("10", "10"), (menu.FieldText(Seats), menu.Item(Seats).tempText));
            Assert.Equal("0", menu.FieldText("A_Rate"));
        }

        [Theory]
        [InlineData("250", "99")]
        [InlineData("0", "1")]
        [InlineData("42", "42")]
        public void LeavingTheFieldMovesTheNumberInsideItsLimits(string typed, string expected)
        {
            Menu menu = OpenMenu();

            menu.TypeAndLeave(Seats, typed);

            Assert.Equal((expected, expected), (menu.FieldText(Seats), menu.Item(Seats).tempText));
            menu.Apply();
            Assert.Equal(expected, TestGame.Saved(Seats));
        }

        /// <summary>
        /// An emptied field goes back to the last number it held.
        /// </summary>
        [Fact]
        public void LeavingItEmptyGoesBackToTheLastNumber()
        {
            Menu menu = OpenMenu();
            menu.TypeAndLeave(Seats, "42");

            menu.TypeAndLeave(Seats, "");

            Assert.Equal("42", menu.FieldText(Seats));
            menu.Apply();
            Assert.Equal("42", TestGame.Saved(Seats));
        }

        /// <summary>
        /// Apply clicked while the field still has focus checks the number too.
        /// </summary>
        [Fact]
        public void ApplyChecksAFieldStillBeingTypedIn()
        {
            Menu menu = OpenMenu();

            menu.Type(Seats, "250");
            menu.Apply();

            Assert.Equal("99", TestGame.Saved(Seats));
        }

        /// <summary>
        /// A save edited by hand opens at the field's default.
        /// </summary>
        [Fact]
        public void ASavedValueThatIsNotANumberOpensAtTheDefault()
        {
            TestGame.Save(Seats, "abc");

            Menu menu = OpenMenu();

            Assert.Equal("10", menu.FieldText(Seats));
            menu.Apply();
            Assert.Equal("10", TestGame.Saved(Seats));
        }

        [Fact]
        public void ASavedNumberOpensAsSaved()
        {
            TestGame.Save(Seats, "64");

            Menu menu = OpenMenu();

            Assert.Equal("64", menu.FieldText(Seats));
        }

        /// <summary>
        /// A field without inputType still keeps what's typed, whatever it is.
        /// </summary>
        [Fact]
        public void APlainTextFieldIsUnchanged()
        {
            TestGame.AddMod("Mod A", """[ { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO", "minValue": 1, "maxValue": 9 } ]""");
            Menu menu = TestGame.BuildMenu();
            menu.Open();

            menu.TypeAndLeave("A_Motto", " 250 abc ");
            menu.Apply();

            Assert.Equal(" 250 abc ", TestGame.Saved("A_Motto"));
        }
    }
}
