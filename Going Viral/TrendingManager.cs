using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace GoingViral
{
    public class TrendingManager
    {

        public static long trending = 0;

        public static long adFans = 0;
        public static long dramaFans = 0;
        public static long tvFans = 0;
        public static long radioFans = 0;
        public static long netFans = 0;
        public static long cafeFans = 0;


        public static TrendingStatus IsTrending()
        {
            if (trending > 0)
            {
                return TrendingStatus.trending;
            }
            else if (trending < 0)
            {
                return TrendingStatus.crisis;
            }
            return TrendingStatus.none;
        }

        public static void SetTrending(long val)
        {
            if (IsTrending() != TrendingStatus.none || val == 0)
            {
                return;
            }

            int maxTrending = 91;
            int minTrending = -91;
            if (!Harmony.HasAnyPatches("com.tel.fanattrition"))
            {
                minTrending = 0;
            }
            trending = Math.Min(Math.Max(val, minTrending), maxTrending);
            if (trending > 0)
            {
                NotificationManager.AddNotification(
                    Language.Insert("NOTIF__TRENDING", new string[] { Groups.GetMainGroup().Title }) + "\n" + Language.Insert("NOTIF__TRENDING_DAYS", new string[] { val.ToString() }),
                    mainScript.green32,
                    NotificationManager._notification._type.other);
            }
            else if (trending < 0)
            {
                if (Harmony.HasAnyPatches("com.tel.fanattrition"))
                {
                    NotificationManager.AddNotification(
                    Language.Insert("NOTIF__CRISIS", new string[] { Groups.GetMainGroup().Title }) + "\n" + Language.Insert("NOTIF__CRISIS_DAYS", new string[] { (-val).ToString() }),
                    mainScript.red32,
                    NotificationManager._notification._type.other);
                }
            }
        }

        public static float GetTrendingCoeff()
        {
            float coeff = 1;

            if (trending != 0)
            {
                coeff += (float)Math.Round((double)trending / 10 + 1, 1);
            }
            if (trending < 0)
            {
                coeff *= -1;
            }

            return coeff;
        }

        public static void UpdateFanCount()
        {

            adFans = 0;
            dramaFans = 0;
            tvFans = 0;
            radioFans = 0;
            netFans = 0;
            cafeFans = 0;
            foreach (business.active_proposal active_proposal in Camera.main.GetComponent<mainScript>().Data.GetComponent<business>().ActiveProposals)
            {
                if (active_proposal.Fans_per_week > 0)
                {
                    if (active_proposal.Type == business._type.ad)
                    {
                        adFans += active_proposal.Fans_per_week;
                    }
                    else if (active_proposal.Type == business._type.tv_drama)
                    {
                        dramaFans += active_proposal.Fans_per_week;
                    }
                }
            }

            foreach (Shows._show show in Shows.shows)
            {
                if (show.status != Shows._show._status.normal && show.status != Shows._show._status.working && show.status != Shows._show._status.canceled)
                {
                    if (show.medium.media_type == Shows._param._media_type.tv)
                    {
                        tvFans += show.fans[show.fans.Count - 1];
                    }
                    else if (show.medium.media_type == Shows._param._media_type.radio)
                    {
                        radioFans += show.fans[show.fans.Count - 1];
                    }
                    else if (show.medium.media_type == Shows._param._media_type.internet)
                    {
                        netFans += show.fans[show.fans.Count - 1];
                    }
                }
            }

            foreach (Cafes._cafe cafe in Cafes.Cafes_)
            {
                int dayCount = 0;
                for (int i = cafe.Stats.Count - 1; i >= 0; i--)
                {
                    cafeFans += cafe.Stats[i].New_Fans;
                    dayCount++;
                    if (dayCount >= 7)
                    {
                        break;
                    }
                }
            }
        }

        public enum TrendingStatus
        {
            none,
            trending,
            crisis
        }

        public static float GetTrendingChance(float scandalPoints)
        {
            float p = 5;
            if (scandalPoints >= 10)
            {
                p *= 10;
            }
            return p;
        }
        public static long GetTrendingMagnitude(float scandalPoints)
        {
            long magnitude = -UnityEngine.Random.Range(2, 8) * 7;
            if (scandalPoints > 1)
            {
                magnitude = Mathf.RoundToInt(magnitude * 1.5f);
            }
            return magnitude;
        }


        public static float GetTrendingChance(Shows._show show)
        {
            float fameCoeff = 0;
            float fameSum = 0f;
            int count = 0;
            if (show.fame.Count > 0)
            {
                fameCoeff = show.fame[0] / 20;
            }
            if (show.castType == Shows._show._castType.entireGroup)
            {
                fameSum += (float)resources.GetFameLevel();
                count++;
            }
            else
            {
                foreach (data_girls.girls girls in show.girls)
                {
                    if (girls != null)
                    {
                        fameSum += girls.GetFameLevel();
                        count++;
                    }
                }
            }
            if (count != 0)
            {
                fameCoeff += fameSum / count / 20;
            }
            if (show.mc != null)
            {
                fameCoeff += show.mc.fame / 20;
            }
            Shows._param genre = show.genre;

            float levelCoeff = genre.GetLevel() / 5 + 0.5f;

            DateTime? lastShowDate = null;

            // Get last show
            foreach (Shows._show _show in Shows.shows)
            {
                if (_show != show && _show.LaunchDate != null && _show.medium.media_type == Shows._param._media_type.tv && _show.genre.id == genre.id)
                {
                    if (lastShowDate == null || _show.LaunchDate > (lastShowDate ?? staticVars.dateTime))
                    {
                        lastShowDate = _show.LaunchDate;
                    }
                }
            }

            float daysSinceCoeff = 1;
            if (lastShowDate != null)
            {
                daysSinceCoeff = (staticVars.dateTime - (lastShowDate ?? staticVars.dateTime)).Days;
                daysSinceCoeff = Math.Min(365, daysSinceCoeff) / 365;
            }
            return 15f * daysSinceCoeff * fameCoeff * levelCoeff;
        }
        public static long GetTrendingMagnitude(Shows._show show)
        {
            return UnityEngine.Random.Range(31, 61);
        }

        public static float GetTrendingChance(singles._param marketing, Single_Marketing_Roll._result marketingResult, Groups._group group = null, float trendCoeff = 0)
        {
            float p = 0;
            if (marketing == null)
            {
                return p;
            }

            float saturationCoeff = 1;
            if(group != null)
            {
                saturationCoeff = singles.GetSaturationCoeff(group);
            }
            float modifiedTrendCoeff = (trendCoeff / 0.18f + 1) / 2;
            if (marketingResult == Single_Marketing_Roll._result.success_crit)
            {
                switch (marketing.Special_Type)
                {
                    case singles._param._special_type.ad_campaign:
                        p = 100 * modifiedTrendCoeff;
                        break;
                    case singles._param._special_type.viral_campaign:
                        p = 70 * modifiedTrendCoeff;
                        break;
                    case singles._param._special_type.fake_scandal:
                        p = 40 * modifiedTrendCoeff + 60;
                        break;
                    case singles._param._special_type.lewd_pv:
                    case singles._param._special_type.edgy_pv:
                    case singles._param._special_type.artsy_pv:
                        //p = 50 * modifiedTrendCoeff;
                        break;
                    default:
                        break;
                }
                p *= saturationCoeff;
            }
            else if (marketingResult == Single_Marketing_Roll._result.fail_crit)
            {
                switch (marketing.Special_Type)
                {
                    case singles._param._special_type.ad_campaign:
                        p = 50;
                        break;
                    case singles._param._special_type.viral_campaign:
                        p = 33;
                        break;
                    case singles._param._special_type.fake_scandal:
                    case singles._param._special_type.lewd_pv:
                    case singles._param._special_type.edgy_pv:
                    case singles._param._special_type.artsy_pv:
                        //p = 50;
                        break;
                    default:
                        break;
                }
            }
            return p;
        }
        public static long GetTrendingMagnitude(singles._param marketing, Single_Marketing_Roll._result marketingResult)
        {
            long magnitude = 0;
            if (marketing == null)
            {
                return magnitude;
            }

            if (marketingResult == Single_Marketing_Roll._result.success_crit)
            {
                switch (marketing.Special_Type)
                {
                    case singles._param._special_type.ad_campaign:
                        magnitude = UnityEngine.Random.Range(31, 61);
                        break;
                    case singles._param._special_type.viral_campaign:
                        magnitude = UnityEngine.Random.Range(31, 91);
                        break;
                    case singles._param._special_type.lewd_pv:
                    case singles._param._special_type.edgy_pv:
                    case singles._param._special_type.artsy_pv:
                        magnitude = UnityEngine.Random.Range(31, 61);
                        break;
                    case singles._param._special_type.fake_scandal:
                        magnitude = UnityEngine.Random.Range(31, 91);
                        break;
                    default:
                        break;
                }
            }
            else if (marketingResult == Single_Marketing_Roll._result.fail_crit)
            {
                switch (marketing.Special_Type)
                {
                    case singles._param._special_type.ad_campaign:
                        magnitude = -UnityEngine.Random.Range(15, 46);
                        break;
                    case singles._param._special_type.viral_campaign:
                        magnitude = -UnityEngine.Random.Range(31, 61);
                        break;
                    case singles._param._special_type.lewd_pv:
                    case singles._param._special_type.edgy_pv:
                    case singles._param._special_type.artsy_pv:
                        magnitude = -UnityEngine.Random.Range(15, 46);
                        break;
                    case singles._param._special_type.fake_scandal:
                    default:
                        break;
                }
            }
            return magnitude;
        }


        static int appealMultiplier = 3;

        public static float GetFanChurn(float appeal, float opinion, resources.fanType type)
        {
            float aversion = 1 / (appeal + opinion * appeal + 0.001f);
            float x = 1;
            if (type == resources.fanType.casual)
            {
                x = appealMultiplier * -Math.Min(-1, GetTrendingCoeff() * 2);
            }
            return aversion * x;
        }
        public static float GetFanAcquisition(data_girls.girls girl, float appeal, resources.fanType type)
        {
            float x = 1;
            float girlFame = girl.GetFameLevel();
            float groupFame = resources.GetFameLevel();
            float avgFame = (girlFame * 2 + groupFame) / 3 / 10;
            LinearFunction._function getCoeffFromFame = new();
            getCoeffFromFame.Init(3f, -appealMultiplier, 7f, appealMultiplier);
            float fameCoeff = Math.Min(appealMultiplier, Math.Max(-appealMultiplier, getCoeffFromFame.GetY(avgFame)));

            if (type == resources.fanType.casual)
            {
                x = fameCoeff * Math.Max(1, GetTrendingCoeff() * 2);
            }
            return appeal * x;
        }
    }

}
