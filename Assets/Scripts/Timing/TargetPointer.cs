using UnityEngine;

namespace FlyingFishMomentum
{
    // Target pointer: hybrid in-world chevron + screen-edge arrow guiding
    // the player to the next objective (rings first, coins fallback).
    // Line-drawn in the ChargeBar glyph style; M5 reskins the visuals.
    public class TargetPointer : MonoBehaviour
    {
        // Viewport placement math, pure for tests: projected coords plus
        // whether the target was behind the camera.
        public readonly struct EdgePlacement
        {
            public readonly bool OnScreen;
            public readonly Vector2 Clamped;
            public readonly float BearingDeg;
            public EdgePlacement(bool onScreen, Vector2 clamped, float bearingDeg)
            {
                OnScreen = onScreen;
                Clamped = clamped;
                BearingDeg = bearingDeg;
            }
        }

        // Bearing 0 = east (target to the right). Behind-camera targets
        // mirror across center so the arrow points the way to turn.
        public static EdgePlacement PlaceEdge(float vx, float vy, bool behind, float margin)
        {
            if (behind) { vx = 1f - vx; vy = 1f - vy; }
            bool onScreen = !behind
                && vx >= margin && vx <= 1f - margin
                && vy >= margin && vy <= 1f - margin;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(vx, margin, 1f - margin),
                Mathf.Clamp(vy, margin, 1f - margin));
            float bearing = Mathf.Atan2(vy - 0.5f, vx - 0.5f) * Mathf.Rad2Deg;
            return new EdgePlacement(onScreen, clamped, bearing);
        }

        const float Margin = 0.1f;
        const float ChevronSize = 1.2f;
        const float ChevronHover = 2f;
        const float EdgeDepth = 1f;

        static readonly Color RingColor = Color.yellow;
        static readonly Color CoinColor = Color.white;

        [SerializeField] TimingPromptSpawner _spawner;
        [SerializeField] Transform _fish;
        GameObject _chevronHolder;
        GameObject _edgeHolder;
        LineRenderer _chevron;
        LineRenderer _edge;
        Material _chevronMat;
        Material _edgeMat;

        public bool ChevronVisible { get; private set; }
        public bool EdgeVisible { get; private set; }

        public void Configure(TimingPromptSpawner spawner, Transform fish)
        {
            _spawner = spawner;
            _fish = fish;
        }

        void Awake()
        {
            _chevronHolder = new GameObject("Chevron");
            _chevronHolder.transform.SetParent(transform, false);
            _chevron = _chevronHolder.AddComponent<LineRenderer>();
            _chevron.positionCount = 5;
            _chevron.useWorldSpace = false;
            _chevron.startWidth = 0.12f;
            _chevron.endWidth = 0.12f;
            _chevronMat = new Material(Shader.Find("Sprites/Default"));
            _chevronMat.renderQueue = 3001;
            _chevron.material = _chevronMat;
            DrawDiamond(_chevron, ChevronSize);

            _edgeHolder = new GameObject("EdgeArrow");
            _edgeHolder.transform.SetParent(transform, false);
            _edge = _edgeHolder.AddComponent<LineRenderer>();
            _edge.positionCount = 4;
            _edge.useWorldSpace = false;
            _edge.startWidth = 0.12f;
            _edge.endWidth = 0.12f;
            _edgeMat = new Material(Shader.Find("Sprites/Default"));
            _edgeMat.renderQueue = 3001;
            _edge.material = _edgeMat;
            DrawTriangle(_edge);

            _chevronHolder.SetActive(false);
            _edgeHolder.SetActive(false);
        }

        static void DrawDiamond(LineRenderer line, float s)
        {
            float h = s / 2f;
            line.SetPosition(0, new Vector3(0f, h, 0f));
            line.SetPosition(1, new Vector3(h, 0f, 0f));
            line.SetPosition(2, new Vector3(0f, -h, 0f));
            line.SetPosition(3, new Vector3(-h, 0f, 0f));
            line.SetPosition(4, new Vector3(0f, h, 0f));
        }

        static void DrawTriangle(LineRenderer line)
        {
            line.SetPosition(0, new Vector3(0.9f, 0f, 0f));
            line.SetPosition(1, new Vector3(0.2f, 0.45f, 0f));
            line.SetPosition(2, new Vector3(0.2f, -0.45f, 0f));
            line.SetPosition(3, new Vector3(0.9f, 0f, 0f));
        }

        void Update()
        {
            var cam = Camera.main;
            if (_spawner == null || _fish == null || cam == null)
            {
                SetVisible(false, false);
                return;
            }
            var objective = _spawner.NextObjective(_fish.position);
            if (objective.Target == null)
            {
                SetVisible(false, false);
                return;
            }
            Color color = objective.IsRing ? RingColor : CoinColor;
            _chevronMat.color = color;
            _edgeMat.color = color;

            Vector3 sp = cam.WorldToScreenPoint(objective.Target.position);
            bool behind = sp.z < 0f;
            var placement = PlaceEdge(
                sp.x / Screen.width, sp.y / Screen.height, behind, Margin);
            if (placement.OnScreen)
            {
                float dist = Vector3.Distance(cam.transform.position, objective.Target.position);
                float alpha = Mathf.Clamp(1.2f - dist / 50f, 0.2f, 1f);
                var c = color;
                c.a = alpha;
                _chevronMat.color = c;
                _chevronHolder.transform.position = objective.Target.position
                    + new Vector3(0f, ChevronHover + 0.3f * Mathf.Sin(Time.time * 3f), 0f);
                _chevronHolder.transform.LookAt(cam.transform);
                SetVisible(true, false);
            }
            else
            {
                float depth = cam.nearClipPlane + EdgeDepth;
                _edgeHolder.transform.position = cam.ViewportToWorldPoint(
                    new Vector3(placement.Clamped.x, placement.Clamped.y, depth));
                _edgeHolder.transform.LookAt(cam.transform);
                _edgeHolder.transform.Rotate(0f, 0f, placement.BearingDeg);
                SetVisible(false, true);
            }
        }

        void SetVisible(bool chevron, bool edge)
        {
            ChevronVisible = chevron;
            EdgeVisible = edge;
            if (_chevronHolder != null) _chevronHolder.SetActive(chevron);
            if (_edgeHolder != null) _edgeHolder.SetActive(edge);
        }
    }
}
