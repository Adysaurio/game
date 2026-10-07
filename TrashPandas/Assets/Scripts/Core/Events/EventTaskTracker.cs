using UnityEngine;

namespace TrashPandas.Core.Events
{
    /// <summary>One frame of what a player is doing during an event.</summary>
    public struct TaskInput
    {
        public bool Primary;    // left click
        public bool Secondary;  // right click
        public bool Crouch;
        public bool Jump;       // pressed this frame
        public Vector2 Move;
    }

    /// <summary>What a player did during the response window, ready to send to the host.</summary>
    public struct EventResponse
    {
        public HeadAnswer Answer;
        public bool ArmsDone;
        public bool LegsDone;
    }

    /// <summary>Accumulates one player's actions during an event's response window.</summary>
    public sealed class EventTaskTracker
    {
        public const float HoldNeeded = 0.5f;
        public const float StillThreshold = 0.3f;

        int _answerKey;
        float _primaryHeld, _bothHeld;
        bool _moved, _crouchingNow, _jumped;

        public void Reset()
        {
            _answerKey = 0;
            _primaryHeld = _bothHeld = 0f;
            _moved = _crouchingNow = _jumped = false;
        }

        /// <param name="answerKey">1-3 if an answer key was pressed this frame, else 0. Only the first counts.</param>
        public void Record(float dt, TaskInput input, int answerKey)
        {
            if (_answerKey == 0 && answerKey >= 1 && answerKey <= 3) _answerKey = answerKey;
            if (input.Primary) _primaryHeld += dt;
            if (input.Primary && input.Secondary) _bothHeld += dt;
            if (input.Move.magnitude > StillThreshold) _moved = true;
            _crouchingNow = input.Crouch;
            _jumped |= input.Jump;
        }

        public EventResponse ToResponse(SocialEvent e) => new EventResponse
        {
            Answer = _answerKey == 0 ? HeadAnswer.None : e.KindOf(_answerKey - 1),
            ArmsDone = e.Arms switch
            {
                ArmsTask.Handshake or ArmsTask.TakeGlass => _primaryHeld >= HoldNeeded - 1e-4f,
                ArmsTask.HandsTogether => _bothHeld >= HoldNeeded - 1e-4f,
                _ => true,
            },
            LegsDone = e.Legs switch
            {
                LegsTask.StayStill => !_moved,
                LegsTask.Kneel => _crouchingNow,
                LegsTask.DanceStep => _jumped,
                _ => true,
            },
        };
    }
}
