using System;
using System.Collections.Generic;
using System.Linq;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// The game's idols, groups, singles, concerts and elections, built the way the game holds them, in the
    /// game's own static lists.
    /// </summary>
    public static class TestGame
    {
        public static readonly DateTime Today = new(2024, 3, 24, 10, 0, 0);

        public static void Clear()
        {
            data_girls.girl.Clear();
            Groups.Groups_.Clear();
            singles.Singles.Clear();
            SEvent_Concerts.Concerts.Clear();
            SEvent_SSK.Elections.Clear();
            SEvent_Tour.Tours.Clear();
            SEvent_Tour_FinishTour.Reset();
            SEvent_SSK_SSK_Finish.Reset();
            traits.Traits.Clear();
            resources.resource.Clear();
            resources.Fans.Clear();
            resources.fanOpinions.Clear();
            Language.Variables.Clear();
            policies.Values.Clear();
            Relationships.Cliques.Clear();
            Event_Manager.RandomEvents.Clear();
            Event_Templates.Dating_Reactions.Clear();
            Event_Templates.Active_Template = null;
            DatingScandal.Capturing = false;
            DatingScandal.Reactions.Clear();
            StaffFiring.Clear();
            AuditionHires.Clear();
            GraduationScene.Clear();
            staticVars.dateTime = Today;
        }

        /// <summary>
        /// A clique of these idols, the first its leader, that the player knows about, registered with the game.
        /// </summary>
        public static Relationships._clique Clique(params data_girls.girls[] girls)
        {
            Relationships._clique clique = new() { Leader = girls[0], Known = true };
            clique.Members.AddRange(girls);
            Relationships.Cliques.Add(clique);
            return clique;
        }

        /// <summary>
        /// An active idol, by her nickname (the name the prompt uses), with this many fans, registered with the game.
        /// </summary>
        public static data_girls.girls Idol(string name, long fans = 1000, int age = 19)
        {
            data_girls.girls girl = new()
            {
                nickname = name,
                status = data_girls._status.normal,
                birthday = Today.Date.AddYears(-age).AddDays(-10),
                Hiring_Date = Today.Date.AddYears(-1)
            };
            girl.Fans.Add(new resources._fan { gender = resources.fanType.male, hardcoreness = resources.fanType.casual, age = resources.fanType.teen, people = fans });
            data_girls.girl.Add(girl);
            return girl;
        }

        public static data_girls.girls With(this data_girls.girls girl, data_girls._paramType type, float value)
        {
            girl.parameters.Add(new data_girls.girls.param { type = type, _val = value, Parent = girl });
            return girl;
        }

        public static data_girls.girls Graduated(this data_girls.girls girl, DateTime date)
        {
            girl.status = data_girls._status.graduated;
            girl.Graduation_Date = date;
            return girl;
        }

        public static data_girls.girls WithTrait(this data_girls.girls girl, string title)
        {
            traits._trait trait = new() { Type = traits._trait._type.Prodigy, Title = title };
            traits.Traits.Add(trait);
            girl.trait = trait.Type;
            return girl;
        }

        /// <summary>
        /// A group with these members; the first group made is the main one (ID 0).
        /// </summary>
        public static Groups._group Group(string title, DateTime created, params data_girls.girls[] girls)
        {
            Groups._group group = new() { ID = Groups.Groups_.Count, Title = title, Date_Created = created };
            group.Girls.AddRange(girls);
            Groups.Groups_.Add(group);
            return group;
        }

        /// <summary>
        /// A released single with these members, center first, in the game's list of singles.
        /// </summary>
        public static singles._single Single(string title, DateTime released, long sales, params data_girls.girls[] girls)
        {
            singles._single single = new() { title = title, status = singles._single._status.released, girls = girls.ToList() };
            single.ReleaseData.ReleaseDate = released;
            single.ReleaseData.Sales = sales;
            singles.Singles.Add(single);
            return single;
        }

        /// <summary>
        /// A single's genre, lyrics and choreography, by the titles the game shows; null leaves one unset.
        /// </summary>
        public static singles._single Styled(this singles._single single, string genre, string lyrics, string choreography)
        {
            single.genre = genre == null ? null : new singles._param { type = singles._param._type.genre, title = genre };
            single.lyrics = lyrics == null ? null : new singles._param { type = singles._param._type.lyrics, title = lyrics };
            single.choreography = choreography == null ? null : new singles._param { type = singles._param._type.choreography, title = choreography };
            return single;
        }

        public static singles._single In(this singles._single single, Groups._group group)
        {
            group.Singles.Add(single);
            return single;
        }

        public static SEvent_Concerts._concert Concert(string title, DateTime finished, long audience, bool done = true)
        {
            SEvent_Concerts._concert concert = new() { Title = title, FinishDate = finished, Status = done ? SEvent_Tour.tour._status.finished : SEvent_Tour.tour._status.working };
            concert.ProjectedValues.Actual_Audience = audience;
            SEvent_Concerts.Concerts.Add(concert);
            return concert;
        }

        /// <summary>
        /// A finished election with this many results; the idols given take the first places in order.
        /// </summary>
        public static SEvent_SSK._SSK Election(DateTime finished, int results, params data_girls.girls[] top)
        {
            SEvent_SSK._SSK election = new() { Status = SEvent_Tour.tour._status.finished, FinishDate = finished };
            for (int i = 0; i < results; i++)
            {
                // The game's votes fall with each place
                election.Results.Add(new SEvent_SSK._SSK._result { Girl = i < top.Length ? top[i] : null, Place = i + 1, Votes = (results - i) * 1000 });
            }
            SEvent_SSK.Elections.Add(election);
            return election;
        }

        /// <summary>
        /// A tour with these stops, each played at a venue level with this audience, in the game's list of
        /// tours: (country, level, audience).
        /// </summary>
        public static SEvent_Tour.tour Tour(DateTime finished, bool done, params (SEvent_Tour._country country, int level, int audience)[] stops)
        {
            SEvent_Tour.tour tour = new() { Status = done ? SEvent_Tour.tour._status.finished : SEvent_Tour.tour._status.normal, FinishDate = finished };
            foreach ((SEvent_Tour._country country, int level, int audience) in stops)
            {
                tour.SelectedCountries.Add(new SEvent_Tour.tour.selectedCountry
                {
                    Country = new SEvent_Tour.country { Type = country },
                    Level = level,
                    Audience = audience,
                });
            }
            SEvent_Tour.Tours.Add(tour);
            return tour;
        }

        /// <summary>
        /// Selects a policy value, as the game holds it: one selected value per policy type.
        /// </summary>
        public static void Policy(policies._type type, policies._value value)
        {
            policies.Values.Add(new policies.value { Type = type, Value = value, Selected = true });
        }

        /// <summary>
        /// A handshake event in the single's promotion, for the whole group or for each idol.
        /// </summary>
        public static singles._single WithHandshake(this singles._single single, bool group = true)
        {
            single.marketing.Add(new singles._param { type = singles._param._type.marketing, Group_Handshake = group, Individual_Handshake = !group });
            return single;
        }

        public static Event_Manager._activeEvent Event(string id, DateTime date, params data_girls.girls[] girls)
        {
            Event_Manager._activeEvent active = new() { data = new Event_Manager._randomEvent { id = id }, date = date };
            foreach (data_girls.girls girl in girls)
            {
                active.actors.Add(new Event_Manager._activeEvent._actor { girl = girl, name = "" });
            }
            return active;
        }

        public static List<KeyValuePair<string, string>> Facts(this Moment moment, string section) => moment.Section(section);

        public static string Fact(this Moment moment, string section, string name) =>
            moment.Section(section).Where(f => f.Key == name).Select(f => f.Value).FirstOrDefault();
    }
}
