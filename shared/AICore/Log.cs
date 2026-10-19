using System;
using UnityEngine;

namespace AICore
{
    /// <summary>
    /// The mod's log lines. Tests replace the sink, since Unity's logger needs the engine.
    /// </summary>
    internal static class Log
    {
        internal static Action<string> Sink = message => Debug.LogWarning(message);

        public static void Warn(string message)
        {
            try
            {
                Sink("[" + AIMod.Name + "] " + KeyStore.Redact(message));
            }
            catch
            {
                // Logging must never break the game
            }
        }
    }
}
