using System.Collections.Generic;
using UnityEngine;

namespace FlyingFishMomentum
{
    // M2 charge-sequence meter: one line-drawn arrow glyph per step above
    // the fish (same placeholder style as the dial arcs). Grey = waiting,
    // green = nailed, red = missed. Reads spawner state only; the overlay
    // text ("CHARGE 2/4 [↑]") names the current arrow.
    public class ChargeBar : MonoBehaviour
    {
        const float Hover = 3.2f;
        const float Spacing = 1.6f;
        const float GlyphSize = 0.9f;

        public bool Visible { get; private set; }
        public int GreenCount { get; private set; }
        public int RedCount { get; private set; }

        [SerializeField] TimingPromptSpawner _spawner;
        [SerializeField] Transform _target;
        GameObject _visuals;
        readonly List<LineRenderer> _glyphs = new List<LineRenderer>();
        readonly List<Material> _mats = new List<Material>();

        public void Configure(TimingPromptSpawner spawner, Transform target)
        {
            _spawner = spawner;
            _target = target;
        }

        void Awake()
        {
            _visuals = new GameObject("Visuals");
            _visuals.transform.SetParent(transform, false);
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Glyph" + i);
                go.transform.SetParent(_visuals.transform, false);
                go.transform.localPosition = new Vector3((i - 1.5f) * Spacing, 0f, 0f);
                var line = go.AddComponent<LineRenderer>();
                line.positionCount = 5;
                line.useWorldSpace = false;
                line.startWidth = 0.12f;
                line.endWidth = 0.12f;
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = Color.grey;
                mat.renderQueue = 3001;
                line.material = mat;
                _glyphs.Add(line);
                _mats.Add(mat);
            }
            _visuals.SetActive(false);
        }

        void Update()
        {
            if (_spawner == null || _target == null || Camera.main == null)
            {
                SetVisible(false);
                return;
            }
            var charge = _spawner.Charge;
            SetVisible(charge.IsActive);
            if (!charge.IsActive) return;
            transform.position = _target.position + new Vector3(0f, Hover, 0f);
            transform.LookAt(Camera.main.transform);
            var order = _spawner.ChargeOrder;
            GreenCount = 0;
            RedCount = 0;
            var results = charge.StepResults;
            for (int i = 0; i < _glyphs.Count; i++)
            {
                bool? done = i < results.Count ? results[i] : null;
                Color color = !done.HasValue ? Color.grey : done.Value ? Color.green : Color.red;
                _mats[i].color = color;
                _glyphs[i].enabled = i < order.Length;
                if (i < order.Length) DrawArrow(_glyphs[i], order[i]);
                if (done.HasValue && done.Value) GreenCount++;
                if (done.HasValue && !done.Value) RedCount++;
            }
        }

        void SetVisible(bool visible)
        {
            Visible = visible;
            if (_visuals != null) _visuals.SetActive(visible);
        }

        // Shaft + V head in local XY, pointing along the arrow direction.
        static void DrawArrow(LineRenderer line, ChargeArrow arrow)
        {
            if (line == null) return;
            Vector2 dir = arrow switch
            {
                ChargeArrow.Up => new Vector2(0f, 1f),
                ChargeArrow.Down => new Vector2(0f, -1f),
                ChargeArrow.Left => new Vector2(-1f, 0f),
                _ => new Vector2(1f, 0f),
            };
            Vector2 tail = -dir * GlyphSize / 2f;
            Vector2 tip = dir * GlyphSize / 2f;
            Vector2 side = new Vector2(-dir.y, dir.x) * GlyphSize * 0.25f;
            line.SetPosition(0, tail);
            line.SetPosition(1, tip);
            line.SetPosition(2, tip - dir * GlyphSize * 0.3f + side);
            line.SetPosition(3, tip);
            line.SetPosition(4, tip - dir * GlyphSize * 0.3f - side);
        }
    }
}
