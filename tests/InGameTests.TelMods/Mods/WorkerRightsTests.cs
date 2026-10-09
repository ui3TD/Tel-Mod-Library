using System.Collections;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Worker Rights' starting salary on the game's real GenerateGirl, after every other mod's
    /// patches on it. The unit test calls the postfix on a hand-made idol.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class WorkerRightsTests
    {
        private const string HarmonyId = "com.tel.workerrights";
        private const long StartingSalary = 20000;

        /// <summary>A newly generated idol asks for 20,000. Uses up one idol ID; the session isn't saved.</summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator NewIdolsStartAt20000(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            data_girls.girls girl = Game.Main.Data.GetComponent<data_girls>().GenerateGirl();
            ctx.Record("salary", girl.salary);
            ctx.Assert(girl.salary == StartingSalary, "A new idol's salary is " + girl.salary + ", expected " + StartingSalary);
        }
    }
}
