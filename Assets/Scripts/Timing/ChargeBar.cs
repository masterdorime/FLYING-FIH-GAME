using System.Collections.Generic;
using UnityEngine;

namespace FlyingFishMomentum
{
    // M2 charge-sequence meter: one marker cube per step above the fish.
    // Grey = pending, green = nailed, red = missed. Reads sequencer state
    // only; the overlay text ("CHARGE 2/3 [↑]") names the current arrow.
    public class ChargeBar : MonoBehaviour
    {
        const float Hover = 3.2f;
        const float Spacing = 0.8f;

        public bool Visible { get; private set; }
        public int GreenCount { get; private set; }
        public int RedCount { get; private set; }

        [SerializeField] TimingPromptSpawner _spawner;
        [SerializeField] Transform _target;
        GameObject _visuals;
        readonly List<MeshRenderer> _markers = new List<MeshRenderer>();

        public void Configure(TimingPromptSpawner spawner, Transform target)
        {
            _spawner = spawner;
            _target = target;
        }

        void Awake()
        {
            _visuals = new GameObject("Visuals");
            _visuals.transform.SetParent(transform, false);
            for (int i = 0; i < 3; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "Step" + i;
                marker.transform.SetParent(_visuals.transform, false);
                marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                marker.transform.localPosition = new Vector3((i - 1) * Spacing, 0f, 0f);
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = Color.grey;
                marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
                _markers.Add(marker.GetComponent<MeshRenderer>());
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
            GreenCount = 0;
            RedCount = 0;
            var results = charge.StepResults;
            for (int i = 0; i < _markers.Count; i++)
            {
                bool? done = i < results.Count ? results[i] : null;
                Color color = !done.HasValue ? Color.grey : done.Value ? Color.green : Color.red;
                _markers[i].sharedMaterial.color = color;
                if (done.HasValue && done.Value) GreenCount++;
                if (done.HasValue && !done.Value) RedCount++;
            }
        }

        void SetVisible(bool visible)
        {
            Visible = visible;
            if (_visuals != null) _visuals.SetActive(visible);
        }
    }
}
