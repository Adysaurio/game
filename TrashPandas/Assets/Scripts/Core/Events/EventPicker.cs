using System;
using System.Collections.Generic;

namespace TrashPandas.Core.Events
{
    /// <summary>Chooses the next event: first who comes over (never the same person twice in a row), then one of their lines.</summary>
    public static class EventPicker
    {
        /// <returns>Index into <paramref name="catalog"/>, or -1 if none of its speakers are in the scene.</returns>
        public static int Pick(IReadOnlyList<SocialEvent> catalog, ISet<string> availableSpeakers, EventScheduler scheduler, Random random)
        {
            var speakers = new List<string>();
            foreach (var e in catalog)
                if (availableSpeakers.Contains(e.Speaker) && !speakers.Contains(e.Speaker)) speakers.Add(e.Speaker);
            if (speakers.Count == 0) return -1;

            string speaker = speakers[scheduler.PickSpeaker(speakers.Count)];
            var theirs = new List<int>();
            for (int i = 0; i < catalog.Count; i++) if (catalog[i].Speaker == speaker) theirs.Add(i);
            return theirs[random.Next(theirs.Count)];
        }
    }
}
