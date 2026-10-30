namespace ConcertHype.Model
{
    /// <summary>
    /// Every number of the show.
    /// Row lists hold one value per row of the senbatsu triangle (rows of 1, 2, 3, 4 and 5 idols).
    /// </summary>
    public sealed class Config
    {
        // Roll
        /// <summary>
        /// Skill counts as skillBase + skillScale x skill, so low-stat idols perform closer to the strong ones.
        /// </summary>
        public double SkillBase = 30;
        public double SkillScale = 0.7;

        public double KSenbatsu = 15;
        public double RollSigma = 10;
        public double EnergyMinFactor = 0.7;

        /// <summary>
        /// Roll points added in each row: back rows have an easier skill check.
        /// </summary>
        public double[] RowEase = { 0, 4, 8, 12, 16 };

        /// <summary>
        /// The lowest roll that is a critical success. Under par is a failure, under accidentRollThreshold a
        /// critical failure.
        /// </summary>
        public double CriticalSuccessRoll = 80;

        // Hype
        public double StartingHype = 50;
        public double FanWeightFloor = 0.25;
        public double[] RowWeights = { 1.5, 1.0, 0.6, 0.6, 0.6 };
        public double KPerf = 0.5;
        /// <summary>
        /// The roll a success needs, and the one a song's energy cost is measured from.
        /// </summary>
        public double Par = 60;

        /// <summary>
        /// The lineup's weighted roll that leaves the hype as it is: above it the performance adds hype, below it takes
        /// some away.
        /// </summary>
        public double HypePar = 90;

        /// <summary>
        /// Production: hype points for how the song suits the lineup's fans (appealPoints x crowd appeal, kept within
        /// plus or minus appealMax) and for each of genre, lyrics and choreography it shares with the song before it
        /// (flowPoints each); a changed lineup amplifies those two. Then up to qualityBonus for production quality:
        /// the full bonus once the production reaches what the venue's size needs, from qualityNeededLow at
        /// qualitySeatsLow seats or fewer to qualityNeededHigh at qualitySeatsHigh or more, on a log scale between.
        /// </summary>
        public double AppealPoints = 15;
        public double AppealMax = 6;
        public double FlowPoints = 2;
        public double QualityBonus = 4;
        public double QualitySeatsLow = 500;
        public double QualitySeatsHigh = 50000;
        public double QualityNeededLow = 0.2;
        public double QualityNeededHigh = 1;

        /// <summary>
        /// Hype points every song gets from the venue, gain or loss: when it's sold out, venueFirstSellOutBonus the
        /// first time a venue of its type sells out and venueSoldOutBonus after that; nothing from venueNeutralShare of
        /// the seats filled up to the last unsold seat; below that a penalty growing in a straight line to
        /// venueEmptyPenalty when it's empty.
        /// </summary>
        public double VenueSoldOutBonus = 3;
        public double VenueFirstSellOutBonus = 10;
        public double VenueNeutralShare = 0.9;
        public double VenueEmptyPenalty = 15;

        public double NoveltyBlendBand = 20;
        public double AmpHigh = 1.5;
        public double AmpLow = 0.5;

        // Energy
        public double EnergyCostBase = 8;
        public double[] RowCost = { 1.5, 1.0, 0.6, 0.6, 0.6 };
        public double KRollCost = 1.0;
        public double RollCostMin = 0.5;
        public double RollCostMax = 1.5;
        public double BenchRecovery = 10;
        public double ReturnThreshold = 30;

        // Accidents
        public double PBase = 0.005;
        public double AccidentRollThreshold = 40;
        public double PRoll = 0.01;
        public double PTired = 0.05;
        public double PMax = 0.5;
        public int AbsenceMin = 1;
        public int AbsenceMax = 3;

        // Player actions
        public int McCharges = 2;
        public double KMc = 0.5;
        public double McRecovery = 10;
        public int McTimeUnits = 1;

        // Score
        public double WAvg = 0.5;
        public double WPeak = 0.25;
        public double WEnd = 0.25;

        /// <summary>
        /// How long a song or talk break plays on screen, in seconds; the original game takes 10.
        /// </summary>
        public double SongSeconds = 5;

        /// <summary>
        /// A fixed seed for every show, to replay one; 0 picks a new seed each show.
        /// </summary>
        public int Seed = 0;

        public double RowWeight(int row) => Pick(RowWeights, row);

        public double RowCostOf(int row) => Pick(RowCost, row);

        public double RowEaseOf(int row) => RowEase == null || RowEase.Length == 0 ? 0 : Pick(RowEase, row);

        private static double Pick(double[] values, int row)
        {
            if (values == null || values.Length == 0)
                return 1;
            return values[row < 0 ? 0 : row >= values.Length ? values.Length - 1 : row];
        }
    }
}
