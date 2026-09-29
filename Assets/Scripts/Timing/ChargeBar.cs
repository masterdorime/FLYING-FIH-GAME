using UnityEngine;

namespace FlyingFishMomentum
{
    // M2 charge-sequence meter: a fill bar above the fish. HOLD fills toward
    // a limit marker (yellow filling, green release zone, fizzle past the
    // marker end); TAP shows countdown fill. Reads spawner state only.
    public class ChargeBar : MonoBehaviour
    {
        const float BarLength = 3f;
        const float Hover = 3.2f;

        public bool Visible { get; private set; }

        [SerializeField] TimingPromptSpawner _spawner;
        [SerializeField] Transform _target;
        GameObject _visuals;
        Transform _fill;
        Transform _marker;
        Material _fillMat;

        public void Configure(TimingPromptSpawner spawner, Transform target)
        {
            _spawner = spawner;
            _target = target;
        }

        void Awake()
        {
            _visuals = new GameObject("Visuals");
            _visuals.transform.SetParent(transform, false);
            var track = GameObject.CreatePrimitive(PrimitiveType.Cube);
            track.name = "Track";
            track.transform.SetParent(_visuals.transform, false);
            track.transform.localScale = new Vector3(BarLength + 0.2f, 0.15f, 0.15f);
            Object.DestroyImmediate(track.GetComponent<Collider>());
            track.GetComponent<MeshRenderer>().sharedMaterial = Plain(Color.grey);
            var fillGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fillGo.name = "Fill";
            fillGo.transform.SetParent(_visuals.transform, false);
            Object.DestroyImmediate(fillGo.GetComponent<Collider>());
            _fillMat = Plain(Color.yellow);
            fillGo.GetComponent<MeshRenderer>().sharedMaterial = _fillMat;
            _fill = fillGo.transform;
            var markerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            markerGo.name = "Limit";
            markerGo.transform.SetParent(_visuals.transform, false);
            markerGo.transform.localScale = new Vector3(0.12f, 0.4f, 0.12f);
            Object.DestroyImmediate(markerGo.GetComponent<Collider>());
            markerGo.GetComponent<MeshRenderer>().sharedMaterial = Plain(Color.red);
            _marker = markerGo.transform;
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
            float progress = charge.StepProgress01(Time.time, s);
            bool hold = charge.CurrentKind == ChargeStepKind.Hold;
            float shown = hold ? Mathf.Min(progress, s.HoldLimit / Mathf.Max(s.HoldRequired, 0.001f)) : progress;
            _fill.localScale = new Vector3(Mathf.Max(BarLength * Mathf.Clamp01(shown), 0.001f), 0.15f, 0.15f);
            _fill.localPosition = new Vector3(-BarLength / 2f + _fill.localScale.x / 2f, 0f, 0.01f);
            _marker.gameObject.SetActive(hold);
            _marker.localPosition = new Vector3(-BarLength / 2f + BarLength / (s.HoldLimit / Mathf.Max(s.HoldRequired, 0.001f)), 0f, 0.02f);
            _fillMat.color = !hold ? Color.cyan : progress >= 1f ? Color.green : Color.yellow;
        }

        void SetVisible(bool visible)
        {
            Visible = visible;
            if (_visuals != null) _visuals.SetActive(visible);
        }
    }
}
