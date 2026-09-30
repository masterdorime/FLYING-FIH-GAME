using UnityEngine;

namespace FlyingFishMomentum
{
    // M2 charge-sequence meter. HOLD and TAP look deliberately different:
    // HOLD shows a fill bar with a red limit marker; TAP shows a shrinking
    // cyan pulse ball. Reads spawner state only.
    public class ChargeBar : MonoBehaviour
    {
        const float BarLength = 3f;
        const float Hover = 3.2f;

        public bool Visible { get; private set; }
        public string ShownKind { get; private set; } = "Hidden";

        [SerializeField] TimingPromptSpawner _spawner;
        [SerializeField] Transform _target;
        GameObject _visuals;
        GameObject _barGroup;
        GameObject _tapGroup;
        Transform _fill;
        Transform _marker;
        Transform _pulse;
        Material _fillMat;

        public void Configure(TimingPromptSpawner spawner, Transform target)
        {
            _spawner = spawner;
            _target = target;
        }

        // Fill spans the full bar exactly at the hold limit; the marker sits
        // where the required duration lands (release zone begins there).
        public static float FillFraction(float progress01, float required, float limit) =>
            UnityEngine.Mathf.Clamp01(progress01 * required / UnityEngine.Mathf.Max(limit, 0.001f));

        public static float MarkerFraction(float required, float limit) =>
            UnityEngine.Mathf.Clamp01(required / UnityEngine.Mathf.Max(limit, 0.001f));

        void Awake()
        {
            _visuals = new GameObject("Visuals");
            _visuals.transform.SetParent(transform, false);
            _barGroup = new GameObject("BarGroup");
            _barGroup.transform.SetParent(_visuals.transform, false);
            var track = GameObject.CreatePrimitive(PrimitiveType.Cube);
            track.name = "Track";
            track.transform.SetParent(_barGroup.transform, false);
            track.transform.localScale = new Vector3(BarLength + 0.2f, 0.15f, 0.15f);
            Object.DestroyImmediate(track.GetComponent<Collider>());
            track.GetComponent<MeshRenderer>().sharedMaterial = Plain(Color.grey);
            var fillGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fillGo.name = "Fill";
            fillGo.transform.SetParent(_barGroup.transform, false);
            Object.DestroyImmediate(fillGo.GetComponent<Collider>());
            _fillMat = Plain(Color.yellow);
            fillGo.GetComponent<MeshRenderer>().sharedMaterial = _fillMat;
            _fill = fillGo.transform;
            var markerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            markerGo.name = "Limit";
            markerGo.transform.SetParent(_barGroup.transform, false);
            markerGo.transform.localScale = new Vector3(0.12f, 0.4f, 0.12f);
            Object.DestroyImmediate(markerGo.GetComponent<Collider>());
            markerGo.GetComponent<MeshRenderer>().sharedMaterial = Plain(Color.red);
            _marker = markerGo.transform;
            _tapGroup = new GameObject("TapGroup");
            _tapGroup.transform.SetParent(_visuals.transform, false);
            var pulseGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulseGo.name = "Pulse";
            pulseGo.transform.SetParent(_tapGroup.transform, false);
            Object.DestroyImmediate(pulseGo.GetComponent<Collider>());
            pulseGo.GetComponent<MeshRenderer>().sharedMaterial = Plain(Color.cyan);
            _pulse = pulseGo.transform;
            _visuals.SetActive(false);
        }

        static Material Plain(Color color)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            return mat;
        }

        void Update()
        {
            if (_spawner == null || _target == null || _spawner.Settings == null ||
                Camera.main == null)
            {
                SetVisible(false);
                return;
            }
            var charge = _spawner.Charge;
            SetVisible(charge.IsActive);
            if (!charge.IsActive) return;
            transform.position = _target.position + new Vector3(0f, Hover, 0f);
            transform.LookAt(Camera.main.transform);
            var s = _spawner.Settings;
            bool hold = charge.CurrentKind == ChargeStepKind.Hold;
            ShownKind = hold ? "Hold" : "Tap";
            _barGroup.SetActive(hold);
            _tapGroup.SetActive(!hold);
            if (hold)
            {
                float progress = charge.StepProgress01(Time.time, s);
                float frac = FillFraction(progress, s.HoldRequired, s.HoldLimit);
                _fill.localScale = new Vector3(Mathf.Max(BarLength * frac, 0.001f), 0.15f, 0.15f);
                _fill.localPosition = new Vector3(-BarLength / 2f + _fill.localScale.x / 2f, 0f, 0.01f);
                _marker.localPosition = new Vector3(
                    -BarLength / 2f + BarLength * MarkerFraction(s.HoldRequired, s.HoldLimit), 0f, 0.02f);
                _fillMat.color = progress >= 1f ? Color.green : Color.yellow;
            }
            else
            {
                float tapProgress = Mathf.Clamp01(
                    (Time.time - charge.StepStartTime) / Mathf.Max(s.TapLead, 0.001f));
                float r = Mathf.Lerp(2f, 0.2f, tapProgress);
                _pulse.localScale = new Vector3(r, r, r);
            }
        }

        void SetVisible(bool visible)
        {
            Visible = visible;
            if (!visible) ShownKind = "Hidden";
            if (_visuals != null) _visuals.SetActive(visible);
        }
    }
}
