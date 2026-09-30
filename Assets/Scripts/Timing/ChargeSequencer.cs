namespace FlyingFishMomentum
{
    public enum ChargeStepKind { Hold, Tap }

    // Pure charge-sequence logic (spec: ring QTE). Explicit clock, no Unity
    // state: fully unit-testable. Tick returns the speed gain to bank
    // (0 most ticks); the completing tick adds the jackpot when clean.
    public class ChargeSequencer
    {
        public bool IsActive { get; private set; }
        public int StepIndex { get; private set; }
        public int StepCount { get; private set; }
        public ChargeStepKind CurrentKind { get; private set; }
        public bool AllSucceeded { get; private set; } = true;
        public float StepStartTime => _stepStart;

        ChargeStepKind[] _order;
        float _stepStart;
        float _holdFor;

        public void Begin(ChargeStepKind[] order, float startTime)
        {
            _order = order;
            StepCount = order.Length;
            StepIndex = 0;
            CurrentKind = order[0];
            _stepStart = startTime;
            _holdFor = 0f;
            AllSucceeded = true;
            IsActive = true;
        }

        public float StepProgress01(float now, TimingSettings s)
        {
            if (!IsActive || s == null) return 0f;
            if (CurrentKind == ChargeStepKind.Hold)
                return _holdFor / UnityEngine.Mathf.Max(s.HoldRequired, 0.001f);
            return TimingDialMath.Progress01(_stepStart + s.TapLead, now, s.TapLead);
        }

        public float Tick(float now, float dt, bool held, bool pressed,
            float speed, TimingSettings s, float minSpeed, float maxSpeed, int streak)
        {
            if (!IsActive || s == null) return 0f;
            if (CurrentKind == ChargeStepKind.Hold)
            {
                if (held)
                {
                    _holdFor += dt;
                    if (_holdFor > s.HoldLimit) return Resolve(false, 0f, now, s);
                    return 0f;
                }
                return Resolve(_holdFor >= s.HoldRequired, s.ChargeStepGain, now, s);
            }
            float target = _stepStart + s.TapLead;
            float good = TimingEvaluator.GoodWindowAt(speed, s, minSpeed, maxSpeed, streak);
            if (pressed)
            {
                var r = TimingEvaluator.Evaluate(now - target, speed, s, minSpeed, maxSpeed, streak);
                return Resolve(r != TimingResult.Miss,
                    r == TimingResult.Perfect ? s.ChargeStepGain : s.ChargeTapGoodGain, now, s);
            }
            if (now > target + good + UnityEngine.Mathf.Min(s.LateBuffer, good))
                return Resolve(false, 0f, now, s);
            return 0f;
        }

        float Resolve(bool success, float gain, float now, TimingSettings s)
        {
            // Successful steps bank immediately; failures bank nothing.
            // The jackpot applies only when every step was clean.
            if (!success)
            {
                AllSucceeded = false;
                gain = 0f;
            }
            StepIndex++;
            _stepStart = now;
            _holdFor = 0f;
            if (StepIndex >= StepCount)
            {
                IsActive = false;
                if (AllSucceeded) gain += s.ChargeJackpot;
            }
            else
            {
                CurrentKind = _order[StepIndex];
            }
            return gain;
        }
    }
}
