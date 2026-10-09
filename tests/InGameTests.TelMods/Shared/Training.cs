using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Training-tick measurements shared by the training mods' checks. They copy the game's
    /// per-tick formula and know one game method, so they live with the tests that rely on them
    /// rather than in the runner (see its README, "Adding a helper to the runner").
    /// </summary>
    internal static class Training
    {
        private static List<KeyValuePair<data_girls._paramType, float>> addParamCalls;

        /// <summary>Ticks per in-game day at the current clock speed; DoGirlTraining divides a day's stamina cost by this.</summary>
        public static float TicksPerDay => Mathf.Floor(1440f / (float)(staticVars.dateTimeAddMinutesPerSecond / staticVars.dateTimeDivider));

        /// <summary>The idol's room, which must be a training room (not a dressing room) where she trains a stat (not stamina).</summary>
        public static agency._room Room(int girlId)
        {
            agency._room room = Game.RoomOf(Game.Girl(girlId));
            data_girls._paramType? param = room.trainingParam();
            if (param == null || room.type == agency._type.dressingRoom
                || param == data_girls._paramType.physicalStamina || param == data_girls._paramType.mentalStamina)
            {
                throw new InvalidOperationException("Idol " + girlId + " isn't training a stat in a training room; IM-InGameTests/fixtures/README.md lists who trains");
            }
            return room;
        }

        /// <summary>
        /// Runs one DoGirlTraining tick in the room and returns the (stat, amount) of every addParam
        /// call it makes, as the caller passed them. The calls are skipped, so the idol's stamina is
        /// unchanged, and the trained stat is put back.
        /// </summary>
        public static List<KeyValuePair<data_girls._paramType, float>> TickAddParams(agency._room room)
        {
            data_girls.girls girl = room.girl;
            data_girls._paramType trained = room.trainingParam().Value;
            float trainedBefore = girl.getParam(trained).val;
            addParamCalls = new List<KeyValuePair<data_girls._paramType, float>>();
            try
            {
                MethodInfo addParam = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.addParam));
                using (TestTools.Spy(addParam, prefix: AccessTools.Method(typeof(Training), nameof(RecordAddParam))))
                    AccessTools.Method(typeof(agency._room), "DoGirlTraining").Invoke(room, null);
                return addParamCalls;
            }
            finally
            {
                addParamCalls = null;
                girl.getParam(trained).val = trainedBefore;
            }
        }

        // Records the call and skips it.
        private static bool RecordAddParam(data_girls._paramType type, float val)
        {
            if (addParamCalls == null)
                return true;
            addParamCalls.Add(new KeyValuePair<data_girls._paramType, float>(type, val));
            return false;
        }
    }
}
