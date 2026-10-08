using TrashPandas.Runtime.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// Concept v2: the round is played with loose raccoons; the trenchcoat only comes out for events.
    /// The coat stays in the scene (events will use it) but is parked out of the way.
    /// </summary>
    public static class GameMode
    {
        public static bool Raccoons = true;

        /// <summary>The manhole the gang climbs out of (next to the den, out of the party's sight).</summary>
        public static Vector3 Manhole = new Vector3(26.5f, 0f, -5.5f);

        /// <summary>Where the n-th raccoon lands after climbing out: a line facing the camera.</summary>
        public static Vector3 SpawnPoint(int n) => Manhole + new Vector3(-1.2f + n * 0.6f, 0.1f, -1.2f);

        public static void ParkCoat(TrenchcoatBody coat)
        {
            if (!coat) return;
            foreach (var r in coat.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in coat.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var rb = coat.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
            coat.transform.position = new Vector3(0f, -50f, 0f);
        }
    }
}
