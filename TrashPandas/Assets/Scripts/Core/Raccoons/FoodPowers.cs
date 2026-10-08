using UnityEngine;

namespace TrashPandas.Core.Raccoons
{
    /// <summary>Food from the buffet = a silly power for a while (one at a time). Each one opens a kind of lock.</summary>
    public enum FoodPower : byte { None, Beans, Salsa }

    /// <summary>
    /// A raccoon's current power. Beans: one fart-jump per airtime (extra height + a little dash, loud and smelly).
    /// Salsa: a fire burp with a cooldown (scares, dazes, melts ice).
    /// </summary>
    public sealed class PowerState
    {
        public const float Duration = 20f, BurpCooldown = 1.2f;
        FoodPower _power;
        float _until = float.NegativeInfinity, _nextBurp;
        bool _fartUsed;

        public void Eat(FoodPower p, float now)
        {
            _power = p;
            _until = now + Duration;
            _fartUsed = false;
            _nextBurp = now;
        }

        public FoodPower Current(float now) => now < _until ? _power : FoodPower.None;
        public float Remaining01(float now) => Current(now) == FoodPower.None ? 0f : Mathf.Clamp01((_until - now) / Duration);

        public bool TryFartJump(float now, bool grounded)
        {
            if (Current(now) != FoodPower.Beans || grounded || _fartUsed) return false;
            _fartUsed = true;
            return true;
        }

        public void Landed() => _fartUsed = false;

        public bool TryBurp(float now)
        {
            if (Current(now) != FoodPower.Salsa || now < _nextBurp) return false;
            _nextBurp = now + BurpCooldown;
            return true;
        }
    }

    public static class FireBurp
    {
        public const float Range = 3.2f, HalfAngle = 35f;
        public static bool Hits(Vector3 origin, Vector3 forward, Vector3 target)
        {
            Vector3 to = target - origin;
            to.y = 0f;
            forward.y = 0f;
            return to.magnitude <= Range && to.sqrMagnitude > 1e-6f && Vector3.Angle(forward, to) <= HalfAngle;
        }
    }
}
