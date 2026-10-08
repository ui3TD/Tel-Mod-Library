using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using _difficulty = staticVars._playerData._difficulty;
using _venue = SEvent_Concerts._venue;

// The concert tests share the game's static state (fans, variables, difficulty, unlocked venue) and patch the game.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ConcertRebalance.Tests
{
    /// <summary>
    /// A set list item worth a fixed amount of hype. An MC, so the production cost doesn't look for its single.
    /// </summary>
    public class FixedHypeItem : SEvent_Concerts._concert.ISetlistItem
    {
        private readonly float hype;

        public FixedHypeItem(float hype) => this.hype = hype;

        public float GetHype(SEvent_Concerts._concert Parent) => hype;
        public bool isMC() => true;
        public void SetTitle(string val) { }
        public string GetTitle() => "";
        public void SetGirl(data_girls.girls girl, int id = 0) { }
        public bool ReplaceGirl(data_girls.girls girl) => false;
        public List<data_girls.girls> GetGirls(bool skipNull = false) => new();
        public int GetSkillValue() => 0;
        public float GetStaminaCost() => 0f;
    }

    /// <summary>
    /// Builds concerts in a game with the mod applied, and compares the popup's projected revenue with what the game pays.
    /// </summary>
    public static class ConcertGame
    {
        private static readonly Lazy<bool> Patched = new(() =>
        {
            new Harmony("tests.ConcertRebalance.Seams").Patch(
                AccessTools.Method(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish)),
                transpiler: new HarmonyMethod(typeof(ConcertGame), nameof(SetStatusDirectly)));

            // Not "tests.ConcertRebalance": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.ConcertRebalance.Behaviour");
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(ConcertRebalance).Assembly, "ConcertRebalance"))
                harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        });

        /// <summary>
        /// Resets the game to Normal (or this difficulty) with no scandals or awards, enough hardcore fans to sell out
        /// any venue, and only the club unlocked.
        /// </summary>
        public static void Reset(bool fujiTickets = false, _difficulty difficulty = _difficulty.normal)
        {
            _ = Patched.Value;
            staticVars.PlayerData.Difficulty = difficulty;
            staticVars.dateTime = new DateTime(2023, 6, 5);
            data_girls.girl = new List<data_girls.girls>();
            resources.resource = new List<long>(new long[32]);
            resources.Fans = new List<resources._fan>
            {
                new() { hardcoreness = resources.fanType.hardcore, people = 10_000_000 },
            };
            Awards._Awards = new List<Awards._award>();
            variables.variable = new List<variables._variable>();
            if (fujiTickets)
                variables.variable.Add(new variables._variable { name = "FUJI_3_TICKETS", value = "true" });
            SEvent_Concerts.UnlockedVenue = _venue.club;
        }

        /// <summary>
        /// A concert whose set list adds up to this hype, which it also reaches on the day.
        /// </summary>
        public static SEvent_Concerts._concert Concert(_venue venue, float hype, int ticketPrice = 5000)
        {
            SEvent_Concerts._concert concert = new() { Venue = venue, Hype = hype };
            concert.ProjectedValues.Parent = concert;
            concert.SetListItems.Add(new FixedHypeItem(hype));
            concert.ProjectedValues.TicketPrice = ticketPrice;
            concert.RecalcProjectedValues();
            return concert;
        }

        /// <summary>
        /// Asserts the popup's estimate is what the game pays, allowing for float rounding.
        /// </summary>
        public static void AssertForecastMatchesPayout(SEvent_Concerts._concert concert)
        {
            long forecast = concert.ProjectedValues.GetRevenue();
            long payout = concert.ProjectedValues.Actual_Revenue;
            Assert.True(payout > 0L);
            Assert.True(Math.Abs(forecast - payout) <= Math.Max(1L, payout / 1_000_000L),
                $"{concert.Venue} at {concert.Hype}% hype: forecast {forecast}, payout {payout}");
        }

        /// <summary>
        /// Finish sets the concert's status through SetStatus, which also redraws a Unity UI widget. The tests set it directly.
        /// </summary>
        private static IEnumerable<CodeInstruction> SetStatusDirectly(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo setStatus = AccessTools.Method(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.SetStatus));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(setStatus))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(ConcertGame), nameof(StubSetStatus));
                }
                yield return instruction;
            }
        }

        private static void StubSetStatus(SEvent_Concerts._concert concert, SEvent_Tour.tour._status status) => concert.Status = status;
    }
}
