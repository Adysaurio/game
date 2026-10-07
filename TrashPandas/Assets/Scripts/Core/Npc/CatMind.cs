using UnityEngine;

namespace TrashPandas.Core.Npc
{
    public enum CatState : byte { Patrol, Sniffing, Hissing, Cooldown }

    public sealed class CatSettings
    {
        public float SniffRange = 3.5f;
        public float GiveUpRange = 4.5f;
        public float HissRange = 1.5f;
        public float HissDuration = 2.5f;
        public float CooldownTime = 4f;
        public float ArriveDistance = 0.5f;
        public float StuckTime = 3f;
        public float StuckDistance = 0.2f;
    }

    /// <summary>
    /// The wedding cat: patrols, smells the raccoons inside the coat, walks up and hisses (which raises
    /// suspicion), then wanders off for a bit. Unsticks itself if it can't make progress.
    /// </summary>
    public sealed class CatMind
    {
        readonly Vector3[] _route;
        readonly CatSettings _s;
        int _next;
        float _stateTime;
        float _stuckClock;
        Vector3 _stuckFrom;
        bool _stuckArmed;

        public CatState State { get; private set; } = CatState.Patrol;
        public Vector3 Destination { get; private set; }
        public bool IsHissing => State == CatState.Hissing;

        public CatMind(Vector3[] route, CatSettings settings = null)
        {
            _route = route;
            _s = settings ?? new CatSettings();
            Destination = route.Length > 0 ? route[0] : Vector3.zero;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public CatState Update(float dt, Vector3 cat, Vector3 coat)
        {
            _stateTime += dt;
            float toCoat = Flat(cat, coat);

            switch (State)
            {
                case CatState.Patrol:
                    if (toCoat <= _s.SniffRange) { Enter(CatState.Sniffing); Destination = coat; break; }
                    Patrol(dt, cat);
                    break;
                case CatState.Sniffing:
                    Destination = coat;
                    if (toCoat <= _s.HissRange) { Enter(CatState.Hissing); Destination = cat; }
                    else if (toCoat > _s.GiveUpRange) { Enter(CatState.Patrol); Destination = _route[_next]; }
                    break;
                case CatState.Hissing:
                    Destination = cat;
                    if (_stateTime >= _s.HissDuration) { Enter(CatState.Cooldown); Advance(); }
                    break;
                case CatState.Cooldown:
                    Patrol(dt, cat);
                    if (_stateTime >= _s.CooldownTime)
                    {
                        Enter(CatState.Patrol);
                        if (toCoat <= _s.SniffRange) { Enter(CatState.Sniffing); Destination = coat; }
                    }
                    break;
            }
            return State;
        }

        void Patrol(float dt, Vector3 cat)
        {
            if (_route.Length == 0) return;
            if (Flat(cat, _route[_next]) <= _s.ArriveDistance) { Advance(); return; }
            Destination = _route[_next];

            if (!_stuckArmed) { _stuckFrom = cat; _stuckClock = 0f; _stuckArmed = true; return; }
            _stuckClock += dt;
            if (_stuckClock >= _s.StuckTime)
            {
                if (Flat(cat, _stuckFrom) < _s.StuckDistance) Advance();
                _stuckFrom = cat;
                _stuckClock = 0f;
            }
        }

        void Advance()
        {
            if (_route.Length == 0) return;
            _next = (_next + 1) % _route.Length;
            Destination = _route[_next];
            _stuckArmed = false;
        }

        void Enter(CatState s)
        {
            State = s;
            _stateTime = 0f;
            _stuckArmed = false;
        }
    }
}
