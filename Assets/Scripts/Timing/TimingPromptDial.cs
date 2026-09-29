using UnityEngine;

namespace FlyingFishMomentum
{
    // M2 prompt meter, Outlast arm-wrestling style: a world-space dial above
    // the fish with green/yellow/red rim zones and a sweeping needle that
    // lands on green at the hit moment. Reads spawner state only — judgment
    // stays in TimingEvaluator. M5 will reskin this.
    public class TimingPromptDial : MonoBehaviour
    {
        const int ArcPoints = 49;
        const float Radius = 2f;
        const float Hover = 3.2f;

        public bool Visible { get; private set; }
        public float NeedleAngleZ { get; private set; }

        [SerializeField] TimingPromptSpawner _spawner;
        [SerializeField] Transform _target;
        [SerializeField] PlayerMomentumController _momentum;
        GameObject _visuals;
        GameObject _pivot;
        LineRenderer _base;
        LineRenderer _good;
        LineRenderer _perfect;

        public void Configure(
            TimingPromptSpawner spawner, Transform target, PlayerMomentumController momentum)
        {
            _spawner = spawner;
            _target = target;
            _momentum = momentum;
        }

        void Awake()
        {
            _visuals = new GameObject("Visuals");
            _visuals.transform.SetParent(transform, false);
            _base = AddArc(new Color(0.4f, 0.4f, 0.4f), 0.12f);
            _good = AddArc(new Color(1f, 0.85f, 0.2f), 0.3f);
            // Perfect paints red and floats closest: coplanar arcs z-fight and
            // yellow swallows green (feel-pass finding).
            _perfect = AddArc(new Color(1f, 0.25f, 0.25f), 0.3f);
            _pivot = new GameObject("NeedlePivot");
            _pivot.transform.SetParent(_visuals.transform, false);
            var needle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            needle.name = "Needle";
            needle.transform.SetParent(_pivot.transform, false);
            needle.transform.localScale = new Vector3(0.14f, Radius, 0.14f);
            needle.transform.localPosition = new Vector3(0f, Radius / 2f, 0f);
            Object.DestroyImmediate(needle.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.white;
            needle.GetComponent<MeshRenderer>().sharedMaterial = mat;
            _visuals.SetActive(false);
        }

        LineRenderer AddArc(Color color, float width)
        {
            var go = new GameObject("Arc");
            go.transform.SetParent(_visuals.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = ArcPoints;
            line.useWorldSpace = false;
            line.startWidth = width;
            line.endWidth = width;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            line.material = mat;
            return line;
        }

        void Update()
        {
            if (_spawner == null || _target == null || _momentum == null ||
                _spawner.Settings == null || Camera.main == null)
            {
                SetVisible(false);
                return;
            }
            var prompt = _spawner.Active;
            SetVisible(prompt.Open);
            if (!prompt.Open) return;
            transform.position = _target.position + new Vector3(0f, Hover, 0f);
            transform.LookAt(Camera.main.transform);
            float lead = _spawner.Settings.LeadTime;
            float progress = _spawner.Progress01;
            float hit = _spawner.HitAngleDeg;
            NeedleAngleZ = TimingDialMath.NeedleAngle(progress, hit);
            _pivot.transform.rotation = Quaternion.Euler(0f, 0f, NeedleAngleZ);
            float speed = _momentum.CurrentSpeed;
            float perfectHalf = TimingDialMath.HalfWidthDeg(
                TimingEvaluator.PerfectWindowAt(speed, _spawner.Settings,
                    _spawner.MomSettings.MinSpeed, _spawner.Settings.MaxSpeedRef), lead);
            float goodHalf = TimingDialMath.HalfWidthDeg(
                TimingEvaluator.GoodWindowAt(speed, _spawner.Settings,
                    _spawner.MomSettings.MinSpeed, _spawner.Settings.MaxSpeedRef), lead);
            DrawArc(_base, -180f, 180f, 0f);
            DrawArc(_good, hit - goodHalf, hit + goodHalf, 0.01f);
            DrawArc(_perfect, hit - perfectHalf, hit + perfectHalf, 0.02f);
        }

        void SetVisible(bool visible)
        {
            Visible = visible;
            if (_visuals != null) _visuals.SetActive(visible);
        }

        static void DrawArc(LineRenderer line, float fromDeg, float toDeg, float z)
        {
            if (line == null) return;
            for (int i = 0; i < ArcPoints; i++)
            {
                float d = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, i / (float)(ArcPoints - 1));
                line.SetPosition(i, new Vector3(Radius * Mathf.Sin(d), Radius * Mathf.Cos(d), z));
            }
        }
    }
}
