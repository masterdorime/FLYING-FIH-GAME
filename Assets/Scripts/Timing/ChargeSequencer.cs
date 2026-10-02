using System.Collections.Generic;
using UnityEngine;

namespace FlyingFishMomentum
{
    public enum ChargeArrow { Up, Down, Left, Right }

    // Pure arrow-sequence logic (ring QTE): tap the shown arrows in order.
    // Explicit inputs, no Unity state: fully unit-testable. Tick returns the
    // speed gain to bank (0 most ticks); the completing tick adds the
    // jackpot when every step was clean.
    public class ChargeSequencer
    {
        public bool IsActive { get; private set; }
        public int StepIndex { get; private set; }
        public int StepCount { get; private set; }
        public ChargeArrow CurrentArrow { get; private set; }
        public bool AllSucceeded { get; private set; } = true;
        public IReadOnlyList<bool?> StepResults => _results;

        ChargeArrow[] _order;
        readonly List<bool?> _results = new List<bool?>();
        float _beginTime;

        public void Begin(ChargeArrow[] order, float startTime)
        {
            _order = order;
            StepCount = order.Length;
            StepIndex = 0;
            CurrentArrow = order[0];
            _beginTime = startTime;
            _results.Clear();
            for (int i = 0; i < order.Length; i++) _results.Add(null);
            AllSucceeded = true;
            IsActive = true;
        }

        public float Tick(float now, ChargeArrow? keyDown, TimingSettings s)
        {
            if (!IsActive || s == null) return 0f;
            if (now - _beginTime > s.ChargeTimeout)
            {
                IsActive = false;
                return 0f;
            }
            if (!keyDown.HasValue) return 0f;
            return Resolve(keyDown.Value == CurrentArrow, s.ChargeStepGain, s);
        }

        float Resolve(bool success, float gain, TimingSettings s)
        {
            // Successful steps bank immediately; failures bank nothing.
            // The jackpot applies only when every step was clean.
            _results[StepIndex] = success;
            if (!success)
            {
                AllSucceeded = false;
                gain = 0f;
            }
            StepIndex++;
            if (StepIndex >= StepCount)
            {
                IsActive = false;
                if (AllSucceeded) gain += s.ChargeJackpot;
            }
            else
            {
                CurrentArrow = _order[StepIndex];
            }
            return gain;
        }

        // Stick/keyboard edge → arrow. Fires once per fresh crossing of the
        // dead zone; held directions and sub-threshold wobble give nothing.
        // Diagonals never guess: one axis must double the other, or the tap
        // is ignored (a wrong guess lights red and kills the jackpot; a
        // missed tap just waits for a clean re-tap).
        public static ChargeArrow? ArrowFromStick(Vector2 prev, Vector2 cur)
        {
            if (cur.magnitude < 0.5f || prev.magnitude >= 0.5f) return null;
            float ax = Mathf.Abs(cur.x);
            float ay = Mathf.Abs(cur.y);
            if (ax > ay * 2f) return cur.x > 0f ? ChargeArrow.Right : ChargeArrow.Left;
            if (ay > ax * 2f) return cur.y > 0f ? ChargeArrow.Up : ChargeArrow.Down;
            return null;
        }

        public static string Glyph(ChargeArrow arrow) => arrow switch
        {
            ChargeArrow.Up => "↑",
            ChargeArrow.Down => "↓",
            ChargeArrow.Left => "←",
            _ => "→",
        };
    }
}
