using ConcertHype.Model;
using System;
using System.Linq;

namespace ConcertHype
{
    public enum Spot
    {
        /// <summary>
        /// The bench column. As where a drag ends: anywhere that isn't a place or a host place.
        /// </summary>
        Bench,
        Place,
        Host,
    }

    /// <summary>
    /// Where a drag starts or ends.
    /// </summary>
    public struct DropPoint : IEquatable<DropPoint>
    {
        public Spot Spot;

        /// <summary>
        /// The place's index in the song's lineup, or in the talk break's row of hosts; -1 for the bench.
        /// </summary>
        public int Place;

        public static DropPoint Bench => new DropPoint { Spot = Spot.Bench, Place = -1 };

        public static DropPoint AtHost(int place) => new DropPoint { Spot = Spot.Host, Place = place };

        public static DropPoint AtPlace(int place) => new DropPoint { Spot = Spot.Place, Place = place };

        public bool Equals(DropPoint other) => Spot == other.Spot && Place == other.Place;

        public override bool Equals(object obj) => obj is DropPoint other && Equals(other);

        public override int GetHashCode() => (int)Spot * 31 + Place;

        public override string ToString() => Spot == Spot.Place ? "place " + Place : Spot == Spot.Host ? "host place " + Place : Spot.ToString();
    }

    /// <summary>
    /// What a drop does: the lineup and talk break hosts after it, or why it's refused.
    /// </summary>
    public sealed class DropResult
    {
        public bool Allowed;
        public bool Changed;
        public int[] Lineup;

        /// <summary>
        /// The talk break's row of host places, -1 where nobody is.
        /// </summary>
        public int[] Hosts;

        /// <summary>
        /// Why the drop is refused, in one sentence; empty when it's allowed.
        /// </summary>
        public string Message = "";
    }

    /// <summary>
    /// The drag-and-drop rules of the lineup screen. A bench idol dropped on a place takes it, and its previous idol
    /// returns to the bench; a song can have any number of substitutes. Two idols on stage dropped on each other
    /// swap places. A substitute dragged off her place brings the place's own idol back. The talk break's row of
    /// host places works the same way, with up to <see cref="Engine.MaxHosts"/> hosts; any idol who can perform can
    /// host, performers included. Song places and host places are on separate tabs, so nobody is dropped from one
    /// onto the other.
    /// </summary>
    public static class DropRules
    {
        public static DropResult Drop(ShowState state, int[] lineup, int[] hosts, int member, DropPoint from, DropPoint to)
        {
            if (state.Finished || member < 0 || from.Equals(to))
                return Keep(lineup, hosts);
            switch (from.Spot)
            {
                case Spot.Host:
                    if (to.Spot == Spot.Host)
                        return Hosts(state, lineup, hosts, member, to.Place);
                    // Dragged off the row: she doesn't host
                    return Unhosts(lineup, hosts, from.Place);
                case Spot.Place:
                    if (to.Spot == Spot.Place)
                        return Swaps(state, lineup, hosts, from.Place, to.Place);
                    if (to.Spot == Spot.Host)
                        return Keep(lineup, hosts);
                    return Leaves(state, lineup, hosts, from.Place);
                default:
                    if (to.Spot == Spot.Place)
                        return Takes(state, lineup, hosts, member, to.Place);
                    if (to.Spot == Spot.Host)
                        return Hosts(state, lineup, hosts, member, to.Place);
                    return Keep(lineup, hosts);
            }
        }

        /// <summary>
        /// The talk break's row with nobody on it.
        /// </summary>
        public static int[] NoHosts() => Enumerable.Repeat(-1, Engine.MaxHosts).ToArray();

        /// <summary>
        /// A quick click on a bench idol: she takes the first empty place she can, as on the game's senbatsu
        /// screen. With none, the message says to drag her.
        /// </summary>
        public static DropResult Click(ShowState state, int[] lineup, int[] hosts, int member)
        {
            if (state.Finished || member < 0)
                return Keep(lineup, hosts);
            for (int j = 0; j < lineup.Length; j++)
            {
                if (lineup[j] < 0)
                {
                    DropResult result = Takes(state, lineup, hosts, member, j);
                    if (result.Allowed)
                        return result;
                }
            }
            return Refuse(state, lineup, hosts, "CONCERTHYPE__CLICK_DRAG", member);
        }

        /// <summary>
        /// A quick click on a bench idol on the talk break's tab: she takes the first empty host place. With none,
        /// the message says to drag her.
        /// </summary>
        public static DropResult ClickHost(ShowState state, int[] lineup, int[] hosts, int member)
        {
            if (state.Finished || member < 0 || hosts.Contains(member))
                return Keep(lineup, hosts);
            int empty = Array.IndexOf(hosts, -1);
            if (empty < 0)
                return Refuse(state, lineup, hosts, TalkBlocked(state) ?? "CONCERTHYPE__CLICK_DRAG_HOST", member);
            return Hosts(state, lineup, hosts, member, empty);
        }

        /// <summary>
        /// The text key saying why no talk break can be called now, or null when one can.
        /// </summary>
        public static string TalkBlocked(ShowState state)
        {
            if (state.Finished || state.McCharges <= 0)
                return "CONCERTHYPE__TALK_NONE_LEFT";
            if (state.SongIndex == 0)
                return "CONCERTHYPE__TALK_NOT_YET";
            if (state.McInThisGap)
                return "CONCERTHYPE__TALK_DONE";
            return null;
        }

        private static DropResult Takes(ShowState state, int[] lineup, int[] hosts, int member, int slot)
        {
            if (slot < 0 || slot >= lineup.Length || lineup.Contains(member))
                return Keep(lineup, hosts);
            if (!state.IsAvailable(member))
                return Refuse(state, lineup, hosts, "CONCERTHYPE__CANT_PERFORM", member);
            int[] next = (int[])lineup.Clone();
            next[slot] = member;
            return Checked(state, lineup, hosts, next, member);
        }

        private static DropResult Swaps(ShowState state, int[] lineup, int[] hosts, int from, int to)
        {
            if (from < 0 || from >= lineup.Length || to < 0 || to >= lineup.Length || lineup[from] < 0)
                return Keep(lineup, hosts);
            int[] next = (int[])lineup.Clone();
            next[from] = lineup[to];
            next[to] = lineup[from];
            // Moved onto an empty place, she leaves hers empty: it gets its own idol back, or the best on the bench
            FillEmpties(state, next);
            return Checked(state, lineup, hosts, next, lineup[from]);
        }

        private static DropResult Leaves(ShowState state, int[] lineup, int[] hosts, int slot)
        {
            Song song = state.CurrentSong;
            if (slot < 0 || slot >= lineup.Length || lineup[slot] < 0)
                return Keep(lineup, hosts);
            int member = lineup[slot];
            int original = song.Slots[slot].Original;
            // Only the place's own idol can take it back: she must be free to perform and not on stage elsewhere
            if (member == original || original < 0 || !state.IsAvailable(original) || lineup.Contains(original))
                return Refuse(state, lineup, hosts, "CONCERTHYPE__CANT_LEAVE", member);
            int[] next = (int[])lineup.Clone();
            next[slot] = original;
            return Checked(state, lineup, hosts, next, member);
        }

        private static void FillEmpties(ShowState state, int[] next)
        {
            Song song = state.CurrentSong;
            for (int k = 0; k < next.Length; k++)
            {
                if (next[k] >= 0)
                    continue;
                int original = song.Slots[k].Original;
                next[k] = original >= 0 && state.IsAvailable(original) && !next.Contains(original)
                    ? original
                    : Engine.BestFill(state, k, next.Where(m => m >= 0).ToList());
            }
        }

        /// <summary>
        /// She takes host place k, and whoever was there goes back to the bench; a host moved along the row swaps
        /// with whoever is on the place she's dropped on.
        /// </summary>
        private static DropResult Hosts(ShowState state, int[] lineup, int[] hosts, int member, int place)
        {
            if (place < 0 || place >= hosts.Length)
                return Keep(lineup, hosts);
            string blocked = TalkBlocked(state);
            if (blocked != null)
                return Refuse(state, lineup, hosts, blocked, member);
            if (!state.IsAvailable(member))
                return Refuse(state, lineup, hosts, "CONCERTHYPE__CANT_PERFORM", member);
            int[] next = (int[])hosts.Clone();
            int already = Array.IndexOf(hosts, member);
            if (already >= 0)
                next[already] = hosts[place];
            next[place] = member;
            return new DropResult { Allowed = true, Changed = !next.SequenceEqual(hosts), Lineup = lineup, Hosts = next };
        }

        private static DropResult Unhosts(int[] lineup, int[] hosts, int place)
        {
            if (place < 0 || place >= hosts.Length || hosts[place] < 0)
                return Keep(lineup, hosts);
            int[] next = (int[])hosts.Clone();
            next[place] = -1;
            return new DropResult { Allowed = true, Changed = true, Lineup = lineup, Hosts = next };
        }

        private static DropResult Checked(ShowState state, int[] lineup, int[] hosts, int[] next, int member)
        {
            if (Engine.Validate(state, next) != null)
                return Refuse(state, lineup, hosts, "CONCERTHYPE__NOT_ALLOWED", member);
            return new DropResult { Allowed = true, Changed = !next.SequenceEqual(lineup), Lineup = next, Hosts = hosts };
        }

        private static DropResult Keep(int[] lineup, int[] hosts) => new DropResult { Allowed = true, Lineup = lineup, Hosts = hosts };

        private static DropResult Refuse(ShowState state, int[] lineup, int[] hosts, string key, int member) => new DropResult
        {
            Lineup = lineup,
            Hosts = hosts,
            Message = Texts.Format(key, member >= 0 && member < state.Setup.Members.Count ? state.Setup.Members[member].Name : ""),
        };
    }
}
