namespace TrashPandas.Core.Movement
{
    /// <summary>Coyote time (jump just after leaving a ledge) and jump buffering (press just before landing).</summary>
    public sealed class JumpAssist
    {
        public float CoyoteTime = 0.12f;
        public float BufferTime = 0.15f;

        float _lastGroundedAt = float.NegativeInfinity;
        float _lastPressAt = float.NegativeInfinity;

        public void SetGrounded(bool grounded, float now)
        {
            if (grounded) _lastGroundedAt = now;
        }

        public void Press(float now) => _lastPressAt = now;

        /// <summary>True once per press when both the press and the ground are recent enough.</summary>
        public bool TryConsume(float now)
        {
            if (now - _lastPressAt > BufferTime || now - _lastGroundedAt > CoyoteTime) return false;
            _lastPressAt = float.NegativeInfinity;
            _lastGroundedAt = float.NegativeInfinity;
            return true;
        }
    }
}
