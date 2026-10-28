using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConcertHype.Tests.Balance
{
    /// <summary>
    /// The spec's random setups, on the game's senbatsu triangle: 12 idols with stats 30 to 80 and a few much more
    /// popular than the rest, and 10 songs whose senbatsus of 6 to 10 fill the triangle from the top, overlap and mix
    /// genres. These are made-up groups, not real play.
    /// </summary>
    public static class RandomSetups
    {
        // The game's stat appeal (data_girls.GetAppealOfStat): per fan type, coefficient per stat
        // in the order cute, cool, sexy, pretty, vocal, dance, funny, smart.
        private static readonly double[][] StatAppeal =
        {
            new[] { 0.3, 0, 0.3, 0, 0, 0.3, 0.3, 0 },          // male
            new[] { 0, 0.3, 0, 0.3, 0.3, 0, 0, 0.3 },          // female
            new[] { 0, 0, 0, 0, 0.4, 0.4, 0.4, 0.4 },          // casual
            new[] { 0.4, 0.4, 0.4, 0.4, 0, 0, 0, 0 },          // hardcore
            new[] { 0, 0.2, 0.2, 0.1, 0, 0.1, 0.2, 0.1 },      // teen
            new[] { 0.1, 0.1, 0.2, 0.2, 0, 0, 0.2, 0.1 },      // young adult
            new[] { 0.2, 0, 0.1, 0, 0.1, 0, 0.2, 0 },          // adult
        };

        private static readonly string[] Genres = { "Pop", "Rock", "EDM", "Enka", "Metal", "Hip Hop" };

        public static ShowSetup Make(int seed, int members = 12, int songs = 10)
        {
            Random random = new(seed);
            ShowSetup setup = new() { CapacityFilled = 1, Production = 0.5, StartingHype = 50 };
            List<double> danceVocal = new();
            for (int m = 0; m < members; m++)
            {
                double[] stats = Enumerable.Range(0, 8).Select(_ => 30 + 50 * random.NextDouble()).ToArray();
                double fans = Math.Exp(10 + 1.2 * Normal(random));
                setup.Members.Add(new Member
                {
                    Name = "Idol " + m,
                    Fans = fans,
                    Talk = (stats[6] + stats[7]) / 200,
                    Appeal = StatAppeal.Select(row => row.Zip(stats, (c, v) => c * v / 100).Sum()).ToArray(),
                });
                danceVocal.Add(Math.Floor((stats[5] + stats[4]) / 2));
            }

            for (int s = 0; s < songs; s++)
            {
                int size = 6 + random.Next(5);
                List<int> pool = Enumerable.Range(0, members).ToList();
                Song song = new()
                {
                    Title = "Song " + s,
                    Genre = Genres[random.Next(Genres.Length)],
                    Appeal = Enumerable.Range(0, FanTypes.Count).Select(_ => 0.6 * (random.NextDouble() - 0.5) * 3).ToArray(),
                };
                for (int position = 0; position < size; position++)
                {
                    // Popular idols are picked more often
                    double total = pool.Sum(m => Math.Sqrt(setup.Members[m].Fans));
                    double pick = random.NextDouble() * total;
                    int chosen = pool[pool.Count - 1];
                    foreach (int m in pool)
                    {
                        pick -= Math.Sqrt(setup.Members[m].Fans);
                        if (pick <= 0)
                        {
                            chosen = m;
                            break;
                        }
                    }
                    pool.Remove(chosen);
                    song.Slots.Add(new Slot { Position = position, Row = Slot.RowOf(position), Original = chosen });
                }
                setup.Songs.Add(song);
            }

            // The game's concert skill: dance and vocal averaged; a single's own center counts at least 80
            setup.Skill = Enumerable.Range(0, members).Select(m => setup.Songs
                .Select(song => song.Slots[0].Original == m ? Math.Max(80, danceVocal[m]) : danceVocal[m]).ToArray()).ToArray();
            return setup;
        }

        private static double Normal(Random random) =>
            Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
    }
}
