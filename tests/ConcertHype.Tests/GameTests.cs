using ConcertHype.Model;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using _concert = SEvent_Concerts._concert;

namespace ConcertHype.Tests
{
    /// <summary>
    /// Unity treats every fake game object as equal: compare references.
    /// </summary>
    public sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    public class GameReaderTests : IDisposable
    {
        public GameReaderTests() => GameFixture.Reset();

        public void Dispose() => GameFixture.Reset();

        [Fact]
        public void Slots_AreTheFilledSenbatsuPositions_InTriangleRows()
        {
            data_girls.girls[] idols = Enumerable.Range(0, 7).Select(i => GameFixture.Idol("I" + i)).ToArray();
            singles._single single = GameFixture.Single("A", idols[0], idols[1], idols[2], null, null, idols[3], idols[4], null, null, null, idols[5]);
            GameShow show = GameReader.Read(GameFixture.Concert(single), new Config());

            Song song = show.Setup.Songs[0];
            Assert.Equal(new[] { 0, 1, 2, 5, 6, 10 }, song.Slots.Select(s => s.Position));
            Assert.Equal(new[] { 0, 1, 1, 2, 3, 4 }, song.Slots.Select(s => s.Row));
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, song.Slots.Select(s => s.Original));
            Assert.All(song.Slots, s => Assert.Same(show.Girls[s.Original], idols[s.Original]));
        }

        [Fact]
        public void AnOriginalWhoCantPerform_LeavesAForcedSlot_AndIsNotInTheCast()
        {
            data_girls.girls a = GameFixture.Idol("A"), b = GameFixture.Idol("B"), c = GameFixture.Idol("C");
            b.status = data_girls._status.graduated;
            GameShow show = GameReader.Read(GameFixture.Concert(GameFixture.Single("S", a, b, c)), new Config());

            Assert.DoesNotContain(show.Girls, g => ReferenceEquals(g, b));
            Assert.Equal(new[] { 0, -1, 1 }, show.Setup.Songs[0].Slots.Select(s => s.Original));
            ShowState state = Engine.Start(show.Setup, new Config(), 1);
            Assert.Equal(new[] { false, true, false }, Engine.ForcedSlots(state));
        }

        [Fact]
        public void Cast_IsEverySenbatsuOnce_PlusSetupCenters_WhoStartAsTheSubstitute()
        {
            data_girls.girls a = GameFixture.Idol("A"), b = GameFixture.Idol("B"), c = GameFixture.Idol("C"), d = GameFixture.Idol("D");
            _concert concert = GameFixture.Concert(GameFixture.Single("S1", a, b), GameFixture.Single("S2", b, c));
            ((_concert._song)concert.SetListItems[1]).Center = d;
            ((_concert._song)concert.SetListItems[0]).Center = b;

            GameShow show = GameReader.Read(concert, new Config());
            Assert.Equal(new[] { a, b, c, d }, show.Girls, ReferenceEqualityComparer.Instance);
            Assert.Equal(-1, show.Setup.Songs[0].PresetSubstitute);  // b is in S1's senbatsu: nothing changes
            Assert.Equal(3, show.Setup.Songs[1].PresetSubstitute);
        }

        [Fact]
        public void TheBench_HoldsEveryActiveIdolInTheAgency_AfterTheSetlistsIdols()
        {
            data_girls.girls other1 = GameFixture.Idol("Other1"), a = GameFixture.Idol("A"), resting = GameFixture.Idol("Resting"),
                b = GameFixture.Idol("B"), other2 = GameFixture.Idol("Other2");
            resting.status = data_girls._status.hiatus;
            GameShow show = GameReader.Read(GameFixture.Concert(GameFixture.Single("S", a, b)), new Config());

            Assert.Equal(new[] { a, b, other1, other2 }, show.Girls, ReferenceEqualityComparer.Instance);
            Assert.Equal(new[] { true, true, false, false }, show.Setup.Members.Select(m => m.InSetlist));
            Assert.Equal(4, show.Setup.Skill.Length);
        }

        [Fact]
        public void FanWeights_AreAgainstTheSetlistsIdols_SoTheRestOfTheAgencyChangesNoWeight()
        {
            GameFixture.Idol("Small", fans: 10);
            data_girls.girls a = GameFixture.Idol("A", fans: 1000), b = GameFixture.Idol("B", fans: 3000);
            ShowSetup setup = GameReader.Read(GameFixture.Concert(GameFixture.Single("S", a, b)), new Config()).Setup;
            ShowState state = Engine.Start(setup, new Config(), 1);
            Assert.Equal(new[] { 0.5, 1.5, 0.25 }, state.FanWeight.Select(w => Math.Round(w, 9)));
        }

        [Fact]
        public void Talk_IsTheGamesTalkBreakSkill()
        {
            data_girls.girls a = GameFixture.Idol("A", funny: 81, smart: 60);
            ShowSetup setup = GameReader.Read(GameFixture.Concert(GameFixture.Single("S", a)), new Config()).Setup;
            Assert.Equal(0.7, setup.Members[0].Talk, 9);  // floor((81 + 60) / 2) = 70
        }

        private static void PlusFive(ref int __result) => __result += 5;

        [Fact]
        public void Talk_KeepsOtherModsChangesToTheTalkBreakSkill()
        {
            Harmony harmony = new("tests.ConcertHype.OtherMod");
            harmony.Patch(AccessTools.Method(typeof(_concert._mc), nameof(_concert._mc.GetSkillValue)), postfix: new HarmonyMethod(typeof(GameReaderTests), nameof(PlusFive)));
            try
            {
                data_girls.girls a = GameFixture.Idol("A", funny: 80, smart: 60);
                Assert.Equal(0.75, GameReader.Read(GameFixture.Concert(GameFixture.Single("S", a)), new Config()).Setup.Members[0].Talk, 9);
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }

        [Fact]
        public void Appeal_ComesFromStats_AndFromTheSinglesOptionsAtTheConfiguredLevel()
        {
            data_girls.girls a = GameFixture.Idol("A");
            singles._single single = GameFixture.Single("S", a);
            single.genre = GameFixture.Option("Rock", 0.1f, 0, 0, 0, 0, 0, 0);
            single.lyrics = GameFixture.Option("Love", 0, 0.2f);
            single.choreography = GameFixture.Option("Cute", 0, 0, 0.3f);
            GameShow show = GameReader.Read(GameFixture.Concert(single), new Config());

            Assert.Equal(new[] { 0.1, 0.2, 0.3, 0, 0, 0, 0 }, show.Setup.Songs[0].Appeal.Select(v => Math.Round(v, 5)));
            Assert.Equal(new[] { 15.1, 15.2, 15.3 }, GameReader.Read(GameFixture.Concert(single), new Config { OptionLevel = 2 }).Setup.Songs[0].Appeal.Take(3).Select(v => Math.Round(v, 5)));
            resources.fanType[] order = { resources.fanType.male, resources.fanType.female, resources.fanType.casual, resources.fanType.hardcore,
                resources.fanType.teen, resources.fanType.youngAdult, resources.fanType.adult };
            Assert.Equal(order.Select(f => Math.Round(GameFixture.Stats.Sum(s => (double)a.GetAppealOfStat(s, f)), 5)),
                show.Setup.Members[0].Appeal.Select(v => Math.Round(v, 5)));
        }

        [Fact]
        public void Skill_IsTheGamesConcertSkill_WithItsCenterRule()
        {
            data_girls.girls center = GameFixture.Idol("C", dance: 40, vocal: 51), other = GameFixture.Idol("O", dance: 70, vocal: 61);
            GameShow show = GameReader.Read(GameFixture.Concert(GameFixture.Single("S", center, other)), new Config());
            Assert.Equal(80, show.Setup.Skill[0][0]);  // the single's own center counts at least 80
            Assert.Equal(65, show.Setup.Skill[1][0]);  // floor of the average
        }

        private static void PlusSeven(ref int __result) => __result += 7;

        [Fact]
        public void Skill_KeepsOtherModsChangesToTheConcertSkill()
        {
            Harmony harmony = new("tests.ConcertHype.OtherMod");
            harmony.Patch(AccessTools.Method(typeof(_concert._song), nameof(_concert._song.GetSkillValue)), postfix: new HarmonyMethod(typeof(GameReaderTests), nameof(PlusSeven)));
            try
            {
                data_girls.girls a = GameFixture.Idol("A"), b = GameFixture.Idol("B", dance: 70, vocal: 60);
                GameShow show = GameReader.Read(GameFixture.Concert(GameFixture.Single("S", a, b)), new Config());
                Assert.Equal(72, show.Setup.Skill[1][0]);
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }

        [Fact]
        public void ShowInputs_ComeFromTheConcert()
        {
            data_girls.girls a = GameFixture.Idol("A", fans: 300, funny: 80, smart: 60);
            _concert concert = GameFixture.Concert(GameFixture.Single("S", a));
            concert.ProjectedValues.Actual_Attendance = 1.3f;
            ShowSetup setup = GameReader.Read(concert, new Config { StartingHype = 40 }).Setup;
            Assert.Equal(1, setup.CapacityFilled);
            Assert.Equal(0.5, setup.Production);
            Assert.Equal(0, setup.HypeFloor);
            Assert.Equal(40, setup.StartingHype);
            Assert.Equal(300, setup.Members[0].Fans);
            Assert.Equal(0.7, setup.Members[0].Talk, 6);

            concert.ProjectedValues.Actual_Attendance = 0.4f;
            Assert.Equal(0.4, GameReader.Read(concert, new Config()).Setup.CapacityFilled, 6);
        }

        [Fact]
        public void AConcertQualifies_WhenTheSwitchIsOnAndEveryItemIsASong()
        {
            _concert concert = GameFixture.Concert(GameFixture.Single("S", GameFixture.Idol("A")));
            Assert.True(ShowSession.Qualifies(concert));
            GameFixture.SetVariable(Switch.VarId, "0");
            Assert.False(ShowSession.Qualifies(concert));
            GameFixture.SetVariable(Switch.VarId, "1");
            Assert.True(ShowSession.Qualifies(concert));
            concert.SetListItems.Add(new _concert._mc());
            Assert.False(ShowSession.Qualifies(concert));
            Assert.False(ShowSession.Qualifies(GameFixture.Concert()));
        }
    }

    public class SessionTests : IDisposable
    {
        public SessionTests() => GameFixture.Reset();

        public void Dispose() => GameFixture.Reset();

        private static _concert ThreeSongs()
        {
            data_girls.girls[] idols = Enumerable.Range(0, 8).Select(i => GameFixture.Idol("I" + i, dance: 50 + 3 * i, vocal: 55)).ToArray();
            return GameFixture.Concert(
                GameFixture.Single("S1", idols[0], idols[1], idols[2]),
                GameFixture.Single("S2", idols[3], idols[1], idols[4]),
                GameFixture.Single("S3", idols[5], idols[6], idols[0]));
        }

        [Fact]
        public void EachSong_IsPlayedOnClick_AnimatedForItsItemOnly_AndLandsOnTheModelsHype()
        {
            _concert concert = ThreeSongs();
            concert.Hype = 50;
            ShowSession session = GameFixture.Begin(concert);
            FakeStage stage = new(concert);

            session.BeforeSong(stage);
            Assert.True(session.HypeFor((_concert._song)concert.SetListItems[0], out float change));
            Assert.False(session.HypeFor((_concert._song)concert.SetListItems[1], out _));
            stage.AddHype(change + 0.3f, false);  // a tween that overshoots a little
            session.AfterSong(stage);

            Assert.Equal((float)session.State.History[0], concert.Hype, 4);

            // The game's animation takes a last step after the song ended; a frame later the hype lands again
            stage.AddHype(-0.2f, false);
            stage.EndFrame();
            Assert.Equal((float)session.State.History[0], concert.Hype, 4);
            Assert.False(session.HypeFor((_concert._song)concert.SetListItems[0], out _));
            Assert.Equal(1, session.State.SongIndex);
        }

        [Fact]
        public void TheSecondLanding_LeavesBigChangesAndTheLastSongAlone()
        {
            _concert concert = ThreeSongs();
            concert.Hype = 50;
            ShowSession session = GameFixture.Begin(concert);
            FakeStage stage = new(concert);
            session.BeforeSong(stage);
            session.AfterSong(stage);
            stage.AddHype(5f, false);  // another mod's bonus, not animation residue
            stage.EndFrame();
            Assert.Equal((float)session.State.History[0] + 5f, concert.Hype, 4);

            stage.SongCounter = 1;
            session.BeforeSong(stage);
            session.AfterSong(stage);
            stage.EndFrame();
            stage.SongCounter = 2;
            session.BeforeSong(stage);
            session.AfterSong(stage);
            Assert.Empty(stage.NextFrameActions);  // after the last song the score takes over
        }

        [Fact]
        public void HypeChangedByAnotherMod_BetweenSongs_IsWhereTheNextSongStarts()
        {
            _concert concert = ThreeSongs();
            concert.Hype = 50;
            ShowSession session = GameFixture.Begin(concert);
            FakeStage stage = new(concert);
            stage.PlaySong(session);
            concert.Hype = 120;
            session.BeforeSong(stage);
            Assert.Equal(120, session.Pending.HypeBefore, 4);
        }

        [Fact]
        public void APopupOutOfStepWithTheShow_IsAnError()
        {
            _concert concert = ThreeSongs();
            ShowSession session = GameFixture.Begin(concert);
            Assert.Throws<InvalidOperationException>(() => session.BeforeSong(new FakeStage(concert) { SongCounter = 1 }));
        }

        [Fact]
        public void TheScore_BecomesTheConcertsHype_AndTheShowIsLogged()
        {
            string path = Path.Combine(Path.GetTempPath(), "concert-hype-test-" + Guid.NewGuid() + ".txt");
            ShowSession.LogPath = path;
            _concert concert = ThreeSongs();
            concert.Hype = 50;
            ShowSession session = GameFixture.Begin(concert);
            FakeStage stage = new(concert);
            for (int i = 0; i < 3; i++)
                stage.PlaySong(session);

            Score score;
            using (new LogRecorder())
                score = session.Finish(stage);

            Assert.Equal((float)score.Total, concert.Hype, 4);
            Assert.True(stage.Calls.Last().animate);
            Assert.Same(score, ShowSession.LastScore);
            string text = File.ReadAllText(path);
            File.Delete(path);
            Assert.Contains("Song 3: S3", text);
            Assert.Matches(@"\) roll [\d.]+ \((CriticalFailure|Failure|Success|CriticalSuccess)\) = [\d.]+ x skill", text);
            Assert.Contains("Score ", text);
        }

        [Fact]
        public void Drops_ChangeTheLineupAndHost_RefusedOnesNothing_AndResetPutsTheLineupBack()
        {
            _concert concert = ThreeSongs();
            ShowSession session = GameFixture.Begin(concert);
            List<int> changes = new();
            Action changed = () => changes.Add(1);
            ShowSession.Changed += changed;
            try
            {
                Assert.True(session.Drop(5, DropPoint.Bench, DropPoint.AtPlace(0)).Allowed);
                Assert.Equal(new[] { 5, 1, 2 }, session.Lineup);
                Assert.False(session.Drop(1, DropPoint.AtPlace(1), DropPoint.Bench).Allowed);
                Assert.Equal(new[] { 5, 1, 2 }, session.Lineup);
                Assert.Single(changes);
                session.ResetLineup();
                Assert.Equal(new[] { 0, 1, 2 }, session.Lineup);

                new FakeStage(concert).PlaySong(session);
                session.Drop(7, DropPoint.Bench, DropPoint.AtHost(0));
                Assert.Equal(7, session.Hosts[0]);
                session.Drop(7, DropPoint.AtHost(0), DropPoint.Bench);
                Assert.Equal(-1, session.Hosts[0]);
            }
            finally
            {
                ShowSession.Changed -= changed;
            }
        }

        [Fact]
        public void ATalkBreak_PlaysAsASetlistItem_ThenTheSameSongComesNext()
        {
            _concert concert = ThreeSongs();
            concert.Hype = 50;
            ShowSession session = GameFixture.Begin(concert);
            FakeStage stage = new(concert);
            stage.PlaySong(session);
            concert.Hype = 40;
            // Two hosts, on the row's last and first places: the item takes them in the row's order
            session.Drop(7, DropPoint.Bench, DropPoint.AtHost(2));
            session.Drop(6, DropPoint.Bench, DropPoint.AtHost(0));
            _concert._song second = (_concert._song)concert.SetListItems[1];

            session.StartTalkBreak(stage);
            _concert._mc talk = Assert.IsType<_concert._mc>(concert.SetListItems[1]);
            Assert.Same(second, concert.SetListItems[2]);
            Assert.Equal(4, concert.SetListItems.Count);
            Assert.Equal(new[] { session.Show.Girls[6], session.Show.Girls[7], null }, talk.Girls);
            Assert.Equal(new[] { 6, 7 }, session.PendingTalk.Hosts);
            Assert.Equal("Talk Break #1", talk.GetTitle());
            Assert.Equal(new[] { -1, -1, -1 }, session.Hosts);
            Assert.True(session.Playing);

            // The popup animates the pull, which only this item gets
            Assert.True(session.HypeForTalk(talk, out float pull));
            Assert.False(session.HypeForTalk(new _concert._mc(), out _));
            Assert.Equal((float)(session.PendingTalk.HypeAfter - 40), pull, 4);
            Assert.True(pull > 0);
            stage.AddHype(pull + 0.2f, false);
            session.AfterSong(stage);
            Assert.Equal((float)session.LastMc.HypeAfter, concert.Hype, 4);
            stage.AddHype(-0.3f, false);
            stage.EndFrame();
            Assert.Equal((float)session.LastMc.HypeAfter, concert.Hype, 4);
            Assert.False(session.Playing);
            Assert.Equal(1, session.State.SongIndex);
            Assert.Equal(1, session.State.McCharges);

            // The popup moves on to the song: matched by reference, past the inserted item
            stage.SongCounter = 2;
            stage.PlaySong(session);
            Assert.Equal(2, session.State.SongIndex);
            Assert.Null(session.LastMc);
        }

        [Fact]
        public void ATalkBreak_KeepsThePlannedChange_AndGivesAReturningIdolHerPlaceBack()
        {
            _concert concert = ThreeSongs();
            ShowSession session = GameFixture.Begin(concert);
            FakeStage stage = new(concert);
            stage.PlaySong(session);
            session.Drop(5, DropPoint.Bench, DropPoint.AtPlace(0));
            int[] planned = (int[])session.Lineup.Clone();
            session.Drop(7, DropPoint.Bench, DropPoint.AtHost(0));
            stage.PlaySong(session);
            Assert.Equal(planned, session.Lineup);

            // Song 3 (5, 6, 0) with its center out for one more song: a free fill there, and a change on place 2
            stage.PlaySong(session);
            session.State.Absence[5] = 1;
            session.Lineup = Engine.DefaultLineup(session.State);
            int fill = session.Lineup[0];
            int sub = Enumerable.Range(0, 8).First(m => !new[] { 5, 6, 0, fill }.Contains(m));
            Assert.True(session.Drop(sub, DropPoint.Bench, DropPoint.AtPlace(2)).Changed);
            int host = Enumerable.Range(0, 8).First(m => !session.Lineup.Contains(m) && m != 5);
            session.Drop(host, DropPoint.Bench, DropPoint.AtHost(0));
            // The talk break brings her back: she takes her place from the fill, and the player's change stays
            stage.PlaySong(session);
            Assert.Equal(0, session.State.Absence[5]);
            Assert.Equal(new[] { 5, 6, sub }, session.Lineup);
            Assert.DoesNotContain(fill, session.Lineup);
        }

        [Fact]
        public void APendingTalkBreak_KeepsTheShowsHype_UntilItEnds()
        {
            _concert concert = ThreeSongs();
            concert.Hype = 50;
            ShowSession session = GameFixture.Begin(concert);
            FakeStage stage = new(concert);
            stage.PlaySong(session);
            session.Drop(7, DropPoint.Bench, DropPoint.AtHost(0));
            session.StartTalkBreak(stage);
            double model = session.State.Hype;
            concert.Hype = 10;  // part way through the popup's animation
            session.SyncHype();
            Assert.Equal(model, session.State.Hype);
        }

        [Fact]
        public void APopupOutOfStepWithATalkBreak_IsAnError()
        {
            _concert concert = ThreeSongs();
            ShowSession session = GameFixture.Begin(concert);
            FakeStage stage = new(concert);
            stage.PlaySong(session);
            session.Drop(7, DropPoint.Bench, DropPoint.AtHost(0));
            session.StartTalkBreak(stage);
            // Another mod kept the popup from playing it: the next Start finds the show still in its talk break
            Assert.Throws<InvalidOperationException>(() => session.BeforeSong(stage));
        }
    }

    /// <summary>
    /// The patches, applied to the game's methods the way the game runs them.
    /// </summary>
    public class PatchTests : IDisposable
    {
        private static readonly Lazy<bool> Patched = new(() =>
        {
            // Not "tests.ConcertHype": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.ConcertHype.Behaviour");
            foreach (Type patchClass in new[]
            {
                typeof(SEvent_Concerts__concert__song_GetHype), typeof(SEvent_Concerts__concert__song_GetStaminaCost),
                typeof(SEvent_Concerts__concert__mc_GetHype), typeof(SEvent_Concerts__concert__mc_GetStaminaCost),
                typeof(SEvent_Concerts__concert_StaminaToRestore),
                typeof(SEvent_Concerts__concert_RecalcProjectedValues), typeof(SEvent_Concerts__concert__projectedValues_GetRevenue),
                typeof(SEvent_Concerts__concert__projectedValues_GetHype),
            })
                harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        });

        public PatchTests()
        {
            _ = Patched.Value;
            GameFixture.Reset();
        }

        public void Dispose() => GameFixture.Reset();

        private static _concert TwoSongs()
        {
            data_girls.girls[] idols = Enumerable.Range(0, 5).Select(i => GameFixture.Idol("I" + i, dance: 60)).ToArray();
            return GameFixture.Concert(GameFixture.Single("S1", idols[0], idols[1]), GameFixture.Single("S2", idols[2], idols[3]));
        }

        [Fact]
        public void SongHype_IsTheModelsChange_ForThePlayingSongOnly()
        {
            _concert concert = TwoSongs();
            _concert._song first = (_concert._song)concert.SetListItems[0], second = (_concert._song)concert.SetListItems[1];
            float vanilla = second.GetHype(concert);

            ShowSession session = GameFixture.Begin(concert);
            session.BeforeSong(new FakeStage(concert));
            Assert.Equal((float)session.Pending.Change, first.GetHype(concert), 4);
            Assert.Equal(vanilla, second.GetHype(concert));
        }

        [Fact]
        public void SongsCostNoRealStamina_DuringTheShow()
        {
            _concert concert = TwoSongs();
            _concert._song song = (_concert._song)concert.SetListItems[0];
            Assert.Equal(30, song.GetStaminaCost());
            GameFixture.Begin(concert);
            Assert.Equal(0, song.GetStaminaCost());
        }

        [Fact]
        public void TalkBreakHype_IsTheModelsPull_ForTheShowsTalkBreakOnly()
        {
            _concert concert = TwoSongs();
            concert.Hype = 30;
            _concert._mc planned = new();
            planned.SetGirl(GameFixture.Idol("Host", funny: 90, smart: 90), 0);
            float vanilla = planned.GetHype(concert);

            ShowSession session = GameFixture.Begin(concert);
            FakeStage stage = new(concert);
            stage.PlaySong(session);
            session.Drop(4, DropPoint.Bench, DropPoint.AtHost(0));
            session.StartTalkBreak(stage);
            _concert._mc talk = (_concert._mc)concert.SetListItems[1];
            Assert.Equal((float)(session.PendingTalk.HypeAfter - session.PendingTalk.HypeBefore), talk.GetHype(concert), 4);
            Assert.Equal(vanilla, planned.GetHype(concert));
        }

        [Fact]
        public void TalkBreaks_CostAndRestoreNoRealStamina_DuringTheShow()
        {
            _concert concert = TwoSongs(), other = TwoSongs();
            _concert._mc talk = new();
            talk.SetGirl(GameFixture.Idol("Host"), 0);
            Assert.Equal(40, talk.GetStaminaCost());
            Assert.Equal(15, concert.StaminaToRestore());
            GameFixture.Begin(concert);
            Assert.Equal(0, talk.GetStaminaCost());
            Assert.Equal(0, concert.StaminaToRestore());
            Assert.Equal(15, other.StaminaToRestore());
        }

        [Fact]
        public void SongLength_ComesFromPace_AtTheGamesOneAnchor()
        {
            MethodBase moveNext = Concert_Popup_PlaySong.TargetMethod();
            Assert.NotNull(moveNext);
            List<HarmonyLib.CodeInstruction> original = PatchProcessor.GetOriginalInstructions(moveNext);
            MethodInfo pace = AccessTools.Method(typeof(Pace), nameof(Pace.SongSeconds));
            using LogRecorder log = new();
            List<HarmonyLib.CodeInstruction> patched = Concert_Popup_PlaySong.Transpiler(original.Select(c => c.Clone()), moveNext).ToList();

            Assert.Empty(log.Messages);
            Assert.True(Pace.TranspilerApplied);
            Assert.Equal(original.Count, patched.Count);
            Assert.Single(patched, c => c.Calls(pace));
            int at = patched.FindIndex(c => c.Calls(pace));
            Assert.True(original[at].opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && (float)original[at].operand == 10f);
            Assert.Contains("songDuration", ((FieldInfo)patched[at + 1].operand).Name);
            // A second run finds its own call and changes nothing
            Assert.Single(Concert_Popup_PlaySong.Transpiler(patched, moveNext), c => c.Calls(pace));
        }

        [Fact]
        public void SongLength_IsTheConfiguredLengthDuringTheShow_AndTheGamesOtherwise()
        {
            Assert.Equal(10f, Pace.SongSeconds());
            GameFixture.Begin(TwoSongs());
            Assert.Equal(5f, Pace.SongSeconds());
            GameFixture.Begin(TwoSongs(), new Config { SongSeconds = 7.5 });
            Assert.Equal(7.5f, Pace.SongSeconds());
            GameFixture.Begin(TwoSongs(), new Config { SongSeconds = 0 });
            Assert.Equal(10f, Pace.SongSeconds());
        }

        [Fact]
        public void StartingHype_RaisesTheGamesStartOnce_NeverLowersIt()
        {
            _concert concert = TwoSongs();
            Concert_Popup popup = GameFixture.Popup(concert);
            GameFixture.Begin(concert);
            float start = 20;
            Concert_Popup_AddHype.Prefix(popup, ref start);
            Assert.Equal(50, start);
            float later = 5;
            Concert_Popup_AddHype.Prefix(popup, ref later);
            Assert.Equal(5, later);

            concert.Hype = 40;
            GameFixture.Begin(concert);
            float election = 70;
            Concert_Popup_AddHype.Prefix(popup, ref election);
            Assert.Equal(70, election);
        }

        [Fact]
        public void TheAccidentWheel_IsOff_OnlyDuringTheShow()
        {
            _concert concert = TwoSongs();
            Concert_Popup popup = GameFixture.Popup(concert);
            UnityEngine.GameObject other = (UnityEngine.GameObject)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(UnityEngine.GameObject));
            Assert.True(Concert_Popup_AnimateAccident.Prefix(popup));
            Assert.True(Concert_Popup_ShowObject.Prefix(popup, popup.Accident_Top_Container));

            GameFixture.Begin(concert);
            Assert.False(Concert_Popup_AnimateAccident.Prefix(popup));
            Assert.False(Concert_Popup_ShowObject.Prefix(popup, popup.Accident_Top_Container));
            Assert.True(Concert_Popup_ShowObject.Prefix(popup, other));
            Assert.True(Concert_Popup_AnimateAccident.Prefix(GameFixture.Popup(TwoSongs())));
        }

        [Fact]
        public void TheCardDrawButton_IsNotShown_DuringTheShow()
        {
            _concert concert = TwoSongs();
            Concert_Popup popup = GameFixture.Popup(concert);
            popup.DrawCardsButton = (UnityEngine.GameObject)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(UnityEngine.GameObject));
            Assert.True(Concert_Popup_ShowObject.Prefix(popup, popup.DrawCardsButton));
            GameFixture.Begin(concert);
            Assert.False(Concert_Popup_ShowObject.Prefix(popup, popup.DrawCardsButton));
        }

        [Fact]
        public void TalkBreaks_CantBeAddedToTheSetlist_WhileTheModIsOn()
        {
            Assert.False(Concert_New_Popup_OnAddMC.Prefix());
            GameFixture.SetVariable(Switch.VarId, "0");
            Assert.True(Concert_New_Popup_OnAddMC.Prefix());
        }

        [Fact]
        public void ActionCards_AreRemoved_ForANewStyleShowOnly()
        {
            _concert concert = TwoSongs();
            concert.Cards.Add(new SEvent_Concerts._card());
            SEvent_Concerts__concert_GenerateCards.Postfix(concert);
            Assert.Empty(concert.Cards);

            concert.Cards.Add(new SEvent_Concerts._card());
            GameFixture.SetVariable(Switch.VarId, "0");
            SEvent_Concerts__concert_GenerateCards.Postfix(concert);
            Assert.Single(concert.Cards);
        }

        [Fact]
        public void APopupOutOfStep_EndsTheShow_AndTheConcertCarriesOn()
        {
            _concert concert = TwoSongs();
            Concert_Popup popup = GameFixture.Popup(concert);
            AccessTools.FieldRefAccess<Concert_Popup, int>("songCounter")(popup) = 1;
            GameFixture.Begin(concert);
            using LogRecorder log = new();
            Concert_Popup_OnNextSongClick.Prefix(popup);
            Assert.Null(ShowSession.Current);
            Assert.Contains(log.Messages, m => m.Contains("plays as in the original game"));
        }

        [Fact]
        public void TheShowEnds_WhenItsConcertFinishes()
        {
            _concert concert = TwoSongs();
            GameFixture.Begin(concert);
            SEvent_Concerts__concert_Finish.Postfix(TwoSongs());
            Assert.NotNull(ShowSession.Current);
            SEvent_Concerts__concert_Finish.Postfix(concert);
            Assert.Null(ShowSession.Current);
        }

        [Theory]
        [InlineData(SEvent_Concerts._venue.concertHall, false)]
        [InlineData(SEvent_Concerts._venue.club, false)]
        [InlineData(SEvent_Concerts._venue.stadium, true)]
        public void Payout_IsWhat200PercentHypePaid_WhileTheRecordKeepsTheScore(SEvent_Concerts._venue venue, bool fuji)
        {
            Assert.True(Revenue.TranspilerApplied);
            if (fuji)
                GameFixture.SetVariable("FUJI_3_TICKETS", "true");
            _concert concert = TwoSongs();
            concert.Venue = venue;
            concert.Hype = 73.4f;
            GameFixture.Begin(concert);
            concert.RecalcProjectedValues();
            long expected = (long)UnityEngine.Mathf.Round(concert.ProjectedValues.Actual_Audience * concert.ProjectedValues.TicketPrice
                * Revenue.MultiplierAtFixedHype(venue) * (fuji ? 1.05f : 1f));
            Assert.True(concert.ProjectedValues.Actual_Audience > 0);
            Assert.Equal(expected, concert.ProjectedValues.Actual_Revenue);
            Assert.Equal(73, concert.ProjectedValues.Actual_Hype);
        }

        [Fact]
        public void Payout_FollowsHype_ForFinishedConcertsAndWithTheSwitchOff()
        {
            _concert finished = TwoSongs();
            finished.Hype = 80;
            finished.Status = SEvent_Tour.tour._status.finished;
            finished.RecalcProjectedValues();
            long atEighty = finished.ProjectedValues.Actual_Revenue;
            finished.Status = SEvent_Tour.tour._status.normal;
            GameFixture.SetVariable(Switch.VarId, "0");
            finished.RecalcProjectedValues();
            Assert.Equal(atEighty, finished.ProjectedValues.Actual_Revenue);
            GameFixture.SetVariable(Switch.VarId, "1");
            finished.RecalcProjectedValues();
            Assert.True(finished.ProjectedValues.Actual_Revenue > atEighty);
        }

        [Fact]
        public void PayoutFallback_MatchesTheTranspiledPayout()
        {
            _concert concert = TwoSongs();
            concert.Hype = 61;
            concert.RecalcProjectedValues();
            long transpiled = concert.ProjectedValues.Actual_Revenue;
            concert.ProjectedValues.Actual_Revenue = 0;
            Revenue.TranspilerApplied = false;
            try
            {
                SEvent_Concerts__concert_RecalcProjectedValues.Postfix(concert);
            }
            finally
            {
                Revenue.TranspilerApplied = true;
            }
            Assert.Equal(transpiled, concert.ProjectedValues.Actual_Revenue);
        }

        [Fact]
        public void Estimate_Uses200PercentHype_WhileTheProjectedHypeLabelKeepsTheGamesValue()
        {
            _concert concert = TwoSongs();
            concert.ProjectedValues.Attendance = 1f;
            float projected = concert.ProjectedValues.GetHype();
            Assert.True(projected < 2f);
            long estimate = concert.ProjectedValues.GetRevenue();
            Assert.Equal((long)UnityEngine.Mathf.Round(concert.ProjectedValues.GetNumberOfSoldTickets() * (float)concert.ProjectedValues.TicketPrice * 2f), estimate);
            Assert.Equal(projected, concert.ProjectedValues.GetHype());
        }

        [Theory]
        [InlineData(SEvent_Concerts._venue.concertHall, false)]
        [InlineData(SEvent_Concerts._venue.club, true)]
        public void WithUnofficialPatchsCurve_TheEstimateMatchesThePayout(SEvent_Concerts._venue venue, bool fuji)
        {
            Harmony harmony = new("tests.ConcertHype.UnofficialPatch");
            harmony.CreateClassProcessor(typeof(UnofficialPatch.SEvent_Concerts__concert__projectedValues_GetRevenue)).Patch();
            try
            {
                if (fuji)
                    GameFixture.SetVariable("FUJI_3_TICKETS", "true");
                _concert concert = TwoSongs();
                concert.Venue = venue;
                concert.RecalcProjectedValues();
                concert.ProjectedValues.Attendance = concert.ProjectedValues.Actual_Attendance;
                long estimate = concert.ProjectedValues.GetRevenue();
                long payout = concert.ProjectedValues.Actual_Revenue;
                Assert.True(payout > 0);
                Assert.True(Math.Abs(estimate - payout) <= Math.Max(1L, payout / 1_000_000L), $"estimate {estimate}, payout {payout}");
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }
    }
}
