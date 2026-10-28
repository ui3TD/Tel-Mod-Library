using ConcertHype.Model;
using System;
using System.Linq;

namespace ConcertHype
{
    public enum Spot
    {
        /// <summary>
        /// The bench column. As where a drag ends: anywhere that isn't a place or the host place.
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
        /// The place's index in the song's lineup; -1 for the bench and the host place.
        /// </summary>
        public int Place;

        public static DropPoint Bench => new DropPoint { Spot = Spot.Bench, Place = -1 };

        public static DropPoint Host => new DropPoint { Spot = Spot.Host, Place = -1 };

        public static DropPoint AtPlace(int place) => new DropPoint { Spot = Spot.Place, Place = place };

        public bool Equals(DropPoint other) => Spot == other.Spot && Place == other.Place;

        public override bool Equals(object obj) => obj is DropPoint other && Equals(other);

        public override int GetHashCode() => (int)Spot * 31 + Place;

        public override string ToString() => Spot == Spot.Place ? "place " + Place : Spot.ToString();
    }

    /// <summary>
    /// What a drop does: the lineup and talk break host after it, or why it's refused.
    /// </summary>
    public sealed class DropResult
    {
        public bool Allowed;
        public bool Changed;
        public int[] Lineup;
        public int Host = -1;

        /// <summary>
        /// Why the drop is refused, in one sentence; empty when it's allowed.
        /// </summary>
        public string Message = "";
    }

    /// <summary>
    /// The drag-and-drop rules of the lineup screen. A bench idol dropped on a place takes it, and its previous
    /// idol returns to the bench; changing a place whose original can perform is the song's one change. A
    /// substitute dragged off her place brings the original back. Places can't be swapped. Any idol who can
    /// perform can host the talk break, performers included.
    /// </summary>
    public static class DropRules
    {
        public static DropResult Drop(ShowState state, int[] lineup, int host, int member, DropPoint from, DropPoint to)
        {
            if (state.Finished || member < 0 || from.Equals(to))
                return Keep(lineup, host);
            switch (from.Spot)
            {
                case Spot.Host:
                    // Dragged off the host place, wherever she lands: the talk break is called off
                    return new DropResult { Allowed = true, Changed = host >= 0, Lineup = lineup, Host = -1 };
                case Spot.Place:
                    if (to.Spot == Spot.Place)
                        return Refuse(state, lineup, host, "CONCERTHYPE__NO_SWAP", member);
                    if (to.Spot == Spot.Host)
                        return Hosts(state, lineup, host, member);
                    return Leaves(state, lineup, host, from.Place);
                default:
                    if (to.Spot == Spot.Place)
                        return Takes(state, lineup, host, member, to.Place);
                    if (to.Spot == Spot.Host)
                        return Hosts(state, lineup, host, member);
                    return Keep(lineup, host);
            }
        }

        /// <summary>
        /// A quick click on a bench idol: she takes the first empty place she can, as on the game's senbatsu
        /// screen. With none, the message says to drag her.
        /// </summary>
        public static DropResult Click(ShowState state, int[] lineup, int host, int member)
        {
            if (state.Finished || member < 0)
                return Keep(lineup, host);
            for (int j = 0; j < lineup.Length; j++)
            {
                if (lineup[j] < 0)
                {
                    DropResult result = Takes(state, lineup, host, member, j);
                    if (result.Allowed)
                        return result;
                }
            }
            return Refuse(state, lineup, host, "CONCERTHYPE__CLICK_DRAG", member);
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

        private static DropResult Takes(ShowState state, int[] lineup, int host, int member, int slot)
        {
            Song song = state.CurrentSong;
            if (slot < 0 || slot >= lineup.Length || lineup.Contains(member))
                return Keep(lineup, host);
            if (!state.IsAvailable(member))
                return Refuse(state, lineup, host, "CONCERTHYPE__CANT_PERFORM", member);
            if (song.Slots.Where((s, k) => k != slot && s.Original == member).Any())
                return Refuse(state, lineup, host, "CONCERTHYPE__OWN_PLACE", member);

            bool[] forced = Engine.ForcedSlots(state);
            int[] next = (int[])lineup.Clone();
            next[slot] = member;
            // A change on a place whose original can perform is the song's one change: an earlier one is put back
            if (!forced[slot] && member != song.Slots[slot].Original)
            {
                for (int k = 0; k < next.Length; k++)
                {
                    if (k != slot && !forced[k] && next[k] != song.Slots[k].Original)
                        next[k] = song.Slots[k].Original;
                }
            }
            return Checked(state, lineup, host, next, member);
        }

        private static DropResult Leaves(ShowState state, int[] lineup, int host, int slot)
        {
            Song song = state.CurrentSong;
            if (slot < 0 || slot >= lineup.Length || lineup[slot] < 0)
                return Keep(lineup, host);
            int member = lineup[slot];
            if (member == song.Slots[slot].Original || Engine.IsForced(state, song, slot))
                return Refuse(state, lineup, host, "CONCERTHYPE__CANT_LEAVE", member);
            int[] next = (int[])lineup.Clone();
            next[slot] = song.Slots[slot].Original;
            return Checked(state, lineup, host, next, member);
        }

        private static DropResult Hosts(ShowState state, int[] lineup, int host, int member)
        {
            string blocked = TalkBlocked(state);
            if (blocked != null)
                return Refuse(state, lineup, host, blocked, member);
            if (!state.IsAvailable(member))
                return Refuse(state, lineup, host, "CONCERTHYPE__CANT_PERFORM", member);
            return new DropResult { Allowed = true, Changed = host != member, Lineup = lineup, Host = member };
        }

        private static DropResult Checked(ShowState state, int[] lineup, int host, int[] next, int member)
        {
            if (Engine.Validate(state, next) != null)
                return Refuse(state, lineup, host, "CONCERTHYPE__NOT_ALLOWED", member);
            return new DropResult { Allowed = true, Changed = !next.SequenceEqual(lineup), Lineup = next, Host = host };
        }

        private static DropResult Keep(int[] lineup, int host) => new DropResult { Allowed = true, Lineup = lineup, Host = host };

        private static DropResult Refuse(ShowState state, int[] lineup, int host, string key, int member) => new DropResult
        {
            Lineup = lineup,
            Host = host,
            Message = Texts.Format(key, member >= 0 && member < state.Setup.Members.Count ? state.Setup.Members[member].Name : ""),
        };
    }
}
