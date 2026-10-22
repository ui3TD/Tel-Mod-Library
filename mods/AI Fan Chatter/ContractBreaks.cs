using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AIFanChatter
{
    /// <summary>
    /// The business contracts an answer to an event or scandal ended: the ones running before the game applied
    /// it and gone after. However the game or another mod ends them (damages after a scandal, an event's
    /// terminate effect), they leave the running list, and a contract never runs out during an answer: the
    /// game ends finished ones on its daily tick.
    /// </summary>
    internal static class ContractBreaks
    {
        /// <summary>The agency's running contracts; tests replace it, as they have no game scene.</summary>
        internal static Func<IList<business.active_proposal>> Running = () =>
            Camera.main.GetComponent<mainScript>().Data.GetComponent<business>().ActiveProposals;

        /// <summary>The running contracts now; empty when the game can't say.</summary>
        public static List<business.active_proposal> Snapshot()
        {
            try
            {
                return (Running() ?? new List<business.active_proposal>()).Where(c => c != null).ToList();
            }
            catch (Exception)
            {
                return new List<business.active_proposal>();
            }
        }

        /// <summary>The contracts in the snapshot that aren't running any more.</summary>
        public static List<business.active_proposal> Ended(IList<business.active_proposal> before)
        {
            if (before == null || before.Count == 0)
            {
                return new List<business.active_proposal>();
            }
            List<business.active_proposal> now = Snapshot();
            return before.Where(c => c.Girl != null && !now.Contains(c)).ToList();
        }
    }
}
