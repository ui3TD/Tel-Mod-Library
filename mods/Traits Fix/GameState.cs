using System;
using System.Collections.Generic;
using UnityEngine;

namespace TraitFix
{
    public partial class TraitsFix
    {
        public static Event_Manager GetEventManager() => GetGameComponent<Event_Manager>();

        public static business GetBusiness() => GetGameComponent<business>();

        // The game's managers are components of mainScript.Data
        private static T GetGameComponent<T>() where T : Component
        {
            Camera camera = Camera.main;
            if (camera == null)
                return null;
            mainScript main = camera.GetComponent<mainScript>();
            if (main == null || main.Data == null)
                return null;
            return main.Data.GetComponent<T>();
        }

        public static bool TryGetRelationshipGirls(Relationships._relationship relationship, out data_girls.girls girl0, out data_girls.girls girl1)
        {
            girl0 = null;
            girl1 = null;
            if (relationship?.Girls == null || relationship.Girls.Count < 2)
                return false;
            girl0 = relationship.Girls[0];
            girl1 = relationship.Girls[1];
            return girl0 != null && girl1 != null && girl0.DatingData != null && girl1.DatingData != null;
        }

        public static bool IsKnownToDateIdol(data_girls.girls girl)
        {
            return girl?.DatingData != null
                && girl.DatingData.Is_Partner_Status_Known
                && girl.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_idol;
        }

        /// <summary>
        /// Whether two idols are dating each other without the player knowing, and no leak of them is waiting.
        /// </summary>
        public static bool IsSecretCouple(Relationships._relationship relationship, out data_girls.girls girl0, out data_girls.girls girl1)
        {
            if (!TryGetRelationshipGirls(relationship, out girl0, out girl1) || !relationship.Dating)
                return false;

            if (relationship.IsRelationshipKnown())
                return false;

            // A leak marks both idols as dating an idol, so this stops the same couple leaking every week.
            // Knowing only one of them is dating (but not who) leaves the couple secret.
            if (IsKnownToDateIdol(girl0) && IsKnownToDateIdol(girl1))
                return false;
            return !IsInWaitingLeak(girl0) && !IsInWaitingLeak(girl1);
        }

        public static void MarkIdolRelationshipKnown(data_girls.girls girl0, data_girls.girls girl1)
        {
            if (girl0?.DatingData == null || girl1?.DatingData == null)
                return;
            girl0.DatingData.Is_Partner_Status_Known = true;
            girl1.DatingData.Is_Partner_Status_Known = true;
            girl0.DatingData.Partner_Status_Known_To_Player = data_girls.girls._dating_data._partner_status.taken_idol;
            girl1.DatingData.Partner_Status_Known_To_Player = data_girls.girls._dating_data._partner_status.taken_idol;
        }

        private static bool DidSingleMissNumberOne(singles._single single)
        {
            if (single?.ReleaseData == null)
                return false;

            // The single charts the month after its release.
            DateTime releaseDate = single.ReleaseData.ReleaseDate;
            if (releaseDate.Year == staticVars.dateTime.Year && releaseDate.Month == staticVars.dateTime.Month)
                return false;

            // The chart popup records the position, except for the last-ranked single or when the popup is skipped.
            // A single that can't be found on any chart counts as having missed #1.
            int chartPosition = single.ReleaseData.Chart_Position;
            if (chartPosition <= 0)
                chartPosition = ResolveChartPosition(single);
            return chartPosition != 1;
        }

        private static int ResolveChartPosition(singles._single single)
        {
            if (Rivals.Date_To_Month == null)
                return 0;

            // A month's chart lists the singles released the month before.
            DateTime chartMonth = single.ReleaseData.ReleaseDate.AddMonths(1);
            foreach (Rivals._date_to_month_id month in Rivals.Date_To_Month)
            {
                if (month == null || month.Date.Year != chartMonth.Year || month.Date.Month != chartMonth.Month)
                    continue;

                List<Rivals._group._single> chartSingles = Rivals.GetSingles(month.ID);
                if (chartSingles == null)
                    return 0;
                for (int i = 0; i < chartSingles.Count; i++)
                {
                    Rivals._group._single chartSingle = chartSingles[i];
                    if (chartSingle != null && chartSingle.Player && chartSingle.SingleID == single.id)
                        return i + 1;
                }
                return 0;
            }
            return 0;
        }

        private static bool IsEventUpcoming()
        {
            if (SEvent_Tour.Tours != null)
            {
                foreach (SEvent_Tour.tour tour in SEvent_Tour.Tours)
                    if (tour != null && tour.Status != SEvent_Tour.tour._status.finished) return true;
            }
            if (SEvent_SSK.Elections != null)
            {
                foreach (SEvent_SSK._SSK election in SEvent_SSK.Elections)
                    if (election != null && election.Status != SEvent_Tour.tour._status.finished) return true;
            }
            if (SEvent_Concerts.Concerts != null)
            {
                foreach (SEvent_Concerts._concert concert in SEvent_Concerts.Concerts)
                    if (concert != null && concert.Status != SEvent_Tour.tour._status.finished) return true;
            }
            return false;
        }

        private static singles._single GetRecentSingle(singles._single groupSingle, singles._single mainSingle)
        {
            if (groupSingle?.ReleaseData == null) return mainSingle?.ReleaseData != null ? mainSingle : null;
            if (mainSingle?.ReleaseData == null) return groupSingle;
            if (groupSingle.ReleaseData.ReleaseDate > mainSingle.ReleaseData.ReleaseDate) return groupSingle;
            if (groupSingle.ReleaseData.ReleaseDate < mainSingle.ReleaseData.ReleaseDate) return mainSingle;
            return groupSingle.ReleaseData.Sales > mainSingle.ReleaseData.Sales ? groupSingle : mainSingle;
        }
    }
}
