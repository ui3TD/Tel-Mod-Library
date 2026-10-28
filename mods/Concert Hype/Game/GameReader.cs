using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConcertHype
{
    /// <summary>
    /// A show set up from a game concert: the model's setup, and which idol and setlist item each index stands for.
    /// </summary>
    public sealed class GameShow
    {
        public ShowSetup Setup;
        public List<data_girls.girls> Girls = new List<data_girls.girls>();
        public List<SEvent_Concerts._concert._song> Items = new List<SEvent_Concerts._concert._song>();
    }

    /// <summary>
    /// Reads a concert into the model through the game's own methods, so other mods' changes to stats, skill, fans
    /// and options carry over. Nothing in the game is changed.
    /// </summary>
    public static class GameReader
    {
        public const int SenbatsuPositions = 15;

        private static readonly data_girls._paramType[] Stats =
        {
            data_girls._paramType.cute, data_girls._paramType.cool, data_girls._paramType.sexy, data_girls._paramType.pretty,
            data_girls._paramType.vocal, data_girls._paramType.dance, data_girls._paramType.funny, data_girls._paramType.smart,
        };

        private static readonly resources.fanType[] FanOrder =
        {
            resources.fanType.male, resources.fanType.female, resources.fanType.casual, resources.fanType.hardcore,
            resources.fanType.teen, resources.fanType.youngAdult, resources.fanType.adult,
        };

        public static GameShow Read(SEvent_Concerts._concert concert, Config config)
        {
            GameShow show = new GameShow();
            show.Items = concert.SetListItems.OfType<SEvent_Concerts._concert._song>().ToList();

            foreach (SEvent_Concerts._concert._song item in show.Items)
            {
                foreach (data_girls.girls girl in Senbatsu(item.Single))
                    AddToCast(show, girl);
            }
            foreach (SEvent_Concerts._concert._song item in show.Items)
                AddToCast(show, item.Center);
            // Then the rest of the agency, who can be brought in from the bench
            int setlistCast = show.Girls.Count;
            foreach (data_girls.girls girl in data_girls.girl ?? new List<data_girls.girls>())
                AddToCast(show, girl);

            ShowSetup setup = new ShowSetup
            {
                Members = show.Girls.Select((girl, m) => ReadMember(girl, m < setlistCast)).ToList(),
                Songs = show.Items.Select(item => ReadSong(show, item, config)).ToList(),
                CapacityFilled = Engine.Clamp(concert.ProjectedValues.Actual_Attendance, 0, 1),
                Production = (concert.GetParam(SEvent_Concerts._concert._progressable._type.production)?.val ?? 0) / 100.0,
                HypeFloor = concert.SSK != null ? SEvent_SSK.GetConcertMinHype(concert.SSK) : 0,
                StartingHype = config.StartingHype,
            };
            setup.Skill = show.Girls.Select(girl => show.Items.Select(item => Skill(item.Single, girl)).ToArray()).ToArray();
            show.Setup = setup;
            return show;
        }

        private static IEnumerable<data_girls.girls> Senbatsu(singles._single single)
        {
            if (single?.girls == null)
                yield break;
            for (int i = 0; i < Math.Min(SenbatsuPositions, single.girls.Count); i++)
            {
                if (!(single.girls[i] is null))
                    yield return single.girls[i];
            }
        }

        private static void AddToCast(GameShow show, data_girls.girls girl)
        {
            if (!(girl is null) && girl.IsActive() && !show.Girls.Any(g => ReferenceEquals(g, girl)))
                show.Girls.Add(girl);
        }

        private static int CastIndex(GameShow show, data_girls.girls girl) =>
            girl is null ? -1 : show.Girls.FindIndex(g => ReferenceEquals(g, girl));

        private static Member ReadMember(data_girls.girls girl, bool inSetlist) => new Member
        {
            Name = girl.girlName(),
            Fans = girl.GetFans_Total(),
            Talk = Talk(girl),
            Appeal = FanOrder.Select(fan => Stats.Sum(stat => (double)girl.GetAppealOfStat(stat, fan))).ToArray(),
            InSetlist = inSetlist,
        };

        /// <summary>
        /// The game's talk break skill for her as the only host, 0 to 1: a talk break item with her, which runs every
        /// mod's changes to it. Nothing is changed in the concert.
        /// </summary>
        private static double Talk(data_girls.girls girl)
        {
            SEvent_Concerts._concert._mc talk = new SEvent_Concerts._concert._mc();
            talk.SetGirl(girl, 0);
            return talk.GetSkillValue() / 100.0;
        }

        private static Song ReadSong(GameShow show, SEvent_Concerts._concert._song item, Config config)
        {
            singles._single single = item.Single;
            Song song = new Song
            {
                Title = single?.title ?? "",
                Genre = single?.genre?.title ?? "",
                Appeal = FanOrder.Select(fan => OptionAppeal(single?.genre, fan, config) + OptionAppeal(single?.lyrics, fan, config)
                    + OptionAppeal(single?.choreography, fan, config)).ToArray(),
            };

            List<data_girls.girls> girls = single?.girls ?? new List<data_girls.girls>();
            for (int position = 0; position < Math.Min(SenbatsuPositions, girls.Count); position++)
            {
                if (!(girls[position] is null))
                    song.Slots.Add(new Slot { Position = position, Row = Slot.RowOf(position), Original = CastIndex(show, girls[position]) });
            }
            // A single without a senbatsu still has a center to fill
            if (song.Slots.Count == 0)
                song.Slots.Add(new Slot { Position = 0, Row = 0, Original = -1 });

            int center = CastIndex(show, item.Center);
            if (center >= 0 && !girls.Any(g => ReferenceEquals(g, item.Center)))
                song.PresetSubstitute = center;
            return song;
        }

        private static double OptionAppeal(singles._param option, resources.fanType fan, Config config)
        {
            if (option == null)
                return 0;
            try
            {
                return option.GetAppealVal(fan, config.OptionLevel, true);
            }
            catch (Exception)
            {
                Log.WarnOnce("appeal " + option.title, $"single option '{option.title}' has no {fan} appeal; counting it as 0");
                return 0;
            }
        }

        /// <summary>
        /// The game's concert skill for her in this song: a song item with her as the center, which runs every mod's
        /// changes to it. Nothing is changed in the concert.
        /// </summary>
        private static double Skill(singles._single single, data_girls.girls girl) =>
            new SEvent_Concerts._concert._song { Single = single, Center = girl }.GetSkillValue();
    }
}
