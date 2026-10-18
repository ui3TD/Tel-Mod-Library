using System;
using System.Collections.Generic;
using System.Linq;

namespace AIFanChatter.LiveReport
{
    /// <summary>
    /// A made-up agency two and a half years into a normal game, held in the game's own static lists the way
    /// a loaded save holds it: a main group of 14 with two graduates, a sister group of 5, 15 singles, five
    /// concerts, one election, the agency's policies, its fans' opinions and a few earlier posts on the feed.
    /// Every idol has every stat, so each line the mod writes about her is filled in. The three moments are
    /// the main group's newest single, the concert just held, and a sister-group idol graduating.
    /// </summary>
    internal sealed class Agency
    {
        public static readonly DateTime Today = new(2025, 9, 14, 10, 0, 0);
        private static readonly DateTime Founded = new(2023, 4, 3);
        private const string MainName = "Kirameki Station";

        public singles._single NewSingle;
        public SEvent_Concerts._concert NewConcert;
        public data_girls.girls Graduate;

        private readonly Dictionary<string, data_girls.girls> idols = new();
        private string setList;
        private Groups._group main;
        private Groups._group sister;

        /// <summary>
        /// Clears the game's lists and fills them with the agency, as a load does. The game's text (Language,
        /// traits) stays as GameText loaded it.
        /// </summary>
        /// <param name="setList">The concert's set list (see SetList); null is the default one.</param>
        public static Agency Build(string setList = null)
        {
            Clear();
            Agency agency = new() { setList = setList };
            agency.Fill();
            return agency;
        }

        /// <summary>
        /// Graduates the moment's idol as the game does: she stays in her group's list, and her place by fans
        /// is taken first, as the mod's graduation patch takes it before the game moves her fans.
        /// </summary>
        public Members.Rank GraduateNow()
        {
            Members.Rank rank = Members.RankOf(Graduate);
            Graduate.status = data_girls._status.graduated;
            Graduate.Graduation_Date = Today;
            Graduate.Graduation_Trivia_Text = "Went back to university to study architecture; still posts dance covers now and then.";
            return rank;
        }

        /// <summary>
        /// The random event happening today, its idol parts played by these idols in order.
        /// </summary>
        public Event_Manager._activeEvent Event(Event_Manager._randomEvent data, IList<string> names)
        {
            Event_Manager._activeEvent active = new() { data = data, date = Today, state = Event_Manager._activeEvent._state.active };
            for (int i = 0; i < data.actor.Count; i++)
            {
                if (data.actor[i].type != Event_Manager._randomEvent.actorType.idol)
                {
                    throw new ArgumentException("Event " + data.id + " has a staff part; only idol parts can be played");
                }
                if (i >= names.Count || !idols.TryGetValue(names[i], out data_girls.girls girl))
                {
                    throw new ArgumentException("Event " + data.id + " needs " + data.actor.Count + " idols by first name; idols: " + string.Join(", ", idols.Keys.ToArray()));
                }
                active.actors.Add(new Event_Manager._activeEvent._actor { data = data.actor[i], girl = girl, name = "" });
            }
            return active;
        }

        private static void Clear()
        {
            data_girls.girl.Clear();
            Groups.Groups_.Clear();
            singles.Singles.Clear();
            SEvent_Concerts.Concerts.Clear();
            SEvent_SSK.Elections.Clear();
            policies.Values.Clear();
            resources.resource.Clear();
            resources.Fans.Clear();
            resources.fanOpinions.Clear();
            SNS_Manager.Messages.Clear();
            staticVars.dateTime = Today;
            staticVars.PlayerData.GroupName = MainName;
            // The producer, whom random events name, and the rival group
            staticVars.PlayerData.FirstName = "Haruto";
            staticVars.PlayerData.LastName = "Sato";
            staticVars.PlayerData.Rival_Group_Name = "Starlight Ribbon";
            staff.Staff.Clear();
            staff.Staff.Add(new staff._staff { type = staff._type.player });
        }

        private void Fill()
        {
            main = Group(0, MainName, Founded);
            sister = Group(1, "Aozora Chapter", new DateTime(2024, 10, 1));

            // Name, age, hired, fans, trait, vocal, dance, cute, cool, pretty, sexy, funny, smart
            Idol(main, "Hoshino", "Mirai", 19, Founded, 82000, traits._trait._type.Prodigy, 88, 71, 64, 58, 70, 40, 45, 62)
                .Trivia("Practises scales in the stairwell before every show.", "Owns more than forty pairs of headphones.");
            Idol(main, "Takeda", "Rin", 21, Founded, 64000, traits._trait._type.Steadfast, 66, 83, 51, 77, 63, 55, 38, 70)
                .Trivia("Captained her high school's volleyball team.", "Writes every fan letter's sender a short reply.");
            Idol(main, "Aoki", "Hinata", 17, Founded, 71000, traits._trait._type.Photogenic, 59, 64, 92, 41, 81, 30, 66, 49)
                .Nickname("Hina");
            Idol(main, "Mori", "Sayaka", 23, Founded, 38000, traits._trait._type.Perfectionist, 74, 79, 47, 72, 76, 61, 33, 81)
                .Scandals(1);
            Idol(main, "Kudo", "Nanami", 16, new DateTime(2023, 9, 15), 45000, traits._trait._type.Precocious, 61, 68, 77, 52, 60, 25, 70, 57);
            Idol(main, "Shimizu", "Kaede", 20, new DateTime(2023, 9, 15), 29000, traits._trait._type.Clumsy, 55, 49, 73, 38, 58, 42, 79, 44)
                .Stamina(mental: 34);
            Idol(main, "Fujii", "Mao", 22, new DateTime(2023, 9, 15), 33000, traits._trait._type.Meme_queen, 48, 57, 56, 50, 54, 47, 91, 60);
            Idol(main, "Ishikawa", "Yui", 18, new DateTime(2024, 2, 1), 27000, traits._trait._type.Late_bloomer, 63, 60, 62, 55, 57, 33, 52, 55);
            Idol(main, "Ogawa", "Rena", 24, new DateTime(2024, 2, 1), 21000, traits._trait._type.Lone_Wolf, 57, 66, 35, 84, 68, 73, 29, 66)
                .Scandals(2);
            Idol(main, "Nishida", "Koharu", 15, new DateTime(2024, 6, 10), 18000, traits._trait._type.Underdog, 52, 61, 70, 44, 52, 20, 58, 47);
            Idol(main, "Endo", "Ayaka", 19, new DateTime(2024, 6, 10), 16000, traits._trait._type.Worrier, 60, 45, 58, 47, 62, 36, 41, 73)
                .Stamina(physical: 41);
            Idol(main, "Matsuda", "Emi", 20, new DateTime(2025, 1, 20), 9500, traits._trait._type.Trendy, 46, 54, 61, 63, 72, 51, 49, 52);
            Idol(main, "Hayashi", "Chiyo", 17, new DateTime(2025, 1, 20), 7800, traits._trait._type.Tenacious, 50, 58, 55, 49, 50, 28, 46, 58);
            Idol(main, "Sakai", "Momoka", 16, new DateTime(2025, 5, 12), 4200, traits._trait._type.Anxiety, 44, 41, 66, 37, 55, 22, 39, 51);
            Idol(main, "Kimura", "Asuka", 23, Founded, 0, traits._trait._type.Loyal, 64, 70, 50, 66, 69, 58, 44, 63)
                .Graduated(new DateTime(2024, 12, 22), "Opened a dance studio in her hometown.");
            Idol(main, "Uchida", "Noa", 22, new DateTime(2023, 9, 15), 0, traits._trait._type.Forgiving, 58, 52, 69, 45, 61, 39, 63, 48)
                .Graduated(new DateTime(2025, 6, 29), "Became a radio presenter in Osaka.");

            Idol(sister, "Kato", "Hikari", 20, new DateTime(2024, 10, 1), 26000, traits._trait._type.Shameless, 62, 76, 48, 69, 66, 70, 57, 50);
            Idol(sister, "Yamane", "Sora", 18, new DateTime(2024, 10, 1), 24000, traits._trait._type.Loyal, 71, 58, 63, 52, 59, 35, 48, 66);
            Idol(sister, "Mizuno", "Kaho", 21, new DateTime(2024, 10, 1), 19000, traits._trait._type.Resilient, 55, 81, 52, 60, 58, 49, 44, 54)
                .Stamina(physical: 30)
                .Trivia("Taught herself to dance from music videos.", "Has never missed a sunrise on New Year's Day.");
            Idol(sister, "Ueda", "Miyu", 22, new DateTime(2024, 10, 1), 15000, traits._trait._type.Secretive, 49, 57, 44, 71, 74, 62, 36, 69)
                .Scandals(2);
            Idol(sister, "Tanaka", "Ruri", 17, new DateTime(2025, 3, 3), 11000, traits._trait._type.Amorous, 53, 50, 72, 40, 57, 31, 60, 46);
            Graduate = idols["Kaho"];

            Singles();
            Concerts();
            Election();
            Policies();
            Fans();
            Feed();
        }

        private void Singles()
        {
            // Title, released, sales, quality, satisfaction, genre, lyrics, choreography, center then members
            Single(main, "Departure Bell", new DateTime(2023, 6, 18), 6200, 3, 55, "Pop", "Friends", "Cute", "Mirai", "Rin", "Hinata", "Sayaka", "Asuka");
            Single(main, "Window Seat Summer", new DateTime(2023, 8, 27), 11800, 4, 61, "Pop", "Happiness", "Energetic", "Hinata", "Mirai", "Rin", "Sayaka", "Asuka");
            Single(main, "Last Train Lullaby", new DateTime(2023, 11, 19), 19500, 4, 58, "Ballad", "Loneliness", "Elegant", "Mirai", "Sayaka", "Rin", "Nanami", "Noa", "Asuka");
            Single(main, "Snow Globe Romance", new DateTime(2024, 1, 28), 24000, 5, 66, "Pop", "Love", "Cute", "Hinata", "Nanami", "Kaede", "Mirai", "Noa");
            Single(main, "Rush Hour Rebellion", new DateTime(2024, 4, 14), 37000, 5, 71, "Rock", "Rebelling", "Cool", "Rin", "Sayaka", "Rena", "Mao", "Mirai", "Asuka");
            Single(main, "Platform 7 Promise", new DateTime(2024, 6, 23), 41500, 6, 74, "Pop", "Love", "Cute", "Mirai", "Hinata", "Nanami", "Yui", "Kaede", "Noa");
            Single(main, "Neon Timetable", new DateTime(2024, 9, 1), 58000, 5, 62, "EDM", "Partying", "Energetic", "Hinata", "Mao", "Rin", "Yui", "Koharu", "Mirai");
            Single(main, "Goodbye, Ticket Gate", new DateTime(2024, 11, 24), 52300, 7, 85, "Ballad", "Regret", "Elegant", "Sayaka", "Mirai", "Rin", "Asuka", "Ayaka", "Noa");
            Single(main, "Spring Express", new DateTime(2025, 2, 16), 76000, 6, 78, "Pop", "Happiness", "Cute", "Nanami", "Hinata", "Mirai", "Koharu", "Emi", "Kaede");
            Single(main, "Signal Fire", new DateTime(2025, 5, 4), 91000, 7, 80, "Rock", "Friends", "Energetic", "Rin", "Mirai", "Rena", "Sayaka", "Yui", "Hinata", "Mao");
            Single(main, "Under the Overpass", new DateTime(2025, 7, 13), 88500, 6, 69, "Funk", "Anxiety", "Cool", "Rena", "Rin", "Sayaka", "Mao", "Ayaka", "Chiyo");
            NewSingle = Single(main, "Midnight Platform", Today, 104000, 8, 82, "Pop", "Loneliness", "Elegant",
                "Mirai", "Hinata", "Rin", "Sayaka", "Nanami", "Kaede", "Yui", "Emi");
            NewSingle.ReleaseData.NewFans = 4100;
            // A group handshake event and an ad campaign, the game's own, and the campaign's roll a success
            NewSingle.marketing.Add(GameText.Marketing.First(m => m.Group_Handshake));
            NewSingle.marketing.Add(GameText.Marketing.First(m => m.Special_Type == singles._param._special_type.ad_campaign));
            NewSingle.Marketing_Result_Status = Single_Marketing_Roll._result.success;

            Single(sister, "Blue Hour", new DateTime(2024, 12, 8), 14000, 4, 63, "Pop", "Friends", "Cute", "Sora", "Hikari", "Kaho", "Miyu");
            Single(sister, "Paper Plane", new DateTime(2025, 3, 30), 21500, 5, 70, "Rock", "Happiness", "Energetic", "Hikari", "Kaho", "Sora", "Miyu", "Ruri");
            Single(sister, "Cloudline", new DateTime(2025, 8, 10), 27000, 6, 74, "EDM", "Love", "Cool", "Kaho", "Hikari", "Sora", "Ruri", "Miyu");
        }

        private void Concerts()
        {
            Concert(SEvent_Concerts._venue.club, new DateTime(2023, 8, 20), 280, 0.93f, 88);
            Concert(SEvent_Concerts._venue.concertHall, new DateTime(2024, 3, 17), 1400, 0.88f, 112);
            Concert(SEvent_Concerts._venue.openAirStage, new DateTime(2024, 8, 25), 3800, 0.76f, 97);
            Concert(SEvent_Concerts._venue.concertHall, new DateTime(2025, 2, 23), 2000, 1f, 143);
            NewConcert = Concert(SEvent_Concerts._venue.openAirStage, Today, 4600, 0.92f, 156);
            SetList(setList ?? DefaultSetList);
        }

        /// <summary>
        /// The concert's set list: items split by ";", each a song as "Single title/center" or a talk break
        /// as "talk: idol, idol". Idols go by first name.
        /// </summary>
        public const string DefaultSetList = "Signal Fire/Rin; Platform 7 Promise/Mirai; Neon Timetable/Hinata; talk: Mao, Kaede, Koharu; "
            + "Cloudline/Hikari; Goodbye, Ticket Gate/Sayaka; Under the Overpass/Rena; Spring Express/Nanami; talk: Hinata, Yui; "
            + "Rush Hour Rebellion/Rin; Midnight Platform/Mirai; Departure Bell/Mirai";

        private void SetList(string spec)
        {
            data_girls.girls Idol(string name) => idols.TryGetValue(name.Trim(), out data_girls.girls girl) ? girl
                : throw new ArgumentException("No idol named " + name.Trim() + "; idols go by first name: " + string.Join(", ", idols.Keys.ToArray()));
            NewConcert.SetListItems.Clear();
            foreach (string item in spec.Split(';').Select(i => i.Trim()).Where(i => i != ""))
            {
                if (item.StartsWith("talk:", StringComparison.OrdinalIgnoreCase))
                {
                    NewConcert.SetListItems.Add(new SEvent_Concerts._concert._mc { Girls = item.Substring(5).Split(',').Select(Idol).ToList() });
                    continue;
                }
                int slash = item.LastIndexOf('/');
                string title = slash < 0 ? item : item.Substring(0, slash).Trim();
                singles._single single = singles.Singles.FirstOrDefault(s => s.title == title)
                    ?? throw new ArgumentException("No single titled " + title + "; the singles are: " + string.Join(", ", singles.Singles.Select(s => s.title).ToArray()));
                NewConcert.SetListItems.Add(new SEvent_Concerts._concert._song { Single = single, Center = slash < 0 ? single.GetCenter() : Idol(item.Substring(slash + 1)) });
            }
        }

        /// <summary>
        /// The general election in June, before Uchida Noa graduated, across both groups.
        /// </summary>
        private void Election()
        {
            SEvent_SSK._SSK election = new() { Status = SEvent_Tour.tour._status.finished, FinishDate = new DateTime(2025, 6, 21) };
            string[] order = { "Mirai", "Hinata", "Rin", "Nanami", "Sayaka", "Hikari", "Mao", "Sora", "Kaede", "Noa", "Kaho", "Yui", "Rena", "Koharu", "Miyu", "Ayaka", "Ruri", "Emi", "Chiyo", "Momoka" };
            for (int i = 0; i < order.Length; i++)
            {
                data_girls.girls girl = idols[order[i]];
                election.Results.Add(new SEvent_SSK._SSK._result { Girl = girl, Place = i + 1 });
                girl.SSK_History.Add(i + 1);
            }
            SEvent_SSK.Elections.Add(election);
        }

        private static void Policies()
        {
            Policy(policies._type.vibe, policies._value.vibe_cute);
            Policy(policies._type.image, policies._value.image_orthodox);
            Policy(policies._type.dating, policies._value.dating_forbidden);
            Policy(policies._type.skirts, policies._value.skirts_safety_shorts);
            Policy(policies._type.performances, policies._value.performances_energy);
            Policy(policies._type.security, policies._value.security_restrictive);
        }

        /// <summary>
        /// The agency's resources (money, fans, buzz, scandal points, fame) and its fan groups, with each fan
        /// type's record of good and bad reactions, as resources.GetFanOpinion reads them. Each trait leans one
        /// way, so the fan types use the whole scale: devoted adult men 100, casual teen girls 0, and 17, 33,
        /// 50, 67 and 83 between.
        /// </summary>
        private static void Fans()
        {
            resources.resource.AddRange(new long[] { 48000000, 0, 1200, 0, 4 });
            var opinions = new (resources.fanType Type, int Good, int Bad)[]
            {
                (resources.fanType.male, 20, 0), (resources.fanType.female, 0, 20),
                (resources.fanType.casual, 0, 20), (resources.fanType.hardcore, 20, 0),
                (resources.fanType.teen, 0, 20), (resources.fanType.youngAdult, 10, 10), (resources.fanType.adult, 20, 0),
            };
            foreach (var (type, good, bad) in opinions)
            {
                resources._fanOpinion opinion = new() { type = type };
                opinion.Vals.AddRange(Enumerable.Repeat(1, good).Concat(Enumerable.Repeat(-1, bad)));
                resources.fanOpinions.Add(opinion);
            }
            long people = 1000;
            foreach (resources.fanType gender in new[] { resources.fanType.male, resources.fanType.female })
                foreach (resources.fanType devotion in new[] { resources.fanType.casual, resources.fanType.hardcore })
                    foreach (resources.fanType age in new[] { resources.fanType.teen, resources.fanType.youngAdult, resources.fanType.adult })
                        resources.Fans.Add(new resources._fan { gender = gender, hardcoreness = devotion, age = age, people = people += 7000 });
        }

        /// <summary>
        /// Posts from the week before, as the game's feed shows them.
        /// </summary>
        private static void Feed()
        {
            foreach (string[] post in new[]
            {
                new[] { "M_HC_YA", "Handshake tickets for Midnight Platform secured. Row 3 at the open air show too, best week ever" },
                new[] { "F_C_T", "Hina's photo book preview is so cute I can't" },
                new[] { "M_C_A", "Kirameki Station on the morning radio. Mirai sings live better than on the CD" },
                new[] { "F_HC_A", "Missed the Aozora Chapter stream. Did Kaho say anything about her break?" },
                new[] { "M_C_T", "why are the open air tickets so expensive lol" },
            })
            {
                SNS_Manager.Messages.Add(new SNS_Manager._message { User = post[0], Message = "<noparse>" + post[1] + "</noparse>" });
            }
        }

        private static Groups._group Group(int id, string title, DateTime created)
        {
            Groups._group group = new() { ID = id, Title = title, Date_Created = created };
            Groups.Groups_.Add(group);
            return group;
        }

        private IdolBuilder Idol(Groups._group group, string last, string first, int age, DateTime hired, long fans, traits._trait._type trait,
            params float[] stats)
        {
            data_girls.girls girl = new()
            {
                id = data_girls.girl.Count,
                firstName = first,
                lastName = last,
                status = data_girls._status.normal,
                // Her birthday a few months after today's date, so she is the age given
                birthday = Today.Date.AddYears(-age - 1).AddMonths(3),
                Hiring_Date = hired,
            };
            data_girls._paramType[] types =
            {
                data_girls._paramType.vocal, data_girls._paramType.dance, data_girls._paramType.cute, data_girls._paramType.cool,
                data_girls._paramType.pretty, data_girls._paramType.sexy, data_girls._paramType.funny, data_girls._paramType.smart,
            };
            for (int i = 0; i < types.Length; i++)
            {
                Param(girl, types[i], stats[i]);
            }
            Param(girl, data_girls._paramType.physicalStamina, 85);
            Param(girl, data_girls._paramType.mentalStamina, 80);
            Param(girl, data_girls._paramType.scandalPoints, 0);
            // A third of her fans are devoted; most are men, as in the game's default fan mix
            girl.Fans.Add(new resources._fan { gender = resources.fanType.male, hardcoreness = resources.fanType.casual, age = resources.fanType.youngAdult, people = fans * 5 / 12 });
            girl.Fans.Add(new resources._fan { gender = resources.fanType.male, hardcoreness = resources.fanType.hardcore, age = resources.fanType.adult, people = fans / 3 });
            girl.Fans.Add(new resources._fan { gender = resources.fanType.female, hardcoreness = resources.fanType.casual, age = resources.fanType.teen, people = fans - fans * 5 / 12 - fans / 3 });
            if (traits.GetTrait(trait) == null)
            {
                traits.Traits.Add(new traits._trait { Type = trait, Title = trait.ToString().Replace('_', ' ') });
            }
            girl.trait = trait;
            data_girls.girl.Add(girl);
            group.Girls.Add(girl);
            idols[first] = girl;
            return new IdolBuilder(girl);
        }

        private static void Param(data_girls.girls girl, data_girls._paramType type, float value)
        {
            girl.parameters.Add(new data_girls.girls.param { type = type, _val = value, Parent = girl });
        }

        private singles._single Single(Groups._group group, string title, DateTime released, long sales, int quality, int satisfaction,
            string genre, string lyrics, string choreography, params string[] members)
        {
            singles._single single = new()
            {
                title = title,
                status = singles._single._status.released,
                girls = members.Select(n => idols[n]).ToList(),
                genre = new singles._param { type = singles._param._type.genre, title = genre },
                lyrics = new singles._param { type = singles._param._type.lyrics, title = lyrics },
                choreography = new singles._param { type = singles._param._type.choreography, title = choreography },
            };
            single.ReleaseData.ReleaseDate = released;
            single.ReleaseData.Sales = sales;
            single.ReleaseData.Quality = quality;
            single.ReleaseData.Fan_Satisfaction = satisfaction;
            single.ReleaseData.NewFans = (int)(sales / 25);
            singles.Singles.Add(single);
            group.Singles.Add(single);
            return single;
        }

        private static SEvent_Concerts._concert Concert(SEvent_Concerts._venue venue, DateTime finished, long audience, float attendance, int hype)
        {
            SEvent_Concerts._concert concert = new() { Venue = venue, FinishDate = finished, Status = SEvent_Tour.tour._status.finished };
            concert.ProjectedValues.Actual_Audience = audience;
            concert.ProjectedValues.Actual_Attendance = attendance;
            concert.ProjectedValues.Actual_Hype = hype;
            concert.ProjectedValues.TicketPrice = venue == SEvent_Concerts._venue.club ? 2500 : 4800;
            SEvent_Concerts.Concerts.Add(concert);
            return concert;
        }

        private static void Policy(policies._type type, policies._value value)
        {
            policies.Values.Add(new policies.value { Type = type, Value = value, Selected = true });
        }

        private sealed class IdolBuilder
        {
            private readonly data_girls.girls girl;

            public IdolBuilder(data_girls.girls girl) => this.girl = girl;

            public IdolBuilder Trivia(params string[] trivia)
            {
                girl.Trivia.AddRange(trivia);
                return this;
            }

            public IdolBuilder Nickname(string nickname)
            {
                girl.nickname = nickname;
                return this;
            }

            public IdolBuilder Scandals(float points)
            {
                girl.getParam(data_girls._paramType.scandalPoints)._val = points;
                return this;
            }

            public IdolBuilder Stamina(float physical = 85, float mental = 80)
            {
                girl.getParam(data_girls._paramType.physicalStamina)._val = physical;
                girl.getParam(data_girls._paramType.mentalStamina)._val = mental;
                return this;
            }

            public IdolBuilder Graduated(DateTime date, string afterwards)
            {
                girl.status = data_girls._status.graduated;
                girl.Graduation_Date = date;
                girl.Graduation_Trivia_Text = afterwards;
                return this;
            }
        }
    }
}
