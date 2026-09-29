using UnityEngine;

namespace FlyingFishMomentum
{
    // M2 placeholder prompt visual (spec S1): a shrinking XZ-plane ring around
    // the fish that reaches hit radius exactly at the prompt target moment.
    // Self-builds its LineRenderer; M5 replaces this with real UI/VFX.
    [RequireComponent(typeof(LineRenderer))]
    public class TimingPromptRing : MonoBehaviour
    {
        const int Segments = 48;
        const float StartRadius = 6f;
        const float HitRadius = 1.2f;
        const float LineWidth = 0.15f;

        [SerializeField] TimingPromptSpawner _spawner;
        [SerializeField] Transform _target;
        LineRenderer _line;

        public void Configure(TimingPromptSpawner spawner, Transform target)
        {
            _spawner = spawner;
            _target = target;
        }

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.positionCount = Segments + 1;
            _line.useWorldSpace = false;
            _line.startWidth = LineWidth;
            _line.endWidth = LineWidth;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = Color.white;
            _line.endColor = Color.white;
            _line.enabled = false;
        }

        void Update()
        {
            if (_spawner == null || _target == null || _line == null) return;
            var settings = _spawner.Settings;
            if (settings == null) return;
            var prompt = _spawner.Active;
            _line.enabled = prompt.Open;
            if (!prompt.Open) return;
            float progress = 1f - Mathf.Clamp01(
                (prompt.TargetTime - Time.time) / Mathf.Max(settings.LeadTime, 0.001f));
            float r = Mathf.Lerp(StartRadius, HitRadius, progress);
            transform.position = _target.position;
            for (int i = 0; i <= Segments; i++)
            {
                float ang = i * Mathf.PI * 2f / Segments;
                _line.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
            }
        }
    }
}
