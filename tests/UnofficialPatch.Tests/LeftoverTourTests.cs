using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;
using SpecialEvent = SpecialEvents_Manager._specialEvent;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A save with an unfinished tour that no tour handler holds, such as the second tour in production when a mod
    /// that runs two tours at once is removed. Each event card acts on its own tour: a leftover moves into an empty
    /// slot; while the slot holds another tour, Cancel cancels the leftover alone, Launch waits with a message, and a
    /// drop staffs the leftover.
    /// </summary>
    public class LeftoverTourTests : IDisposable
    {
        private static readonly AccessTools.FieldRef<SEvent_Button_Tour, SEvent_Tour> CardHandler =
            AccessTools.FieldRefAccess<SEvent_Button_Tour, SEvent_Tour>("_Sevent_Tour");
        private static readonly AccessTools.FieldRef<SEvent_Button_Tour, bool> CardDisabled =
            AccessTools.FieldRefAccess<SEvent_Button_Tour, bool>("disabled");

        private readonly SEvent_Tour handler = Fake<SEvent_Tour>();
        private readonly SEvent_Tour.tour leftover;
        private readonly SEvent_Tour.tour other;
        private readonly SpecialEvent worldRecord = new() { Type = SpecialEvents_Manager._type.WorldTour };
        private readonly List<agency._room> rooms = new();
        private readonly List<SEvent_Tour> redrawn = new();

        private readonly Func<SEvent_Tour, SEvent_Tour[]> gameHandlers = LeftoverTours.Handlers;
        private readonly Func<List<agency._room>> gameRooms = LeftoverTours.Rooms;
        private readonly Action<SEvent_Tour> gameRedraw = LeftoverTours.RedrawEventList;

        public LeftoverTourTests()
        {
            TestGame.Reset();
            LeftoverTours.ResetReports();
            LeftoverTours.Dropped = null;
            SEvent_Tour.activeRoom = null;
            SEvent_Tour.Tours.Clear();
            SpecialEvents_Manager.SpecialEvents.Clear();
            SpecialEvents_Manager.SpecialEvents.Add(worldRecord);
            Language.Data.Remove(LeftoverTours.SlotTakenKey);

            leftover = Tour();
            other = Tour();
            LeftoverTours.Handlers = _ => new[] { handler };
            LeftoverTours.Rooms = () => rooms;
            LeftoverTours.RedrawEventList = h => redrawn.Add(h);
        }

        public void Dispose()
        {
            LeftoverTours.Handlers = gameHandlers;
            LeftoverTours.Rooms = gameRooms;
            LeftoverTours.RedrawEventList = gameRedraw;
            LeftoverTours.Dropped = null;
        }

        private static T Fake<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        private static SEvent_Tour.tour Tour()
        {
            SEvent_Tour.tour tour = new();
            SEvent_Tour.Tours.Add(tour);
            return tour;
        }

        private SEvent_Button_Tour Card(SEvent_Tour.tour tour)
        {
            SEvent_Button_Tour card = Fake<SEvent_Button_Tour>();
            card.Tour = tour;
            CardHandler(card) = handler;
            return card;
        }

        private agency._room Room(SEvent_Tour.tour tour)
        {
            agency._room room = Fake<agency._room>();
            room.tour = tour;
            rooms.Add(room);
            return room;
        }

        private static bool Launch(SEvent_Button_Tour card, bool runOriginal = true) => SEvent_Button_Tour_OnLaunch.Prefix(card, runOriginal);
        private static bool Cancel(SEvent_Button_Tour card, bool runOriginal = true) => SEvent_Button_Tour_OnCancel.Prefix(card, runOriginal);

        private bool Drop(SEvent_Tour.tour tour, agency._room room, bool runOriginal = true)
        {
            DragAndDropManager_OnMouseUp_Tour.Prefix(tour);
            try
            {
                return SEvent_Tour_Assign.Prefix(handler, room, runOriginal);
            }
            finally
            {
                DragAndDropManager_OnMouseUp_Tour.Finalizer(null);
            }
        }

        // The slot is empty: the leftover moves in, then the game's own method runs on it

        [Fact]
        public void EmptySlot_Launch_MovesTheLeftoverIn()
        {
            Assert.True(Launch(Card(leftover)));
            Assert.Same(leftover, handler.Tour);
            Assert.Empty(Seams.Notifications);
        }

        /// <summary>
        /// Without the fix, the game's CancelTour looked for the room whose tour equals the empty slot: the first
        /// room with no tour job, whose job (a training, a treatment, a date) it ended.
        /// </summary>
        [Fact]
        public void EmptySlot_Cancel_MovesTheLeftoverIn()
        {
            Room(null);

            Assert.True(Cancel(Card(leftover)));

            Assert.Same(leftover, handler.Tour);
            Assert.Empty(Seams.CancelledJobs);
        }

        [Fact]
        public void EmptySlot_Drop_MovesTheLeftoverIn()
        {
            agency._room room = Room(null);

            Assert.True(Drop(leftover, room));

            Assert.Same(leftover, handler.Tour);
            Assert.Empty(Seams.RoomsAssigned);
        }

        // The slot holds another tour

        [Fact]
        public void SlotTaken_Cancel_CancelsOnlyTheLeftover_AndEveryRoomOnIt()
        {
            handler.Tour = other;
            agency._room production = Room(leftover);
            agency._room logistics = Room(leftover);
            Room(other);
            Room(null);

            Assert.False(Cancel(Card(leftover)));

            Assert.Equal(new[] { production, logistics }, Seams.CancelledJobs);
            Assert.Equal(new[] { other }, SEvent_Tour.Tours);
            Assert.Same(other, handler.Tour);
            // The slot's tour keeps the World Tour in production
            Assert.Equal(SpecialEvent._status.production, worldRecord.Status);
            Assert.Equal(new[] { handler }, redrawn);
        }

        [Fact]
        public void SlotTaken_Launch_Waits_AndSaysWhy()
        {
            handler.Tour = other;

            Assert.False(Launch(Card(leftover)));

            Assert.Same(other, handler.Tour);
            Assert.Equal(new[] { LeftoverTours.SlotTakenFallback }, Seams.Notifications);
        }

        [Fact]
        public void SlotTaken_Launch_UsesTheTranslatedMessage()
        {
            handler.Tour = other;
            Language.Data[LeftoverTours.SlotTakenKey] = "translated";
            try
            {
                Launch(Card(leftover));
            }
            finally
            {
                Language.Data.Remove(LeftoverTours.SlotTakenKey);
            }

            Assert.Equal(new[] { "translated" }, Seams.Notifications);
        }

        /// <summary>
        /// A room keeps its own tour, so staff can work on the leftover while another tour is in the slot.
        /// </summary>
        [Fact]
        public void SlotTaken_Drop_StaffsTheLeftover()
        {
            handler.Tour = other;
            agency._room room = Room(null);

            Assert.False(Drop(leftover, room));

            Assert.Equal(new[] { (room, leftover) }, Seams.RoomsAssigned);
            Assert.Same(room, SEvent_Tour.activeRoom);
            Assert.Same(other, handler.Tour);
        }

        // Nothing to repair

        [Fact]
        public void TheSlotsOwnTour_IsTheGames()
        {
            handler.Tour = leftover;

            Assert.True(Launch(Card(leftover)));
            Assert.True(Cancel(Card(leftover)));
            Assert.True(Drop(leftover, Room(null)));

            Assert.Empty(Seams.Notifications);
            Assert.Empty(Seams.CancelledJobs);
            Assert.Empty(Seams.RoomsAssigned);
        }

        /// <summary>
        /// Another mod's handler (such as National Tour's) holds its own tour: that tour isn't a leftover.
        /// </summary>
        [Fact]
        public void TourHeldByAnotherHandler_IsTheGames()
        {
            SEvent_Tour second = Fake<SEvent_Tour>();
            second.Tour = leftover;
            LeftoverTours.Handlers = _ => new[] { handler, second };

            Assert.True(Launch(Card(leftover)));
            Assert.True(Cancel(Card(leftover)));

            Assert.Null(handler.Tour);
            Assert.Empty(Seams.Notifications);
        }

        [Fact]
        public void FinishedTour_IsTheGames()
        {
            leftover.Status = SEvent_Tour.tour._status.finished;
            handler.Tour = other;

            Assert.True(Cancel(Card(leftover)));
            Assert.Contains(leftover, SEvent_Tour.Tours);
        }

        [Fact]
        public void TourNotInTheGamesList_IsTheGames()
        {
            SEvent_Tour.Tours.Remove(leftover);

            Assert.True(Launch(Card(leftover)));
            Assert.Null(handler.Tour);
        }

        [Fact]
        public void DisabledCard_IsTheGames()
        {
            SEvent_Button_Tour card = Card(leftover);
            CardDisabled(card) = true;

            Assert.True(Launch(card));
            Assert.Null(handler.Tour);
        }

        [Fact]
        public void ConcertCard_IsTheGames()
        {
            Assert.True(Launch(Card(null)));
            Assert.True(Cancel(Card(null)));
            Assert.Null(handler.Tour);
        }

        /// <summary>
        /// Another mod's prefix skipped the game's method: the fix leaves it skipped and changes nothing.
        /// </summary>
        [Fact]
        public void AnotherModSkippedTheMethod_NothingChanges()
        {
            Assert.True(Launch(Card(leftover), runOriginal: false));
            Assert.True(Cancel(Card(leftover), runOriginal: false));
            Assert.True(Drop(leftover, Room(null), runOriginal: false));

            Assert.Null(handler.Tour);
            Assert.Empty(Seams.RoomsAssigned);
        }

        /// <summary>
        /// Assign outside a drop (another mod calling it) is the game's.
        /// </summary>
        [Fact]
        public void AssignOutsideADrop_IsTheGames()
        {
            handler.Tour = other;

            Assert.True(SEvent_Tour_Assign.Prefix(handler, Room(null), true));
            Assert.Empty(Seams.RoomsAssigned);
        }

        [Fact]
        public void DroppedTour_IsForgottenEvenWhenTheDropThrows()
        {
            DragAndDropManager_OnMouseUp_Tour.Prefix(leftover);
            Exception thrown = new InvalidOperationException("drop failed");

            Assert.Same(thrown, DragAndDropManager_OnMouseUp_Tour.Finalizer(thrown));
            Assert.Null(LeftoverTours.Dropped);
        }

        // Fail soft

        [Fact]
        public void AnError_LetsTheGamesMethodRun_AndIsReportedOnce()
        {
            LeftoverTours.Handlers = _ => throw new InvalidOperationException("broken");

            Assert.True(Launch(Card(leftover)));
            Assert.True(Launch(Card(leftover)));

            Assert.Single(Seams.Notifications);
            Assert.Contains("SEvent_Button_Tour_OnLaunch", Seams.Notifications[0]);
            Assert.Contains(Log.Messages, m => m.message.Contains("failed; the game's own method ran instead"));
        }
    }
}
