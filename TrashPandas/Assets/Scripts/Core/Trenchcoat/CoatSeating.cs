using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Rules for hopping out of the coat and back in (shared by debug mode and the online host).</summary>
    public static class CoatSeating
    {
        public static bool TryReturn(SlotSystem slots, int playerId, Vector3 raccoon, Vector3 coat, float maxDistance, out int seat)
        {
            seat = -1;
            if (slots.SlotOf(playerId).HasValue) return false;
            Vector2 flat = new Vector2(raccoon.x - coat.x, raccoon.z - coat.z);
            if (flat.magnitude > maxDistance) return false;
            var free = slots.FirstFreeSlot();
            if (!free.HasValue || !slots.TryEnter(playerId, free.Value)) return false;
            seat = free.Value;
            return true;
        }

        public static Vector3 SpawnBeside(Vector3 coat, Quaternion coatRotation) =>
            coat + coatRotation * Vector3.right * 0.9f + Vector3.up * 0.2f;
    }
}
