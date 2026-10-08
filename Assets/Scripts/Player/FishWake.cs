using UnityEngine;

namespace FlyingFishMomentum
{
    // Water package: wake ribbon dressing on the fish (bubble trail
    // underwater, foam wake at the surface — same ribbon). Visual only:
    // never feeds judging, placement, or seeds. Width tapers to zero so
    // the tail always vanishes even without an alpha gradient.
    public class FishWake : MonoBehaviour
    {
        private const int PuffCount = 8;
        private const float PuffLife = 0.6f;

        private TrailRenderer _trail;
        private float _lastY;
        private bool _hasLastY;
        private readonly System.Collections.Generic.List<GameObject> _pool =
            new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.List<Vector3> _vel =
            new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<float> _age =
            new System.Collections.Generic.List<float>();

        public int ActivePuffs
        {
            get
            {
                int n = 0;
                foreach (var p in _pool)
                    if (p != null && p.activeSelf) n++;
                return n;
            }
        }

        void Awake()
        {
            EnsureTrail();
        }

        // Idempotent explicit setup: AddComponent does not synchronously
        // fire Awake in every context (verified headless), so callers and
        // tests build through here instead of assuming it.
        public TrailRenderer EnsureTrail()
        {
            if (_trail != null) return _trail;
            _trail = GetComponent<TrailRenderer>();
            if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 1.2f;
            _trail.startWidth = 1.5f;
            _trail.endWidth = 0f;
            _trail.emitting = true;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 1f, 1f, 0.6f);
            mat.renderQueue = 3000;
            // sharedMaterial: the getter (material) instantiates in edit
            // mode, which the test runner flags as an error.
            _trail.sharedMaterial = mat;
            return _trail;
        }

        void LateUpdate()
        {
            TickSurface();
            TickPuffs(Time.deltaTime);
        }

        void OnDestroy()
        {
            foreach (var p in _pool)
                if (p != null) Object.DestroyImmediate(p);
        }

        // Breach watch: upward surface crossing fires the fixed radial
        // pattern (no RNG — identical every breach). Same y=0 threshold
        // as SurfaceCrossing; visual only, never judging.
        public void TickSurface()
        {
            float y = transform.position.y;
            if (!_hasLastY) { _lastY = y; _hasLastY = true; return; }
            if (_lastY < 0f && y >= 0f) FireSplash(transform.position);
            _lastY = y;
        }

        private void FireSplash(Vector3 at)
        {
            EnsurePool();
            for (int i = 0; i < PuffCount; i++)
            {
                var puff = _pool[i];
                puff.transform.position = at;
                float a = Mathf.Deg2Rad * (45f * i);
                _vel[i] = new Vector3(Mathf.Cos(a) * 3f, 2.5f, Mathf.Sin(a) * 3f);
                _age[i] = 0f;
                puff.SetActive(true);
            }
        }

        public void TickPuffs(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _pool.Count; i++)
            {
                var puff = _pool[i];
                if (puff == null || !puff.activeSelf) continue;
                _age[i] += dt;
                if (_age[i] >= PuffLife) { puff.SetActive(false); continue; }
                var v = _vel[i];
                v.y -= 9.8f * dt;
                _vel[i] = v;
                puff.transform.position += v * dt;
                float t = _age[i] / PuffLife;
                puff.transform.localScale = Vector3.one * (0.8f + 2.2f * t);
                var rend = puff.GetComponent<MeshRenderer>();
                // Per-puff material instance (built so in EnsurePool):
                // fading sharedMaterial here touches only this puff.
                if (rend != null && rend.sharedMaterial != null)
                {
                    var c = rend.sharedMaterial.color;
                    c.a = 0.9f * (1f - t);
                    rend.sharedMaterial.color = c;
                }
                if (Camera.main != null) puff.transform.LookAt(Camera.main.transform);
            }
        }

        private void EnsurePool()
        {
            while (_pool.Count < PuffCount)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Quad);
                puff.name = "SplashPuff";
                Object.DestroyImmediate(puff.GetComponent<MeshCollider>());
                var mat = new Material(Shader.Find("Sprites/Default"));
                var sprite = Resources.Load<Texture2D>("WaterPuff");
                if (sprite != null) mat.mainTexture = sprite;
                mat.color = new Color(1f, 1f, 1f, 0.9f);
                mat.renderQueue = 3000;
                puff.GetComponent<MeshRenderer>().sharedMaterial = mat;
                puff.transform.localScale = Vector3.one * 0.8f;
                puff.SetActive(false);
                _pool.Add(puff);
                _vel.Add(Vector3.zero);
                _age.Add(PuffLife);
            }
        }
    }
}
