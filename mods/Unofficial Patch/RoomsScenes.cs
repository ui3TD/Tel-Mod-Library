using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;

namespace UnofficialPatch
{
    // Fixes clinics taking an idol who is already working in another room: canAssign returns true for clinics
    // before the busy check other rooms make. A room link alone doesn't mean she is busy (the game leaves it set
    // after some jobs end), so only a room that still holds her counts.
    [HarmonyPatch(typeof(agency._room), nameof(agency._room.canAssign), new[] { typeof(data_girls.girls), typeof(data_girls._paramType?) })]
    public class agency__room_canAssign
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<agency__room_canAssign>(ex);

        public static void Postfix(agency._room __instance, data_girls.girls _girl, ref bool __result)
        {
            if (!__result || __instance.type != agency._type.doctorsOffice || _girl == null)
                return;
            if (_girl.status == data_girls._status.practice || (_girl.room != __instance && Holds(_girl.room, _girl)))
                __result = false;
        }

        // The room is working with her: its own idol, or a cafe shift she is still on (a sick idol isn't working).
        public static bool Holds(agency._room room, data_girls.girls girl)
        {
            if (room == null)
                return false;
            if (room.girl == girl)
                return true;
            return room.type == agency._type.cafeAndShop && !girl.IsSick() && room.GetCafe()?.WorkingGirls.Contains(girl) == true;
        }
    }

    // Fixes idols stuck in a Break Room scene after the room is demolished: CancelJob sets the room back to normal
    // without FinishScene, so its idols keep the scene status with no scene behind it, and no automatic room or new
    // scene takes them. End the scene first with the game's FinishScene, which resets only idols still in it.
    // Runs after other prefixes so they still see the scene. Idempotent: a finished scene leaves the room normal.
    [HarmonyPatch(typeof(agency._room), nameof(agency._room.CancelJob))]
    public class agency__room_CancelJob
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<agency__room_CancelJob>(ex);

        // The game's FinishScene draws sprites; tests replace it
        public static Action<agency._room> FinishScene = room => room.FinishScene();

        [HarmonyPriority(Priority.Last)]
        public static void Prefix(agency._room __instance, bool __runOriginal)
        {
            if (__runOriginal && __instance.status == agency._room._status.scene && __instance.sceneGirls != null)
                FinishScene(__instance);
        }
    }

    // Fixes Break Room scenes starting with idols who are no longer free. OnNewDay picks free idols and a free room,
    // and AssignSceneWithDelay starts the scene up to 19 hours later without checking either again. An idol injured,
    // depressed, sent on hiatus, graduated or put in training meanwhile was put in the scene (and set back to normal
    // when it ended, curing her), and a scene whose room was demolished, or that was picked before a save was
    // loaded, stranded its idols. Start the scene only if it can still happen; otherwise the next day picks new ones.
    // Pattern-based, so it does nothing if another mod has replaced the call.
    [HarmonyPatch]
    public class Scenes_AssignSceneWithDelay
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Scenes_AssignSceneWithDelay>(ex);

        private static readonly MethodInfo Assign = AccessTools.Method(typeof(agency._room), nameof(agency._room.assign),
            new[] { typeof(Scenes.type), typeof(List<data_girls.girls>) });
        private static readonly MethodInfo StartMethod = AccessTools.Method(typeof(Scenes_AssignSceneWithDelay), nameof(StartDelayedScene));
        private static readonly FieldInfo CurrentAgency = AccessTools.Field(typeof(agency), nameof(agency._this));

        public static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(Scenes), nameof(Scenes.AssignSceneWithDelay)));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int replaced = 0;
            foreach (CodeInstruction code in codes)
            {
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && ReferenceEquals(code.operand, Assign))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = StartMethod;
                    replaced++;
                }
            }
            if (replaced == 0)
                PatchLog.WarnOncePerPatch<Scenes_AssignSceneWithDelay>("no scene start left to check (another mod may have changed it).");
            return codes;
        }

        public static void StartDelayedScene(agency._room room, Scenes.type type, List<data_girls.girls> girls)
        {
            if (CanStart(room, girls))
                room.assign(type, girls);
        }

        // The room is still one of the agency's and free, and every idol is still free by the test OnNewDay chose
        // them with. Rooms and idols from before a load aren't in the current lists.
        public static bool CanStart(agency._room room, List<data_girls.girls> girls)
        {
            if (room == null || girls == null || room.status != agency._room._status.normal)
                return false;
            if (CurrentAgency.GetValue(null) is agency owner && !owner.allRooms().Contains(room))
                return false;
            List<data_girls.girls> free = data_girls.GetFreeGirls(includeSceneGirls: false);
            return girls.All(free.Contains);
        }
    }

    // Repairs idols already stuck in the scene status (from saves made before these fixes, or anything else that ends
    // a scene room's work without FinishScene): sets back to normal every idol in the scene status that no room's
    // scene holds, before the day's scenes are picked. Idempotent: once nobody is stuck, it changes nothing.
    [HarmonyPatch(typeof(Scenes), nameof(Scenes.OnNewDay))]
    public class Scenes_OnNewDay
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Scenes_OnNewDay>(ex);

        // Leaving the scene status makes the game look for the idol's scene through Camera.main; tests replace it
        public static Action<data_girls.girls> SetNormal = girl => girl.SetStatus(data_girls._status.normal);

        public static void Prefix(agency ___Agency) => Repair(___Agency);

        public static void Repair(agency owner)
        {
            if (owner is null || data_girls.girl == null)
                return;
            List<agency._room> scenes = owner.allRooms().Where(r => r.status == agency._room._status.scene && r.sceneGirls != null).ToList();
            foreach (data_girls.girls girl in data_girls.girl.ToList())
            {
                if (girl != null && girl.status == data_girls._status.scene && !scenes.Any(r => r.sceneGirls.Contains(girl)))
                    SetNormal(girl);
            }
        }
    }
}
