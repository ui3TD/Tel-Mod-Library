using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;
using _paramType = data_girls._paramType;
using _trait = traits._trait._type;
using _venue = SEvent_Concerts._venue;

namespace ConcertRebalance.Tests
{
    /// <summary>
    /// An MC break held by these idols, worth no hype.
    /// </summary>
    public class CastItem : SEvent_Concerts._concert.ISetlistItem
    {
        private readonly List<data_girls.girls> girls;

        public CastItem(params data_girls.girls[] girls) => this.girls = new List<data_girls.girls>(girls);

        public float GetHype(SEvent_Concerts._concert Parent) => 0f;
        public bool isMC() => true;
        public void SetTitle(string val) { }
        public string GetTitle() => "";
        public void SetGirl(data_girls.girls girl, int id = 0) { }
        public bool ReplaceGirl(data_girls.girls girl) => false;
        public List<data_girls.girls> GetGirls(bool skipNull = false) => new(girls);
        public int GetSkillValue() => 0;
        public float GetStaminaCost() => 0f;
    }

    /// <summary>
    /// A concert can't leave an idol with more physical stamina than she had when it started, however much the MC breaks
    /// restored during it.
    /// </summary>
    public class StaminaCapTests
    {
        private const string OtherModId = "tests.ConcertRebalance.OtherMod";

        public StaminaCapTests()
        {
            ConcertGame.Reset();
            ConcertRebalance.RecordStartingStamina(null);
        }

        /// <summary>
        /// An idol with every stat at 40 and this much physical stamina (mental stamina 100).
        /// </summary>
        private static data_girls.girls Idol(float stamina, _trait trait = _trait.None)
        {
            data_girls.girls girl = new() { trait = trait };
            foreach (_paramType type in Enum.GetValues(typeof(_paramType)))
            {
                float value = type switch
                {
                    _paramType.physicalStamina => stamina,
                    _paramType.mentalStamina => 100f,
                    _ => 40f,
                };
                girl.parameters.Add(new data_girls.girls.param { type = type, _val = value, Parent = girl });
            }
            return girl;
        }

        private static float Stamina(data_girls.girls girl) => girl.getParam(_paramType.physicalStamina)._val;

        private static void SetStamina(data_girls.girls girl, float stamina) => girl.getParam(_paramType.physicalStamina)._val = stamina;

        private static SEvent_Concerts._concert Concert(params data_girls.girls[] cast)
        {
            SEvent_Concerts._concert concert = ConcertGame.Concert(_venue.club, 100f);
            concert.SetListItems.Add(new CastItem(cast));
            return concert;
        }

        /// <summary>
        /// Runs the mod's StartConcert postfix as the game would (the game's StartConcert opens Unity popups).
        /// </summary>
        private static void Start(SEvent_Concerts._concert concert)
        {
            SEvent_Concerts manager = (SEvent_Concerts)FormatterServices.GetUninitializedObject(typeof(SEvent_Concerts));
            manager.Concert = concert;
            SEvent_Concerts_StartConcert_Stamina.Postfix(manager);
        }

        /// <summary>
        /// Stamina another mod gives as the concert starts (its StartConcert patches run before the record) is kept.
        /// </summary>
        [Fact]
        public void StaminaGivenAsTheConcertStarts_IsKept()
        {
            data_girls.girls idol = Idol(50f);
            SEvent_Concerts._concert concert = Concert(idol);
            SetStamina(idol, 70f);
            Start(concert);
            SetStamina(idol, 90f);
            concert.Finish();

            Assert.Equal(70f, Stamina(idol));
        }

        [Fact]
        public void StaminaGainedDuringTheConcert_IsTakenBackAtTheEnd()
        {
            data_girls.girls idol = Idol(50f);
            SEvent_Concerts._concert concert = Concert(idol);
            Start(concert);
            SetStamina(idol, 80f);
            concert.Finish();

            Assert.Equal(50f, Stamina(idol));
        }

        [Fact]
        public void StaminaLostDuringTheConcert_IsKept()
        {
            data_girls.girls idol = Idol(50f);
            SEvent_Concerts._concert concert = Concert(idol);
            Start(concert);
            SetStamina(idol, 20f);
            concert.Finish();

            Assert.Equal(20f, Stamina(idol));
        }

        [Fact]
        public void IdolsOutsideTheConcert_AreUntouched()
        {
            data_girls.girls idol = Idol(50f);
            data_girls.girls other = Idol(50f);
            SEvent_Concerts._concert concert = Concert(idol);
            Start(concert);
            SetStamina(other, 80f);
            concert.Finish();

            Assert.Equal(80f, Stamina(other));
        }

        [Fact]
        public void MentalStamina_IsUntouched()
        {
            data_girls.girls idol = Idol(50f);
            SEvent_Concerts._concert concert = Concert(idol);
            Start(concert);
            SetStamina(idol, 80f);
            idol.getParam(_paramType.mentalStamina)._val = 70f;
            concert.Finish();

            Assert.Equal(70f, idol.getParam(_paramType.mentalStamina)._val);
        }

        /// <summary>
        /// The game scales stamina loss by trait (Spoiled ×2, High-maintenance ×1.5, Tenacious sometimes ignores it) and
        /// limits it per day. The cap ignores all of that and lands exactly on the start.
        /// </summary>
        [Theory]
        [InlineData(_trait.None, 0f)]
        [InlineData(_trait.Spoiled, 0f)]
        [InlineData(_trait.High_maintenance, 0f)]
        [InlineData(_trait.Tenacious, 0f)]
        [InlineData(_trait.None, -50f)]
        public void TheCap_LandsExactlyOnTheStart(_trait trait, float dailyChange)
        {
            data_girls.girls idol = Idol(30f, trait);
            idol.getParam(_paramType.physicalStamina).daily_change = dailyChange;
            SEvent_Concerts._concert concert = Concert(idol);
            Start(concert);
            SetStamina(idol, 45f);
            concert.Finish();

            Assert.Equal(30f, Stamina(idol));
            Assert.Equal(100f, idol.getParam(_paramType.mentalStamina)._val);
        }

        [Fact]
        public void FinishingWithoutAStart_ChangesNothing()
        {
            data_girls.girls idol = Idol(50f);
            SEvent_Concerts._concert concert = Concert(idol);
            SetStamina(idol, 80f);
            concert.Finish();

            Assert.Equal(80f, Stamina(idol));
        }

        [Fact]
        public void TheRecord_IsUsedOnlyOnce()
        {
            data_girls.girls idol = Idol(50f);
            SEvent_Concerts._concert concert = Concert(idol);
            Start(concert);
            concert.Finish();
            SetStamina(idol, 80f);
            concert.Finish();

            Assert.Equal(80f, Stamina(idol));
        }

        [Fact]
        public void StartingAnotherConcert_ReplacesTheRecord()
        {
            data_girls.girls abandonedCast = Idol(50f);
            data_girls.girls idol = Idol(50f);
            SEvent_Concerts._concert abandoned = Concert(abandonedCast);
            SEvent_Concerts._concert concert = Concert(idol);
            Start(abandoned);
            Start(concert);
            SetStamina(abandonedCast, 80f);
            abandoned.Finish();
            concert.Finish();

            Assert.Equal(80f, Stamina(abandonedCast));
        }

        /// <summary>
        /// A mod that finishes some other concert (say, a simulated one) mid-way neither triggers the cap nor uses up the
        /// record of the concert being played.
        /// </summary>
        [Fact]
        public void FinishingADifferentConcert_LeavesTheRecordForThePlayedOne()
        {
            data_girls.girls idol = Idol(50f);
            SEvent_Concerts._concert played = Concert(idol);
            SEvent_Concerts._concert other = Concert(idol);
            Start(played);
            SetStamina(idol, 80f);
            other.Finish();
            Assert.Equal(80f, Stamina(idol));

            played.Finish();
            Assert.Equal(50f, Stamina(idol));
        }

        /// <summary>
        /// Another mod's Finish prefix (even one asking to run early) and postfix that give stamina for finishing a concert
        /// keep their bonus.
        /// </summary>
        [Fact]
        public void OtherModsFinishBonuses_AreNotCapped()
        {
            Harmony otherMod = new(OtherModId);
            MethodInfo finish = AccessTools.Method(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish));
            try
            {
                otherMod.Patch(finish,
                    prefix: new HarmonyMethod(typeof(StaminaCapTests), nameof(OtherModBonus)) { priority = Priority.High },
                    postfix: new HarmonyMethod(typeof(StaminaCapTests), nameof(OtherModBonus)));

                data_girls.girls idol = Idol(50f);
                SEvent_Concerts._concert concert = Concert(idol);
                Start(concert);
                SetStamina(idol, 80f);
                concert.Finish();

                Assert.Equal(70f, Stamina(idol));
            }
            finally
            {
                otherMod.UnpatchSelf();
            }
        }

        private static void OtherModBonus(SEvent_Concerts._concert __instance)
        {
            foreach (data_girls.girls girl in __instance.GetGirls())
                girl.addParam(_paramType.physicalStamina, 10f);
        }

        /// <summary>
        /// The record is taken after other mods' StartConcert patches, and the cap runs before other mods' Finish patches.
        /// The unlock window closes before other mods' StartConcert postfixes.
        /// </summary>
        [Theory]
        [InlineData(typeof(SEvent_Concerts_StartConcert_Stamina), "Postfix", Priority.Last)]
        [InlineData(typeof(SEvent_Concerts__concert_Finish_StaminaCap), "Prefix", Priority.First)]
        [InlineData(typeof(SEvent_Concerts_StartConcert), "Postfix", Priority.First)]
        public void PatchPriorities(Type patchClass, string patchMethod, int priority)
        {
            MethodInfo patch = AccessTools.Method(patchClass, patchMethod);
            HarmonyPriority attribute = patch.GetCustomAttribute<HarmonyPriority>();
            Assert.NotNull(attribute);
            Assert.Equal(priority, attribute.info.priority);
        }
    }
}
