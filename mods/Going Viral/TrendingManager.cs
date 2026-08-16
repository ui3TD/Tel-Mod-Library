using HarmonyLib;
using System;
using UnityEngine;

namespace GoingViral
{
    public class TrendingManager
    {
        public const long MAX_TREND_DAYS = 90;
        public const long MIN_TREND_DAYS = -90;

        public static long trending = 0;
        public static long adFans = 0;
        public static long dramaFans = 0;
        public static long tvFans = 0;
        public static long radioFans = 0;
        public static long netFans = 0;
        public static long cafeFans = 0;

        public enum TrendingStatus
        {
            none,
            trending,
            crisis
        }

        public static TrendingStatus IsTrending()
        {
            if (trending > 0) return TrendingStatus.trending;
            if (trending < 0) return TrendingStatus.crisis;
            return TrendingStatus.none;
        }

        public static void SetTrending(long val)
        {
            if (IsTrending() != TrendingStatus.none || val == 0)
                return;

            long min = Harmony.HasAnyPatches("com.tel.fanattrition") ? MIN_TREND_DAYS : 0;
            trending = Math.Max(min, Math.Min(MAX_TREND_DAYS, val));
            if (trending == 0)
                return;

            Groups._group group = Groups.GetMainGroup();
            string groupTitle = group != null ? group.Title : "";
            if (trending > 0)
            {
                NotificationManager.AddNotification(
                    Language.Insert("NOTIF__TRENDING", new string[] { groupTitle }) + "\n" +
                    Language.Insert("NOTIF__TRENDING_DAYS", new string[] { trending.ToString() }),
                    mainScript.green32,
                    NotificationManager._notification._type.other);
            }
            else
            {
                NotificationManager.AddNotification(
                    Language.Insert("NOTIF__CRISIS", new string[] { groupTitle }) + "\n" +
                    Language.Insert("NOTIF__CRISIS_DAYS", new string[] { Math.Abs(trending).ToString() }),
                    mainScript.red32,
                    NotificationManager._notification._type.other);
            }
        }

        // Positive trends multiply gains; negative trends multiply churn by the same magnitude.
        // Keep the original positive curve (day 1 ~= 2.1x, day 90 = 11x) without the old sign flip.
        public static float GetTrendingCoeff()
        {
            if (trending == 0)
                return 1f;
            return (float)Math.Round(2d + Math.Abs((double)trending) / 10d, 1);
        }

        public static void UpdateFanCount()
        {
            adFans = dramaFans = tvFans = radioFans = netFans = cafeFans = 0;

            try
            {
                mainScript main = Camera.main != null ? Camera.main.GetComponent<mainScript>() : null;
                business businessData = main != null && main.Data != null ? main.Data.GetComponent<business>() : null;
                if (businessData != null && businessData.ActiveProposals != null)
                {
                    foreach (business.active_proposal proposal in businessData.ActiveProposals)
                    {
                        if (proposal == null || proposal.Fans_per_week <= 0)
                            continue;
                        if (proposal.Type == business._type.ad)
                            adFans = SafeAdd(adFans, proposal.Fans_per_week);
                        else if (proposal.Type == business._type.tv_drama)
                            dramaFans = SafeAdd(dramaFans, proposal.Fans_per_week);
                    }
                }
            }
            catch { }

            if (Shows.shows != null)
            {
                foreach (Shows._show show in Shows.shows)
                {
                    if (show == null || show.medium == null || show.fans == null || show.fans.Count == 0)
                        continue;
                    if (show.status == Shows._show._status.normal || show.status == Shows._show._status.working || show.status == Shows._show._status.canceled)
                        continue;

                    long latest = show.fans[show.fans.Count - 1];
                    if (show.medium.media_type == Shows._param._media_type.tv)
                        tvFans = SafeAdd(tvFans, latest);
                    else if (show.medium.media_type == Shows._param._media_type.radio)
                        radioFans = SafeAdd(radioFans, latest);
                    else if (show.medium.media_type == Shows._param._media_type.internet)
                        netFans = SafeAdd(netFans, latest);
                }
            }

            if (Cafes.Cafes_ != null)
            {
                foreach (Cafes._cafe cafe in Cafes.Cafes_)
                {
                    if (cafe == null || cafe.Stats == null)
                        continue;
                    int start = Math.Max(0, cafe.Stats.Count - 7);
                    for (int i = start; i < cafe.Stats.Count; i++)
                    {
                        if (cafe.Stats[i] != null)
                            cafeFans = SafeAdd(cafeFans, cafe.Stats[i].New_Fans);
                    }
                }
            }
        }

        public static float GetTrendingChance(float scandalPoints)
        {
            float p = scandalPoints >= 10f ? 50f : 5f;
            return Mathf.Clamp(p, 0f, 100f);
        }

        public static long GetTrendingMagnitude(float scandalPoints)
        {
            long magnitude = -UnityEngine.Random.Range(2, 8) * 7L;
            if (scandalPoints > 1f)
                magnitude = Mathf.RoundToInt(magnitude * 1.5f);
            return Math.Max(MIN_TREND_DAYS, magnitude);
        }

        public static float GetTrendingChance(Shows._show show)
        {
            if (show == null || show.genre == null || show.medium == null || show.medium.media_type != Shows._param._media_type.tv)
                return 0f;

            // Normalize cast fame from vanilla's 0-10 fame scale to 0-1.
            float castCoeff = 0f;
            if (show.castType == Shows._show._castType.entireGroup)
            {
                castCoeff = Mathf.Clamp01(resources.GetFameLevel() / 10f);
            }
            else if (show.girls != null)
            {
                float fameSum = 0f;
                int count = 0;
                foreach (data_girls.girls girl in show.girls)
                {
                    if (girl == null)
                        continue;

                    fameSum += girl.GetFameLevel();
                    count++;
                }

                if (count > 0)
                    castCoeff = Mathf.Clamp01((fameSum / count) / 10f);
            }

            // Normalize genre level from the vanilla 0-10 level scale to 0-1.
            float genreCoeff = Mathf.Clamp01(show.genre.GetLevel() / 10f);

            // Combine the applicable quality factors instead of multiplying them.
            // This mirrors Going Viral's single-trend design: build one normalized
            // quality score first, then apply the maximum trend chance.
            float qualityTotal = castCoeff + genreCoeff;
            int qualityFactors = 2;

            // Only count MC fame when the show actually has an MC. A show without
            // an MC therefore isn't automatically penalized by a zero-valued factor.
            if (show.mc != null)
            {
                float mcCoeff = Mathf.Clamp01(show.mc.fame / 10f);
                qualityTotal += mcCoeff;
                qualityFactors++;
            }

            float qualityCoeff = qualityTotal / qualityFactors;

            DateTime? lastShowDate = null;
            if (Shows.shows != null)
            {
                foreach (Shows._show other in Shows.shows)
                {
                    if (other == null || other == show || other.medium == null || other.genre == null)
                        continue;
                    if (other.LaunchDate == default(DateTime) || other.medium.media_type != Shows._param._media_type.tv || other.genre.id != show.genre.id)
                        continue;
                    if (!lastShowDate.HasValue || other.LaunchDate > lastShowDate.Value)
                        lastShowDate = other.LaunchDate;
                }
            }

            // No previous TV show of this genre means maximum freshness.
            float daysSinceCoeff = 1f;
            if (lastShowDate.HasValue)
            {
                int days = Math.Max(0, (staticVars.dateTime - lastShowDate.Value).Days);
                daysSinceCoeff = Math.Min(365, days) / 365f;
            }

            // Both coefficients are bounded to 0-1, so 15f is now a true maximum
            // rather than a base multiplier that can balloon above 15%.
            return Mathf.Clamp(15f * daysSinceCoeff * qualityCoeff, 0f, 15f);
        }

        public static long GetTrendingMagnitude(Shows._show show)
        {
            return UnityEngine.Random.Range(31, 61);
        }

        public static float GetTrendingChance(singles._param marketing, Single_Marketing_Roll._result marketingResult, Groups._group group = null, float trendCoeff = 0f)
        {
            if (marketing == null)
                return 0f;

            float p = 0f;
            float saturationCoeff = group != null ? singles.GetSaturationCoeff(group) : 1f;
            float modifiedTrendCoeff = (trendCoeff / 0.18f + 1f) / 2f;

            if (marketingResult == Single_Marketing_Roll._result.success_crit)
            {
                switch (marketing.Special_Type)
                {
                    case singles._param._special_type.ad_campaign: p = 100f * modifiedTrendCoeff; break;
                    case singles._param._special_type.viral_campaign: p = 70f * modifiedTrendCoeff; break;
                    case singles._param._special_type.fake_scandal: p = 40f * modifiedTrendCoeff + 60f; break;
                }
                p *= saturationCoeff;
            }
            else if (marketingResult == Single_Marketing_Roll._result.fail_crit)
            {
                switch (marketing.Special_Type)
                {
                    case singles._param._special_type.ad_campaign: p = 50f; break;
                    case singles._param._special_type.viral_campaign: p = 33f; break;
                }
            }
            return Mathf.Clamp(p, 0f, 100f);
        }

        public static long GetTrendingMagnitude(singles._param marketing, Single_Marketing_Roll._result marketingResult)
        {
            if (marketing == null)
                return 0;

            if (marketingResult == Single_Marketing_Roll._result.success_crit)
            {
                switch (marketing.Special_Type)
                {
                    case singles._param._special_type.ad_campaign: return UnityEngine.Random.Range(31, 61);
                    case singles._param._special_type.viral_campaign: return UnityEngine.Random.Range(31, 91);
                    case singles._param._special_type.fake_scandal: return UnityEngine.Random.Range(31, 91);
                    case singles._param._special_type.lewd_pv:
                    case singles._param._special_type.edgy_pv:
                    case singles._param._special_type.artsy_pv: return UnityEngine.Random.Range(31, 61);
                }
            }
            else if (marketingResult == Single_Marketing_Roll._result.fail_crit)
            {
                switch (marketing.Special_Type)
                {
                    case singles._param._special_type.ad_campaign: return -UnityEngine.Random.Range(15, 46);
                    case singles._param._special_type.viral_campaign: return -UnityEngine.Random.Range(31, 61);
                    case singles._param._special_type.lewd_pv:
                    case singles._param._special_type.edgy_pv:
                    case singles._param._special_type.artsy_pv: return -UnityEngine.Random.Range(15, 46);
                }
            }
            return 0;
        }

        private const int appealMultiplier = 3;

        public static float GetFanChurn(float appeal, float opinion, resources.fanType type)
        {
            float safeAppeal = Math.Max(0f, appeal);
            float safeOpinion = Mathf.Clamp01(opinion);
            float aversion = 1f / (safeAppeal * (1f + safeOpinion) + 0.001f);
            return aversion * (type == resources.fanType.casual ? appealMultiplier : 1f);
        }

        public static float GetFanAcquisition(data_girls.girls girl, float appeal, resources.fanType type)
        {
            float weight = Math.Max(0f, appeal);
            if (type == resources.fanType.casual)
                weight *= appealMultiplier;
            return weight;
        }

        public static int ScaleInt(int value, float coeff)
        {
            long scaled = ScaleLong(value, coeff);
            if (scaled > int.MaxValue) return int.MaxValue;
            if (scaled < int.MinValue) return int.MinValue;
            return (int)scaled;
        }

        public static long ScaleLong(long value, float coeff)
        {
            if (float.IsNaN(coeff) || float.IsInfinity(coeff))
                return value;
            double scaled = Math.Round(value * (double)coeff, MidpointRounding.AwayFromZero);
            if (scaled >= long.MaxValue) return long.MaxValue;
            if (scaled <= long.MinValue) return long.MinValue;
            return (long)scaled;
        }

        public static long SafeAdd(long a, long b)
        {
            if (b > 0 && a > long.MaxValue - b) return long.MaxValue;
            if (b < 0 && a < long.MinValue - b) return long.MinValue;
            return a + b;
        }
    }
}
