using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Module = NationalTour.Readiness.Module;

namespace NationalTour
{
    /// <summary>
    /// Saving and loading national tours. The save file keeps only base-game values, so it loads without the mod:
    /// national stops are written as their paired countries, and a national tour in production fills the game's
    /// current-tour field when no world tour does. Which tours are national, the current national tour and the
    /// national cooldown go in one saved variable. Prefecture venue levels and fan fatigue aren't saved: they are
    /// replayed from the finished national tours.
    /// </summary>
    public static class SaveState
    {
        public const string VARIABLE = "com.tel.nationaltour";
        private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";

        public class Entry
        {
            public List<int> Tours = new();
            public int Current = -1;
            public DateTime CooldownStart;
            public DateTime CooldownStop;

            public bool IsEmpty => Tours.Count == 0 && Current == -1 && CooldownStop == default;
        }

        /// <summary>
        /// "tours=12,15;current=15;cooldown=2026-05-01 00:00:00|2026-08-01 00:00:00"
        /// </summary>
        public static string Format(Entry entry)
        {
            List<string> parts = new() { "tours=" + string.Join(",", entry.Tours.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToArray()) };
            if (entry.Current != -1)
                parts.Add("current=" + entry.Current.ToString(CultureInfo.InvariantCulture));
            if (entry.CooldownStop != default)
                parts.Add("cooldown=" + entry.CooldownStart.ToString(DATE_FORMAT, CultureInfo.InvariantCulture) + "|" + entry.CooldownStop.ToString(DATE_FORMAT, CultureInfo.InvariantCulture));
            return string.Join(";", parts.ToArray());
        }

        /// <summary>
        /// Reads what it can: a part it doesn't understand is skipped.
        /// </summary>
        public static Entry Parse(string value)
        {
            Entry entry = new();
            foreach (string part in (value ?? "").Split(';'))
            {
                int equals = part.IndexOf('=');
                if (equals < 0)
                    continue;
                string key = part.Substring(0, equals);
                string data = part.Substring(equals + 1);
                if (key == "tours")
                {
                    foreach (string id in data.Split(','))
                    {
                        if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                            entry.Tours.Add(parsed);
                    }
                }
                else if (key == "current" && int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out int current))
                    entry.Current = current;
                else if (key == "cooldown")
                {
                    string[] dates = data.Split('|');
                    if (dates.Length == 2
                        && DateTime.TryParseExact(dates[0], DATE_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime start)
                        && DateTime.TryParseExact(dates[1], DATE_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime stop))
                    {
                        entry.CooldownStart = start;
                        entry.CooldownStop = stop;
                    }
                }
            }
            return entry;
        }

        private static SEvent_Tour._country PairedCountry(SEvent_Tour._country prefecture)
        {
            foreach (KeyValuePair<SEvent_Tour._country, Prefectures.Prefecture> pair in Prefectures.ForCountry)
            {
                if (Prefectures.TypeOf(pair.Value) == prefecture)
                    return pair.Key;
            }
            return prefecture;
        }

        /// <summary>
        /// After the game has written its tour data.
        /// </summary>
        public static void OnSave(SaveManager.SavedData data)
        {
            Entry entry = new();
            foreach (SEvent_Tour.tour tour in SEvent_Tour.Tours)
            {
                if (Prefectures.IsNational(tour))
                    entry.Tours.Add(tour.ID);
            }
            SEvent_Tour.tour current = NationalHandler.Instance is not null ? NationalHandler.Instance.Tour : null;
            if (current != null)
                entry.Current = current.ID;
            if (NationalRecord.Record.Cooldown_Stop != default)
            {
                entry.CooldownStart = NationalRecord.Record.Cooldown_Start;
                entry.CooldownStop = NationalRecord.Record.Cooldown_Stop;
            }

            // National stops as their paired countries: the only numbers the game (without the mod) knows
            HashSet<int> national = new(entry.Tours);
            foreach (SEvent_Tour.TourData tour in data.SEvent_Tour__Tours ?? new List<SEvent_Tour.TourData>())
            {
                if (!national.Contains(tour.ID))
                    continue;
                foreach (SEvent_Tour.Tour_CountryData stop in tour.SelectedCountries)
                {
                    if (Prefectures.IsPrefecture(stop.Country))
                        stop.Country = PairedCountry(stop.Country);
                }
            }

            // Without the mod, a national tour in production is the world tour in production
            if (data.SEvent_Tour__Tour == -1 && entry.Current != -1)
                data.SEvent_Tour__Tour = entry.Current;

            // The game saves its live variable list, whichever of the two is saved first
            if (entry.IsEmpty)
                variables.Delete(VARIABLE);
            else
                SetVariable(Format(entry));
        }

        /// <summary>
        /// Not variables.Set, which also checks the story's tasks for a new variable.
        /// </summary>
        private static void SetVariable(string value)
        {
            foreach (variables._variable variable in variables.variable)
            {
                if (variable.name == VARIABLE)
                {
                    variable.value = value;
                    return;
                }
            }
            variables.variable.Add(new variables._variable { name = VARIABLE, value = value });
        }

        /// <summary>
        /// After the game has loaded its tours (stops on the paired countries) and its current tour. A save without
        /// the entry loads as the base game: no national tours, no national cooldown, every prefecture at the start.
        /// </summary>
        public static void OnLoad(SEvent_Tour game, SaveManager.SavedData data)
        {
            NationalRecord.Reset();
            if (NationalHandler.Instance is not null)
                NationalHandler.Instance.Tour = null;
            foreach (SEvent_Tour.country prefecture in Prefectures.All)
            {
                prefecture.Level = 1;
                prefecture.ConcertCount = 0;
                prefecture.LastConcertDate = default;
            }

            string value = data.variables__variables?.FirstOrDefault(v => v != null && v.name == VARIABLE)?.value;
            if (value == null)
                return;
            Entry entry = Parse(value);

            // Stops back on the prefectures
            List<SEvent_Tour.tour> tours = new();
            foreach (int id in entry.Tours)
            {
                SEvent_Tour.tour tour = SEvent_Tour.GetTourByID(id);
                if (tour == null)
                    continue;
                foreach (SEvent_Tour.tour.selectedCountry stop in tour.SelectedCountries)
                {
                    if (stop.Country != null && Prefectures.ForCountry.TryGetValue(stop.Country.Type, out Prefectures.Prefecture prefecture))
                    {
                        SEvent_Tour.country destination = Prefectures.Get(Prefectures.TypeOf(prefecture));
                        if (destination != null)
                            stop.Country = destination;
                    }
                }
                tours.Add(tour);
            }

            // The national tour in production, out of the World Tour's slot if the game put it there
            SEvent_Tour.tour current = entry.Current == -1 ? null : SEvent_Tour.GetTourByID(entry.Current);
            if (current != null && Prefectures.IsNational(current) && NationalHandler.Instance is not null)
            {
                NationalHandler.Instance.Tour = current;
                if (game.Tour == current)
                    game.Tour = null;
            }

            NationalRecord.Record.Cooldown_Start = entry.CooldownStart;
            NationalRecord.Record.Cooldown_Stop = entry.CooldownStop;
            NationalRecord.Record.Status = NationalRecord.AnyUnfinished(national: true)
                ? SpecialEvents_Manager._specialEvent._status.production
                : SpecialEvents_Manager._specialEvent._status.normal;

            Replay(tours);
        }

        /// <summary>
        /// Each prefecture's venue level, concert count and last concert date, as the results popup changed them:
        /// finished national tours in the order they were made.
        /// </summary>
        public static void Replay(IEnumerable<SEvent_Tour.tour> tours)
        {
            foreach (SEvent_Tour.tour tour in tours.Where(t => t.Status == SEvent_Tour.tour._status.finished && Prefectures.IsNational(t)).OrderBy(t => t.ID))
            {
                foreach (SEvent_Tour.tour.selectedCountry stop in tour.SelectedCountries)
                {
                    if (stop.Country == null)
                        continue;
                    if (stop.ShouldLevelUp())
                        stop.Country.LevelUp();
                    stop.Country.ConcertCount++;
                    // Time stands still while the results are shown, so they ended on the day the tour finished
                    stop.Country.LastConcertDate = tour.FinishDate;
                }
            }
        }
    }


    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.SaveFunction))]
    public class SEvent_Tour_SaveFunction
    {
        public static void Postfix(SEvent_Tour __instance)
        {
            if (NationalHandler.IsNationalHandler(__instance))
                return;
            try
            {
                SaveState.OnSave(Camera.main.GetComponent<mainScript>().GetSavedData());
            }
            catch (Exception e)
            {
                Readiness.Fail(Module.Save, "saving failed: " + e);
            }
        }
    }


    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.LoadFunction))]
    public class SEvent_Tour_LoadFunction
    {
        public static void Postfix(SEvent_Tour __instance)
        {
            if (NationalHandler.IsNationalHandler(__instance))
                return;
            try
            {
                SaveState.OnLoad(__instance, Camera.main.GetComponent<mainScript>().GetSavedData());
                // The game drew the event list before the stops were prefectures again
                __instance.GetComponent<SpecialEvents_Manager>().RenderTab();
            }
            catch (Exception e)
            {
                // An error here would stop the rest of the game's loading
                Readiness.Fail(Module.Save, "loading failed: " + e);
            }
        }
    }
}
