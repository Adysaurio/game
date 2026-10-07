using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Core.Panic
{
    public enum ChaserState : byte { Idle, FetchWeapon, Chase }

    public struct ChaseTarget
    {
        public int Id;
        public Vector3 Position;
    }

    public struct ChaserOutput
    {
        public ChaserState State;
        public Vector3 Destination;
        public bool Swing;
        public int TargetId;
    }

    /// <summary>
    /// A panicked human: grabs its assigned weapon (if any), chases a loose raccoon and swings when in reach.
    /// Sticks to its target until that raccoon escapes or is caught, then picks the nearest one left.
    /// </summary>
    public sealed class ChaserMind
    {
        public float SwingRange = 1.2f;
        public float SwingCooldown = 1.2f;

        float _cooldown;
        int _target = -1;

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public ChaserOutput Update(float dt, Vector3 self, bool hasWeapon, Vector3? weapon, IReadOnlyList<ChaseTarget> targets)
        {
            _cooldown -= dt;
            if (!hasWeapon && weapon.HasValue)
                return new ChaserOutput { State = ChaserState.FetchWeapon, Destination = weapon.Value, TargetId = -1 };

            ChaseTarget? current = null;
            for (int i = 0; i < targets.Count; i++)
                if (targets[i].Id == _target) { current = targets[i]; break; }
            if (!current.HasValue)
            {
                float best = float.MaxValue;
                for (int i = 0; i < targets.Count; i++)
                {
                    float d = Flat(self, targets[i].Position);
                    if (d < best) { best = d; current = targets[i]; }
                }
            }
            if (!current.HasValue)
            {
                _target = -1;
                return new ChaserOutput { State = ChaserState.Idle, Destination = self, TargetId = -1 };
            }

            _target = current.Value.Id;
            bool swing = Flat(self, current.Value.Position) <= SwingRange && _cooldown <= 0f;
            if (swing) _cooldown = SwingCooldown;
            return new ChaserOutput { State = ChaserState.Chase, Destination = current.Value.Position, Swing = swing, TargetId = _target };
        }
    }
}
