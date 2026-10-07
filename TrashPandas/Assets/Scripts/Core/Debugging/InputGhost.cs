using System.Collections.Generic;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Core.Debugging
{
    /// <summary>Records one player's slot input and replays it in a loop, so one person can play with themselves.</summary>
    public sealed class InputGhost
    {
        struct Frame
        {
            public float Time;      // seconds since the first recorded frame
            public SlotInput Input;
            public float JumpRel;   // press time relative to the first frame; NegativeInfinity = none
        }

        readonly List<Frame> _frames = new List<Frame>();
        float _recordStart;
        float _playStart;

        public bool HasRecording => _frames.Count > 1;

        /// <summary>Loop length: recorded span plus one frame step, so the last frame gets its share.</summary>
        public float Duration { get; private set; }

        public void Clear()
        {
            _frames.Clear();
            Duration = 0f;
        }

        public void Record(float now, SlotInput input)
        {
            if (_frames.Count == 0) _recordStart = now;
            float jump = input.JumpPressedAt;
            float jumpRel = float.IsInfinity(jump) || float.IsNaN(jump) || jump < _recordStart
                ? float.NegativeInfinity
                : jump - _recordStart;
            _frames.Add(new Frame { Time = now - _recordStart, Input = input, JumpRel = jumpRel });

            int n = _frames.Count;
            float step = n > 1 ? _frames[n - 1].Time - _frames[n - 2].Time : 0f;
            Duration = _frames[n - 1].Time + step;
        }

        public void StartPlayback(float now) => _playStart = now;

        public SlotInput Sample(float now)
        {
            if (!HasRecording || Duration <= 0f) return SlotInput.Idle;

            float elapsed = Mathf.Max(0f, now - _playStart);
            float loops = Mathf.Floor(elapsed / Duration);
            float loopT = elapsed - loops * Duration;

            int index = 0;
            for (int i = 1; i < _frames.Count && _frames[i].Time <= loopT + 1e-5f; i++) index = i;

            var frame = _frames[index];
            var output = frame.Input;
            output.JumpPressedAt = !float.IsNegativeInfinity(frame.JumpRel) && frame.JumpRel <= loopT + 1e-5f
                ? _playStart + loops * Duration + frame.JumpRel
                : float.NegativeInfinity;
            return output;
        }
    }
}
