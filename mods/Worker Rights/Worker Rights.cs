using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using static WorkerRights.WorkerRights;

namespace WorkerRights
{
    public class WorkerRights
    {
        public const int FIRE_MIN_DAYS = 30;
        public const int DEF_SALARY = 20000;
        public const float MAXFAME_SALARY_EARN_COEFF = 0.1f;
        public const int GRAD_DAYS_PENALTY = 10;
        public const int GRAD_DAYS_PENALTY_SEVERE = 30;
    }

    // Staff cannot be fired using scandal points within the first month
    [HarmonyPatch(typeof(staff._staff), nameof(staff._staff.CanFire))]
    public class staff__staff_CanFire
    {
        public static void Postfix(staff._staff __instance, ref bool __result)
        {
            if (__instance != null && (staticVars.dateTime - __instance.HireDate).Days < FIRE_MIN_DAYS)
            {
                __result = false;
            }
        }
    }
    // 20000 yen/wk is the expected starting salary for 100% satisfaction
    // Never lower a larger expectation from vanilla or another mod
    // Why twice DEF_SALARY: the game's GetExpectedSalary_Total clamps an idol's average earnings to between
    // half and double this base. A new idol earns nothing, so she expects half of it: exactly DEF_SALARY.
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetExpectedSalary))]
    public class data_girls_girls_GetExpectedSalary
    {
        public static void Postfix(ref int __result, data_girls.girls __instance)
        {
            if (__instance != null && __instance.GetFameLevel() < 1f)
            {
                __result = Math.Max(__result, DEF_SALARY * 2);
            }
        }
    }

    // Each week the graduation date moves with how the idol is treated and used, as the game intended.
    // The game works this out in Graduation_Date_Update but discards it (it doesn't store the result of AddDays).
    // Its numbers are retuned so busy idols don't stay forever; see GraduationWeek.
    // The prefix runs last and the postfix first, so the date change made inside the method itself
    // (by a mod that makes the game store its own value) is replaced, not added to.
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Date_Update))]
    public class data_girls_girls_Graduation_Date_Update
    {
        [HarmonyPriority(Priority.Last)]
        public static void Prefix(data_girls.girls __instance, out DateTime? __state)
        {
            __state = __instance?.Graduation_Date;
        }

        [HarmonyPriority(Priority.First)]
        public static void Postfix(data_girls.girls __instance, bool __result, bool __runOriginal, DateTime? __state)
        {
            // __result: a graduation was just announced or happened, and the game set the date itself
            if (__instance == null || __result || __instance.status == data_girls._status.announced_graduation || __instance.status == data_girls._status.graduated)
                return;

            // Another mod replaced the method with its own model: keep only the low-salary penalty
            if (!__runOriginal)
            {
                float penalty = GraduationWeek.LowSalaryPenalty(__instance.GetSalarySatisfaction_Percentage(), GraduationWeek.LowSalaryApplies());
                if (penalty != 0f)
                    __instance.Graduation_Date = __instance.Graduation_Date.AddDays(penalty);
                return;
            }

            DateTime before = __state ?? __instance.Graduation_Date;
            __instance.Graduation_Date = before.AddDays(GraduationWeek.WeeklyDays(GraduationWeek.Read(__instance)));
        }
    }

    /// <summary>
    /// What the weekly graduation date change reads about an idol.
    /// </summary>
    public struct GraduationInputs
    {
        public float PhysicalStamina;
        public float MentalStamina;
        /// <summary>Relationship with the player, 0 to 1.</summary>
        public float Relationship;
        public int FlirtPoints;
        public bool DatingBanned;
        public int SalarySatisfaction;
        /// <summary>Normal or Unfair with manual salaries: only then does low salary move the date.</summary>
        public bool LowSalaryApplies;
        public data_girls._status Status;
        /// <summary>Days since her last contract, 0 while a longer contract is running, -1 if she never had one.</summary>
        public int DaysSinceContract;
        /// <summary>Contracts accepted in the last 60 days: 1 per one-off, 10 per longer contract.</summary>
        public int ContractPoints;
        public int Shows;
        /// <summary>Row in the latest released single: 0 center, 1 for 2nd-3rd, 2-4 further back, 5 or -1 not in it.</summary>
        public int SingleRow;
        public bool Pushed;
        public int Age;
    }

    /// <summary>
    /// The weekly graduation date change, in days (positive = later).
    /// </summary>
    public static class GraduationWeek
    {
        public const float WEEKLY_MAX = 5f;
        public const int CONTRACT_WINDOW_DAYS = 60;
        public const int CONTRACT_POINTS_ONE_OFF = 1;
        // A longer contract costs about 10 times the stamina of a one-off
        public const int CONTRACT_POINTS_LONG = 10;
        public const float CONTRACT_MAX = 2f;

        // The game keeps running contracts on its business component
        public static Func<List<business.active_proposal>> RunningContracts = () =>
        {
            Camera camera = Camera.main;
            if (camera == null)
                return null;
            mainScript main = camera.GetComponent<mainScript>();
            if (main == null || main.Data == null)
                return null;
            business business = main.Data.GetComponent<business>();
            return business == null ? null : business.ActiveProposals;
        };

        public static bool LowSalaryApplies()
        {
            if (staticVars.IsEasy())
                return false;
            policies.value salaryPolicy = policies.GetSelectedPolicyValue(policies._type.salary);
            return salaryPolicy != null && salaryPolicy.Value == policies._value.salary_manual;
        }

        // In Unfair and Normal with manual salaries only: the game's -3 / -1, increased 10x
        public static float LowSalaryPenalty(int satisfaction, bool applies)
        {
            if (!applies)
                return 0f;
            if (satisfaction < 20)
                return -GRAD_DAYS_PENALTY_SEVERE;
            if (satisfaction < 50)
                return -GRAD_DAYS_PENALTY;
            return 0f;
        }

        public static float WeeklyDays(GraduationInputs w)
        {
            float days = 0f;

            if (w.PhysicalStamina < 25f)
                days -= 1f;
            if (w.MentalStamina < 25f)
                days -= 3f;
            else if (w.MentalStamina < 50f)
                days -= 1f;

            // The game compares this 0-1 ratio with 40 and 85, so it always took the "below 40" branch
            if (w.Relationship < 0.40f)
                days -= 2f;
            else if (w.Relationship > 0.85f)
                days += 1f;

            days += Math.Min(Math.Max(w.FlirtPoints, 0), 4);
            if (w.DatingBanned)
                days -= 2f;

            if (w.SalarySatisfaction < 50)
                days += LowSalaryPenalty(w.SalarySatisfaction, w.LowSalaryApplies);
            else if (w.SalarySatisfaction > 150)
                days += 1.5f;
            else if (w.SalarySatisfaction > 100)
                days += 1f;
            else if (w.SalarySatisfaction > 75)
                days += 0.5f;

            if (w.Status == data_girls._status.injured)
                days -= 2f;
            else if (w.Status == data_girls._status.depressed)
                days -= 7f;
            else if (w.Status == data_girls._status.hiatus)
                days += 2f;

            if (w.DaysSinceContract > 360)
                days -= 6f;
            else if (w.DaysSinceContract > 180)
                days -= 4f;
            else if (w.DaysSinceContract > 120)
                days -= 3f;
            else if (w.DaysSinceContract > 90)
                days -= 2f;
            else if (w.DaysSinceContract > 60)
                days -= 1f;
            else if (w.DaysSinceContract >= 0)
                days += Math.Min(CONTRACT_MAX, 0.5f + w.ContractPoints / 4f);

            int shows = Math.Min(w.Shows, 3);
            if (shows > 0)
                days += 0.5f + shows * 0.5f;

            switch (w.SingleRow)
            {
                case 0:
                    days += 3.5f;
                    break;
                case 1:
                    days += 1.5f;
                    break;
                case 2:
                case 3:
                case 4:
                    days += 0.5f;
                    break;
            }

            if (w.Pushed)
                days += 1.5f;

            days = Math.Min(days, WEEKLY_MAX);

            // The game checks these in the wrong order, so only the first could ever apply
            if (w.Age > 30)
                days -= 7f;
            else if (w.Age > 25)
                days -= 3f;
            else if (w.Age > 20)
                days -= 1f;

            return days;
        }

        public static GraduationInputs Read(data_girls.girls girl)
        {
            int age = girl.GetAge();
            policies.value dating = policies.GetSelectedPolicyValue(policies._type.dating);
            singles._single single = singles.GetLatestReleasedSingle();
            ReadContracts(girl, out int daysSinceContract, out int contractPoints);

            return new GraduationInputs
            {
                PhysicalStamina = girl.getParam(data_girls._paramType.physicalStamina).val,
                MentalStamina = girl.getParam(data_girls._paramType.mentalStamina).val,
                Relationship = girl.GetRelationshipWithPlayer().Ratio,
                FlirtPoints = girl.DatingData.Success_Counter,
                DatingBanned = dating != null && dating.Value != policies._value.dating_allowed
                    && girl.DatingData.Partner_Status == data_girls.girls._dating_data._partner_status.free && age >= 18,
                SalarySatisfaction = girl.GetSalarySatisfaction_Percentage(),
                LowSalaryApplies = LowSalaryApplies(),
                Status = girl.status,
                DaysSinceContract = daysSinceContract,
                ContractPoints = contractPoints,
                Shows = Shows.GetShowsWithGirl(girl, count_entire_group: false).Count,
                SingleRow = single == null ? -1 : single.GetRowOfAGirl(girl),
                Pushed = girl.Is_Pushed(),
                Age = age,
            };
        }

        /// <summary>
        /// Days since her last accepted contract (the game's GetDaysSinceLastProposal returns the first),
        /// and her contract points over the last 60 days.
        /// </summary>
        public static void ReadContracts(data_girls.girls girl, out int daysSinceContract, out int contractPoints)
        {
            daysSinceContract = -1;
            contractPoints = 0;
            if (business.History != null)
            {
                foreach (business._history contract in business.History)
                {
                    if (contract == null || contract.Girl != girl)
                        continue;
                    int days = (staticVars.dateTime - contract.Date).Days;
                    if (daysSinceContract < 0 || days < daysSinceContract)
                        daysSinceContract = days;
                    if (days < CONTRACT_WINDOW_DAYS)
                        contractPoints += IsLongContract(contract.Type) ? CONTRACT_POINTS_LONG : CONTRACT_POINTS_ONE_OFF;
                }
            }

            List<business.active_proposal> running = null;
            try
            {
                running = RunningContracts();
            }
            catch (Exception)
            {
                // No business component outside a loaded game
            }
            if (running == null)
                return;
            foreach (business.active_proposal contract in running)
            {
                if (contract != null && contract.Girl == girl && contract.EndDate >= staticVars.dateTime)
                {
                    daysSinceContract = 0;
                    return;
                }
            }
        }

        public static bool IsLongContract(business._type type)
        {
            business._data data = business.GetData(type);
            return data != null && data.duration > 0;
        }
    }

    // In hard mode, idols at 10 fame will expect at least 10% of their earnings as salary
    // Never lower a larger expectation from vanilla or another mod
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetExpectedSalary_Total))]
    public class data_girls_girls_GetExpectedSalary_Total
    {
        public static void Postfix(ref long __result, data_girls.girls __instance)
        {
            if (__instance == null || !staticVars.IsHard() || __instance.GetFameLevel() != 10)
                return;

            float earning = __instance.GetAverageEarnings();
            if (float.IsNaN(earning) || float.IsInfinity(earning) || earning <= 0f)
                return;

            long floor = (long)Mathf.Round(earning * MAXFAME_SALARY_EARN_COEFF);
            if (floor > __result)
            {
                __result = floor;
            }
        }
    }


    // Default salary set to 20000
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GenerateGirl))]
    public class data_girls_GenerateGirl
    {
        public static void Postfix(ref data_girls.girls __result)
        {
            if (__result != null)
                __result.salary = DEF_SALARY;
        }
    }

}
