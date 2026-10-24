namespace UnofficialPatch
{
    // The fixes a player can turn off in ModMenus (assets/JSON/Mod Menu/modmenu.json), each a save variable.
    // Read on every use, so a change applies at once. Without ModMenus the variables don't exist and every fix is on.
    public static class Settings
    {
        public const string TheaterStamina = "UnofficialPatch_TheaterStamina";
        public const string TourFame = "UnofficialPatch_TourFame";
        public const string BestEmployer = "UnofficialPatch_BestEmployer";
        public const string SalaryWishes = "UnofficialPatch_SalaryWishes";
        public const string ScandalStamina = "UnofficialPatch_ScandalStamina";

        public static readonly string[] All = { TheaterStamina, TourFame, BestEmployer, SalaryWishes, ScandalStamina };

        // On unless the box was unticked ("0").
        public static bool On(string varID) => variables.Get(varID) != "0";
    }
}
