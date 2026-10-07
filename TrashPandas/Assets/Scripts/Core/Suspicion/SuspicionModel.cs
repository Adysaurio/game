using System;
using System.Collections.Generic;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Core.Suspicion
{
    public sealed class SuspicionSettings
    {
        /// <summary>Per missing body part, per second, while a human sees the coat.</summary>
        public float WitnessedMissingPartRate = 6f;
        /// <summary>Multiplier for missing parts when nobody is looking (hiding works).</summary>
        public float UnwitnessedFactor = 0.2f;
        /// <summary>Per second at weirdness 1 (collapsed, flailing) while seen.</summary>
        public float WeirdMovementRate = 12f;
        /// <summary>Instant jump when a human spots a loose raccoon.</summary>
        public float RaccoonSightingBurst = 20f;
        /// <summary>Seconds before the same human can count another sighting.</summary>
        public float SightingCooldown = 2f;
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
        float _calmFor;

        public float Value { get; private set; }
        public bool Caught { get; private set; }
        public event Action<bool> CaughtChanged;

        public SuspicionModel(SuspicionSettings settings) => _s = settings;

        public void Tick(float dt, in SuspicionFrame f)
        {
            if (Caught || dt <= 0f) return;
            float missing = CountParts(f.MissingParts) * _s.WitnessedMissingPartRate * (f.CoatWitnessed ? 1f : _s.UnwitnessedFactor);
            float weird = f.CoatWitnessed ? Mathf.Clamp01(f.SeenWeirdness) * _s.WeirdMovementRate : 0f;
            float hiss = f.CatHissing ? _s.CatHissRate : 0f;
            float rise = missing + weird + hiss;

            if (rise > 0f) { _calmFor = 0f; Add(rise * dt); return; }

            float before = _calmFor;
            _calmFor += dt;
            float decaying = Mathf.Max(0f, _calmFor - Mathf.Max(before, _s.CalmDelay));
            if (decaying > 0f) Add(-_s.CalmDecayRate * decaying);
        }

        public bool ReportRaccoonSighting(int witnessId, float now)
        {
            if (Caught) return false;
            if (_lastSighting.TryGetValue(witnessId, out float last) && now - last < _s.SightingCooldown) return false;
            _lastSighting[witnessId] = now;
            _calmFor = 0f;
            Add(_s.RaccoonSightingBurst);
            return true;
        }

        public void Reset()
        {
            Value = 0f;
            _calmFor = 0f;
            _lastSighting.Clear();
            if (Caught) { Caught = false; CaughtChanged?.Invoke(false); }
        }

        void Add(float amount)
        {
            Value = Mathf.Clamp(Value + amount, 0f, _s.Max);
            if (!Caught && Value >= _s.Max)
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
