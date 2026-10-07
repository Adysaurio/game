using UnityEngine;

namespace TrashPandas.Runtime.Npc
{
    /// <summary>Patrol or service route (waiter, cat), in world space.</summary>
    public sealed class NpcRoute : MonoBehaviour
    {
        public Vector3[] Points = new Vector3[0];
    }
}
