using System;
using System.Collections.Generic;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Core.Suspicion
{
    public sealed class SuspicionSettings
    {
        /// <summary>Per missing body part, per second, while a human sees the coat.</summary>
        public float WitnessedMissingPartRate = 4f;
        /// <summary>Multiplier for missing parts when nobody is looking (hiding works).</summary>
        public float UnwitnessedFactor = 0.2f;
        /// <summary>Per second at weirdness 1 (collapsed, flailing) while seen.</summary>
        public float WeirdMovementRate = 8f;
        /// <summary>Weirdness below this is noise (rounding while walking), not something odd.</summary>
        public float WeirdnessFloor = 0.05f;
        /// <summary>Instant jump when a human spots a loose raccoon.</summary>
        public float RaccoonSightingBurst = 15f;
        /// <summary>Seconds before the same human can count another sighting.</summary>
        public float SightingCooldown = 2f;
        /// <summary>Sightings by other humans within this many seconds of the first one count less…</summary>
        public float SightingWindow = 1f;
        /// <summary>…this fraction of the burst each (a crowd noticing at once is fast, not instant game over).</summary>
        public float ExtraWitnessFactor = 0.25f;
        public float CatHissRate = 15f;
        public float CalmDecayRate = 4f;
        /// <summary>Seconds without anything suspicious before it starts going down.</summary>
        public float CalmDelay = 2f;
        public float Max = 100f;
    }

    /// <summary>What the humans perceived this frame.</summary>
    public struct SuspicionFrame
    {
        public BodyPart MissingParts;
        public bool CoatWitnessed;
        /// <summary>0..1, how odd the coat looks to whoever sees it.</summary>
        public float SeenWeirdness;
        public bool CatHissing;
    }

    /// <summary>The team's shared suspicion meter (spec §5). At Max the team is caught: "RUN!".</summary>
    public sealed class SuspicionModel
    {
        readonly SuspicionSettings _s;
        readonly Dictionary<int, float> _lastSighting = new Dictionary<int, float>();
        // Doubles: at very high frame rates a per-frame change is below float resolution near 30-100.
        double _value;
        double _calmFor;
        float _windowStart = float.NegativeInfinity;

        public float Value => (float)_value;
        public bool Caught { get; private set; }
        public event Action<bool> CaughtChanged;

        public SuspicionModel(SuspicionSettings settings) => _s = settings;

        public void Tick(float dt, in SuspicionFrame f)
        {
            if (Caught || dt <= 0f) return;
            float missing = CountParts(f.MissingParts) * _s.WitnessedMissingPartRate * (f.CoatWitnessed ? 1f : _s.UnwitnessedFactor);
            float weird = f.CoatWitnessed && f.SeenWeirdness >= _s.WeirdnessFloor ? Mathf.Clamp01(f.SeenWeirdness) * _s.WeirdMovementRate : 0f;
            float hiss = f.CatHissing ? _s.CatHissRate : 0f;
            float rise = missing + weird + hiss;

            if (rise > 0f) { _calmFor = 0.0; Add((double)rise * dt); return; }

            double before = _calmFor;
            _calmFor += dt;
            double decaying = _calmFor - System.Math.Max(before, _s.CalmDelay);
            if (decaying > 0.0) Add(-_s.CalmDecayRate * decaying);
        }

        public bool ReportRaccoonSighting(int witnessId, float now)
        {
            if (Caught) return false;
            if (_lastSighting.TryGetValue(witnessId, out float last) && now - last < _s.SightingCooldown) return false;
            _lastSighting[witnessId] = now;
            _calmFor = 0.0;
            bool sameMoment = now - _windowStart <= _s.SightingWindow;
            if (!sameMoment) _windowStart = now;
            Add(_s.RaccoonSightingBurst * (sameMoment ? _s.ExtraWitnessFactor : 1f));
            return true;
        }

        /// <summary>A one-off change (social event result). Raising also restarts the calm delay.</summary>
        public void Adjust(float delta)
        {
            if (Caught) return;
            if (delta > 0f) _calmFor = 0.0;
            Add(delta);
        }

        public void Reset()
        {
            _value = 0.0;
            _calmFor = 0.0;
            _lastSighting.Clear();
            _windowStart = float.NegativeInfinity;
            if (Caught) { Caught = false; CaughtChanged?.Invoke(false); }
        }

        void Add(double amount)
        {
            _value = System.Math.Min(System.Math.Max(_value + amount, 0.0), _s.Max);
            if (!Caught && _value >= _s.Max)
            {
                Caught = true;
                CaughtChanged?.Invoke(true);
            }
        }

        static int CountParts(BodyPart parts)
        {
            int n = 0;
            for (int bits = (int)parts; bits != 0; bits &= bits - 1) n++;
            return n;
        }
    }
}
