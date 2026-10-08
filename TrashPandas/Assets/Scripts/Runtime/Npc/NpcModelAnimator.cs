using UnityEngine;

namespace TrashPandas.Runtime.Npc
{
    /// <summary>
    /// Plays a rigged human model's clips from what its NpcPawn is doing: idle, walk, run, look around while
    /// searching, and a throw/swing on the wind-up. Works on every machine (mood and movement are replicated).
    /// </summary>
    public sealed class NpcModelAnimator : MonoBehaviour
    {
        public NpcPawn Pawn;
        public float RunAbove = 2.1f;
        Animator _anim;
        string _clip;
        Vector3 _last;
        float _speed;

        void Awake() => _anim = GetComponent<Animator>();
        void OnEnable() { _clip = null; if (Pawn) _last = Pawn.transform.position; }

        void Update()
        {
            if (!_anim || !Pawn) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 d = Pawn.transform.position - _last; d.y = 0f;
            _last = Pawn.transform.position;
            _speed = Mathf.Lerp(_speed, d.magnitude / dt, 0.2f);
            byte mood = Pawn.Mood;
            string clip; float rate = 1f;
            if (mood == NpcPawn.MoodWindup) { clip = "Throw"; rate = 1.6f; }
            else if (_speed > RunAbove) { clip = "Run"; rate = Mathf.Clamp(_speed / 3.5f, 0.8f, 1.6f); }
            else if (_speed > 0.25f) { clip = "Walk"; rate = Mathf.Clamp(_speed / 1.3f, 0.7f, 1.8f); }
            else if (mood == NpcPawn.MoodSearching || mood == 1) clip = "Search";
            else clip = "Idle";
            if (clip != _clip)
            {
                _clip = clip;
                _anim.CrossFadeInFixedTime(clip, clip == "Throw" ? 0.08f : 0.2f);
            }
            _anim.speed = rate;
        }
    }
}
