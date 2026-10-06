using UnityEngine;

namespace FlyingFishMomentum
{
    // Target pointer: one MMO-style guide arrow floating a little above
    // the fish, always rotated to point at the next objective (rings
    // first, coins fallback). Line-drawn in the ChargeBar glyph style;
    // M5 reskins the visuals.
    public class TargetPointer : MonoBehaviour
    {
        // Screen-space bearing from the fish to the target, in degrees
        // (0 = east, 90 = north). Behind-camera targets mirror across
        // center so the arrow points the way to turn.
        public static float GuideBearingDeg(float fx, float fy, float tx, float ty, bool targetBehind)
        {
            if (targetBehind) { tx = 1f - tx; ty = 1f - ty; }
            return Mathf.Atan2(ty - fy, tx - fx) * Mathf.Rad2Deg;
        }

        const float Hover = 2f;

        static readonly Color RingColor = Color.yellow;
        static readonly Color CoinColor = Color.white;

        [SerializeField] TimingPromptSpawner _spawner;
        [SerializeField] Transform _fish;
        GameObject _arrowHolder;
        LineRenderer _arrow;
        Material _arrowMat;

        public bool ArrowVisible { get; private set; }
        public float BearingDeg { get; private set; }

        public void Configure(TimingPromptSpawner spawner, Transform fish)
        {
            _spawner = spawner;
            _fish = fish;
        }

        void Awake()
        {
            _arrowHolder = new GameObject("GuideArrow");
            _arrowHolder.transform.SetParent(transform, false);
            _arrow = _arrowHolder.AddComponent<LineRenderer>();
            _arrow.positionCount = 4;
            _arrow.useWorldSpace = false;
            _arrow.startWidth = 0.12f;
            _arrow.endWidth = 0.12f;
            _arrowMat = new Material(Shader.Find("Sprites/Default"));
            _arrowMat.renderQueue = 3001;
            _arrow.material = _arrowMat;
            DrawTriangle(_arrow);

            _arrowHolder.SetActive(false);
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
                SetVisible(false);
                return;
            }
            var objective = _spawner.NextObjective(_fish.position);
            if (objective.Target == null)
            {
                SetVisible(false);
                return;
            }
            Vector3 fishSp = cam.WorldToScreenPoint(_fish.position);
            if (fishSp.z < 0f)
            {
                SetVisible(false);
                return;
            }
            Vector3 targetSp = cam.WorldToScreenPoint(objective.Target.position);
            float bearing = GuideBearingDeg(
                fishSp.x / Screen.width, fishSp.y / Screen.height,
                targetSp.x / Screen.width, targetSp.y / Screen.height,
                targetSp.z < 0f);
            BearingDeg = bearing;

            Color color = objective.IsRing ? RingColor : CoinColor;
            float dist = Vector3.Distance(cam.transform.position, objective.Target.position);
            float alpha = Mathf.Clamp(1.2f - dist / 50f, 0.2f, 1f);
            var c = color;
            c.a = alpha;
            _arrowMat.color = c;

            _arrowHolder.transform.position = _fish.position
                + new Vector3(0f, Hover + 0.3f * Mathf.Sin(Time.time * 3f), 0f);
            _arrowHolder.transform.rotation =
                cam.transform.rotation * Quaternion.AngleAxis(bearing, Vector3.forward);
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            ArrowVisible = visible;
            if (_arrowHolder != null) _arrowHolder.SetActive(visible);
        }
    }
}
