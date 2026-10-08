using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;
using _media_type = Shows._param._media_type;

// Every test shares the game's static state (idols, shows, stats, countries, text).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PromotionTierTweaks.Tests
{
    /// <summary>
    /// Builds the game state the promotion tiers read.
    /// </summary>
    public static class TestGame
    {
        // Must differ from "tests.PromotionTierTweaks", which PatchTargetAssert unpatches.
        private static readonly Lazy<bool> Patched = new(() =>
        {
            Harmony harmony = new("tests.PromotionTierTweaks.behaviour");
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(Activities_GetMaxLevel_Promotion).Assembly, "PromotionTierTweaks"))
                harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        });

        /// <summary>
        /// Resets the game state to a new game and loads the mod's text into the game's language table.
        /// With patched set, the mod is applied to the game's methods (once per test run).
        /// </summary>
        public static void Reset(bool patched = false)
        {
            if (patched)
                _ = Patched.Value;

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            data_girls.girl = new List<data_girls.girls>();
            singles.Singles = new List<singles._single>();
            Shows.shows = new List<Shows._show>();
            SEvent_Tour.Countries = new List<SEvent_Tour.country>();
            Stats.data.business.photoshoot_counter = 0;
            Stats.data.business.ad_top_payment = 0;
            Stats.data.business.tv_drama_top_fame = 0;
            Stats.data.business.variety_top_fame = 0;

            SimpleJSON.JSONNode constants = LoadJson("JSON/Constants/constants.json");
            for (int i = 0; i < constants.Count; i++)
                Language.Data[constants[i]["id"]] = constants[i]["text"];
            // The game's own text the mod uses, as in its English constants.json
            Language.Data["ACTIVITIES__MAGAZINES"] = "Appear in magazines";
            Language.Data["ACTIVITIES__INTERNETPEAK"] = "Internet show peak audience";
            Language.Data["ACTIVITIES__IDOLSLEVEL"] = "Idols with level";
            Language.Data["ACTIVITIES__INTERNETSHOW"] = "Launch an internet show";
            Language.Data["ACTIVITIES__RADIOSHOW"] = "Launch a radio show";
            Language.Data["ACTIVITIES__TVSHOW"] = "Launch a TV show";
            Language.Data["FAME"] = "Fame";
        }

        /// <summary>
        /// A show of this medium that has aired this many episodes.
        /// </summary>
        public static Shows._show Show(_media_type medium, int episodes, long peakAudience = 0, Shows._show._status status = Shows._show._status.released)
        {
            Shows._show show = new()
            {
                status = status,
                episodeCount = episodes,
                peakAudience = peakAudience,
                medium = new Shows._param { media_type = medium },
            };
            Shows.shows.Add(show);
            return show;
        }

        /// <summary>
        /// An idol at this fame level, registered with the game.
        /// </summary>
        public static data_girls.girls Idol(int fameLevel = 1, data_girls._status status = data_girls._status.normal, bool maxedStats = false)
        {
            data_girls.girls girl = new() { status = status };
            girl.parameters.Add(new data_girls.girls.param { type = data_girls._paramType.famePoints, val = resources.FameLevelToPoints(fameLevel) });
            if (maxedStats)
            {
                for (data_girls._paramType stat = data_girls._paramType.cute; stat <= data_girls._paramType.smart; stat++)
                    girl.parameters.Add(new data_girls.girls.param { type = stat, val = 99f });
            }
            data_girls.girl.Add(girl);
            return girl;
        }

        /// <summary>
        /// Meets every vanilla requirement for every promotion tier: the game alone would allow level 10.
        /// The mod's tougher requirements are left unmet.
        /// </summary>
        public static void MeetVanillaRequirements()
        {
            Idol(5, maxedStats: true);
            Idol(5);
            Idol(5);
            singles.Singles.Add(new singles._single { status = singles._single._status.released });
            Stats.data.business.photoshoot_counter = GamePromo("lvl6_mags");
            Stats.data.business.ad_top_payment = GamePromo("lvl5_ad_contract_revenue");
            Stats.data.business.tv_drama_top_fame = GamePromo("lvl7_drama_fame_points");
            Stats.data.business.variety_top_fame = GamePromo("lvl7_variety_fame_points");
            Show(_media_type.internet, 1, GamePromo("lvl4_internet_audience"));
            Show(_media_type.radio, 1);
            Show(_media_type.tv, 1);
            SEvent_Tour.Countries.Add(new SEvent_Tour.country { ConcertCount = 1 });
        }

        /// <summary>
        /// Meets the mod's requirements, which the game's own then also are.
        /// </summary>
        public static void MeetModRequirements()
        {
            Show(_media_type.internet, Utility.lvl3Eps, Utility.lvl4audience);
            Show(_media_type.radio, Utility.lvl4Eps);
            Show(_media_type.tv, Utility.lvl6Eps);
            for (int i = 0; i < Utility.lvl9IdolCount; i++)
                Idol(Utility.lvl9Fame);
        }

        /// <summary>
        /// One of the game's promotion requirements (Activities.promo, private).
        /// </summary>
        public static int GamePromo(string field) =>
            (int)AccessTools.Field(AccessTools.Inner(typeof(Activities), "promo"), field).GetValue(null);

        /// <summary>
        /// The highest promotion level the game allows from this level, through the (patched) game method.
        /// </summary>
        public static int MaxLevel(int currentLevel)
        {
            Activities activities = (Activities)FormatterServices.GetUninitializedObject(typeof(Activities));
            Activities._activity act = new() { type = Activity._type.promotion, lvl = currentLevel };
            try
            {
                return (int)AccessTools.Method(typeof(Activities), "GetMaxLevel_Promotion").Invoke(activities, new object[] { act });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModFile(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Promotion Tier Tweaks", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));

        /// <summary>
        /// Parses one of the mod's JSON files the way the game does.
        /// </summary>
        public static SimpleJSON.JSONNode LoadJson(string relativePath) =>
            mainScript.ProcessInboundData(File.ReadAllText(ModAsset(relativePath)));
    }
}
