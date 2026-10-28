namespace ConcertHype.Model
{
    /// <summary>
    /// Every number of the show. The defaults are the spec's; concert_hype.json overrides them by field name in camelCase.
    /// Row lists hold one value per row of the senbatsu triangle (rows of 1, 2, 3, 4 and 5 idols).
    /// </summary>
    public sealed class Config
    {
        // Roll
        public double KAppeal = 15;
        public double KSenbatsu = 15;
        public double RollSigma = 10;
        public double EnergyMinFactor = 0.7;
        public int OptionLevel = 1;

        // Hype
        public double StartingHype = 50;
        public double FanWeightFloor = 0.25;
        public double[] RowWeights = { 1.5, 1.0, 0.6, 0.6, 0.6 };
        public double KPerf = 0.5;
        public double Par = 60;
        public double GenreFlowBonus = 3;
        public double CapacityFloor = 0.5;
        public double ProductionFloor = 0.75;
        public double ProductionRange = 0.5;
        public double NoveltyBlendBand = 20;
        public double AmpHigh = 1.5;
        public double AmpLow = 0.5;

        // Energy
        public double EnergyCostBase = 8;
        public double[] RowCost = { 1.5, 1.0, 0.6, 0.6, 0.6 };
        public double KRollCost = 1.0;
        public double RollCostMin = 0.5;
        public double RollCostMax = 1.5;
        public double HypeLiftBase = 2;
        public double HypeLiftPerPoint = 0.1;
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
        public int SubstitutionsPerSong = 1;
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

        private static double Pick(double[] values, int row)
        {
            if (values == null || values.Length == 0)
                return 1;
            return values[row < 0 ? 0 : row >= values.Length ? values.Length - 1 : row];
        }
    }
}
