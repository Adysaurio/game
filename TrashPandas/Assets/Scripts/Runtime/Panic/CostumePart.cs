using UnityEngine;

namespace TrashPandas.Runtime.Panic
{
    /// <summary>Costume pieces that live elsewhere in the hierarchy (on the head) follow the costume on/off.</summary>
    public sealed class CostumePart : MonoBehaviour
    {
        public GameObject Linked;
        void OnEnable() { if (Linked) Linked.SetActive(true); }
        void OnDisable() { if (Linked) Linked.SetActive(false); }
    }
}
