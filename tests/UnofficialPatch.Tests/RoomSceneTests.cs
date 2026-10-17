using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// Automated clinics take injured idols whatever their training preference except 'don't train', and never take
    /// an idol who is busy in another room.
    /// </summary>
    public class ClinicTests
    {
        private static readonly MethodInfo CanAutoTrain = AccessTools.Method(typeof(agency._room), "CanAutoTrain");

        public ClinicTests() => TestGame.Reset();

        [Theory]
        [InlineData(data_girls.girls._training.vocal, true)]
        [InlineData(data_girls.girls._training.dance, true)]
        [InlineData(data_girls.girls._training.visual, true)]
        [InlineData(data_girls.girls._training.cute, true)]
        [InlineData(data_girls.girls._training.sexy, true)]
        [InlineData(data_girls.girls._training.default_, true)]
        [InlineData(data_girls.girls._training.dont_train, false)]
        public void ClinicAcceptsAnyTrainingPreference(data_girls.girls._training training, bool expected)
        {
            agency.GirlsBannedFromAutoTasks = new List<data_girls.girls>();

            Assert.Equal(expected, AutoTrain(agency._type.doctorsOffice, new data_girls.girls { training = training }));
        }

        [Fact]
        public void ClinicStillRejectsSickAndBannedIdols()
        {
            data_girls.girls sick = new() { training = data_girls.girls._training.vocal, status = data_girls._status.injured };
            data_girls.girls banned = new() { training = data_girls.girls._training.vocal };
            agency.GirlsBannedFromAutoTasks = new List<data_girls.girls> { banned };

            Assert.False(AutoTrain(agency._type.doctorsOffice, sick));
            Assert.False(AutoTrain(agency._type.doctorsOffice, banned));
        }

        [Fact]
        public void OtherRoomsKeepTheirPreferenceCheck()
        {
            agency.GirlsBannedFromAutoTasks = new List<data_girls.girls>();

            Assert.False(AutoTrain(agency._type.danceStudio, new data_girls.girls { training = data_girls.girls._training.vocal }));
        }

        [Fact]
        public void ClinicRefusesIdolsBusyElsewhere()
        {
            agency._room clinic = new() { type = agency._type.doctorsOffice, staffer = new staff._staff() };
            agency._room studio = new() { type = agency._type.danceStudio };
            agency._room otherClinic = new() { type = agency._type.doctorsOffice };
            data_girls.girls free = new();
            data_girls.girls training = new() { status = data_girls._status.practice, room = studio };
            data_girls.girls leaving = new() { status = data_girls._status.announced_graduation, room = studio };
            studio.girl = leaving;
            data_girls.girls treated = new() { status = data_girls._status.injured, room = otherClinic };
            otherClinic.girl = treated;

            Assert.True(clinic.canAssign(free));
            Assert.False(clinic.canAssign(training));
            Assert.False(clinic.canAssign(leaving));
            Assert.False(clinic.canAssign(treated));
        }

        [Fact]
        public void ClinicIgnoresLinksToRoomsThatNoLongerHoldTheIdol()
        {
            agency._room clinic = new() { type = agency._type.doctorsOffice, staffer = new staff._staff() };
            Cafes._cafe cafe = new() { ID = 0 };
            Cafes.Cafes_ = new List<Cafes._cafe> { cafe };
            agency._room cafeRoom = new() { type = agency._type.cafeAndShop, TheaterID = 0 };
            data_girls.girls exWorker = new() { room = cafeRoom };
            data_girls.girls sickWorker = new() { status = data_girls._status.injured, room = cafeRoom };
            data_girls.girls leavingWorker = new() { status = data_girls._status.announced_graduation, room = cafeRoom };
            cafe.WorkingGirls.AddRange(new[] { sickWorker, leavingWorker });

            Assert.True(clinic.canAssign(exWorker));
            Assert.True(clinic.canAssign(sickWorker));
            Assert.False(clinic.canAssign(leavingWorker));
        }

        private static bool AutoTrain(agency._type type, data_girls.girls girl)
        {
            return (bool)CanAutoTrain.Invoke(new agency._room { type = type }, new object[] { girl });
        }
    }

    /// <summary>
    /// Idols don't get stuck after hanging out in the Break Room, and a delayed Break Room scene only starts if its
    /// room and idols are still free.
    /// </summary>
    public class SceneTests : IDisposable
    {
        private static readonly MethodInfo AssignScene = AccessTools.Method(typeof(agency._room), nameof(agency._room.assign),
            new[] { typeof(Scenes.type), typeof(List<data_girls.girls>) });

        // Rooms the scene fix ended a scene in
        private static readonly List<agency._room> Finished = new();

        private static readonly FieldInfo CurrentAgency = AccessTools.Field(typeof(agency), nameof(agency._this));

        private readonly object savedAgency = CurrentAgency.GetValue(null);
        private readonly Action<agency._room> savedFinishScene = agency__room_CancelJob.FinishScene;
        private readonly Action<data_girls.girls> savedSetNormal = Scenes_OnNewDay.SetNormal;

        public SceneTests()
        {
            TestGame.Reset();
            Finished.Clear();
            agency__room_CancelJob.FinishScene = Finished.Add;
            Scenes_OnNewDay.SetNormal = girl => girl.status = data_girls._status.normal;
        }

        public void Dispose()
        {
            CurrentAgency.SetValue(null, savedAgency);
            agency__room_CancelJob.FinishScene = savedFinishScene;
            Scenes_OnNewDay.SetNormal = savedSetNormal;
        }

        private static agency Agency(params agency._room[] rooms)
        {
            agency owner = TestGame.Component<agency>();
            agency._floor floor = new();
            floor.floor.AddRange(rooms);
            owner.floors = new List<agency._floor> { floor };
            return owner;
        }

        private static agency._room Recreation(agency._room._status status = agency._room._status.normal, params data_girls.girls[] sceneGirls)
        {
            return new agency._room { type = agency._type.recreation_room, status = status, sceneGirls = sceneGirls.ToList() };
        }

        [Fact]
        public void CancellingARoomInASceneEndsTheScene()
        {
            data_girls.girls idol = TestGame.Registered(1, data_girls._status.scene);
            agency._room room = Recreation(agency._room._status.scene, idol);

            agency__room_CancelJob.Prefix(room, __runOriginal: true);

            Assert.Equal(new[] { room }, Finished);
        }

        [Fact]
        public void CancellingOtherRoomsLeavesScenesAlone()
        {
            agency__room_CancelJob.Prefix(Recreation(), __runOriginal: true);
            agency__room_CancelJob.Prefix(new agency._room { type = agency._type.danceStudio }, __runOriginal: true);

            Assert.Empty(Finished);
        }

        [Fact]
        public void SkippedCancelDoesNothing()
        {
            agency._room room = Recreation(agency._room._status.scene, TestGame.Registered(1, data_girls._status.scene));

            agency__room_CancelJob.Prefix(room, __runOriginal: false);

            Assert.Empty(Finished);
        }

        [Fact]
        public void DelayedStartCallsTheCheck()
        {
            MethodBase moveNext = AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(Scenes), nameof(Scenes.AssignSceneWithDelay)));
            List<CodeInstruction> codes = Scenes_AssignSceneWithDelay.Transpiler(PatchProcessor.GetOriginalInstructions(moveNext)).ToList();

            Assert.Contains(codes, c => c.operand is MethodInfo m && m.Name == nameof(Scenes_AssignSceneWithDelay.StartDelayedScene));
            Assert.DoesNotContain(codes, c => ReferenceEquals(c.operand, AssignScene));
            Assert.Contains(Harmony.GetPatchInfo(moveNext).Transpilers, p => p.PatchMethod.DeclaringType == typeof(Scenes_AssignSceneWithDelay));
        }

        [Fact]
        public void DelayedStartWithoutTheCallWarns()
        {
            Scenes_AssignSceneWithDelay.Transpiler(Enumerable.Empty<CodeInstruction>()).ToList();

            Assert.Contains(Log.Of(LogType.Warning), m => m.Contains("AssignSceneWithDelay"));
        }

        [Theory]
        [InlineData(data_girls._status.normal, true)]
        [InlineData(data_girls._status.announced_graduation, true)]
        [InlineData(data_girls._status.injured, false)]
        [InlineData(data_girls._status.depressed, false)]
        [InlineData(data_girls._status.hiatus, false)]
        [InlineData(data_girls._status.graduated, false)]
        [InlineData(data_girls._status.practice, false)]
        [InlineData(data_girls._status.scene, false)]
        public void DelayedSceneStartsOnlyIfItsIdolsAreStillFree(data_girls._status status, bool starts)
        {
            agency._room room = Recreation();
            CurrentAgency.SetValue(null, Agency(room));
            List<data_girls.girls> girls = new() { TestGame.Registered(1), TestGame.Registered(2, status) };

            Assert.Equal(starts, Scenes_AssignSceneWithDelay.CanStart(room, girls));
        }

        [Fact]
        public void DelayedSceneNeedsItsRoomStillFree()
        {
            agency._room busy = Recreation(agency._room._status.scene);
            agency._room demolished = Recreation();
            CurrentAgency.SetValue(null, Agency(busy));
            List<data_girls.girls> girls = new() { TestGame.Registered(1), TestGame.Registered(2) };

            Assert.False(Scenes_AssignSceneWithDelay.CanStart(busy, girls));
            Assert.False(Scenes_AssignSceneWithDelay.CanStart(demolished, girls));
        }

        [Fact]
        public void DelayedSceneFromBeforeALoadDoesNotStart()
        {
            agency._room room = Recreation();
            CurrentAgency.SetValue(null, Agency(room));
            TestGame.Registered(1);
            List<data_girls.girls> oldGirls = new() { new data_girls.girls { id = 1 } };

            Assert.False(Scenes_AssignSceneWithDelay.CanStart(room, oldGirls));
        }

        [Fact]
        public void NewDayRepairsOnlyIdolsNoSceneHolds()
        {
            data_girls.girls inScene = TestGame.Registered(1, data_girls._status.scene);
            data_girls.girls orphan = TestGame.Registered(2, data_girls._status.scene);
            data_girls.girls cancelled = TestGame.Registered(3, data_girls._status.scene);
            data_girls.girls injured = TestGame.Registered(4, data_girls._status.injured);

            Scenes_OnNewDay.Prefix(Agency(Recreation(agency._room._status.scene, inScene), Recreation(agency._room._status.normal, cancelled, injured)));

            Assert.Equal(data_girls._status.scene, inScene.status);
            Assert.Equal(data_girls._status.normal, orphan.status);
            Assert.Equal(data_girls._status.normal, cancelled.status);
            Assert.Equal(data_girls._status.injured, injured.status);
        }
    }
}
