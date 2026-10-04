using System.Collections.Generic;
using UnityEngine;
using FlyingFishMomentum.Scoring;

namespace FlyingFishMomentum.Run
{
    // M4 endless run: turns a ChunkSpec + seed into placed content
    // (rings/coins/islands) under pooled per-chunk roots, and recycles
    // the long water/seabed segments. Visuals mirror M1SceneBuilder
    // (LineRenderer rings, gold-sphere CoinPickups, scaled-cube islands).
    // Seeded RNG only: one dedicated System.Random per BuildChunk call,
    // never shared with beat/charge streams. Spawner wiring lands in
    // Task 7; this class only builds/reclaims content and exposes the
    // live lists.
    public class ChunkBuilder : MonoBehaviour
    {
        // PRD §12.4 safety pins (rule floors, not tuning): a beat gap fits
        // between rings at band speed; slalom gates stay steerable at top
        // speed (M1 20m precedent); prompts never sit inside rock volumes.
        public const float MinRingSpacing = 100f;
        public const float GateHalfWidth = 10f;
        public const float LaneX = 15f;
        public const float SwimY = -3f;
        public const float FlyY = 10f;
        public const float CoinSpacing = 15f;
        public const float IslandHalfX = 5f;
        public const float IslandHalfDepth = 5f;
        public const float IslandCenterY = 5f;

        private readonly List<ChargeRing> _rings = new List<ChargeRing>();
        private readonly List<CoinPickup> _coins = new List<CoinPickup>();
        private readonly List<GameObject> _islands = new List<GameObject>();
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly Queue<GameObject> _ringPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _coinPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _islandPool = new Queue<GameObject>();
        [SerializeField] private List<GameObject> _waterSegs = new List<GameObject>();
        [SerializeField] private List<GameObject> _seabedSegs = new List<GameObject>();
        private List<float> _waterHalf = new List<float>();
        private List<float> _seabedHalf = new List<float>();
        private Material _ringMat;
        private Material _coinMat;
        private Material _rockMat;
        private Material _waterMat;
        private Material _skyMat;
        private string _appliedMoodId;

        public IReadOnlyList<ChargeRing> Rings => _rings;
        public IReadOnlyList<CoinPickup> Coins => _coins;
        public IReadOnlyList<GameObject> Islands => _islands;

        // M4 Task 7 (Task 6 review): the sky tint rides a real renderer,
        // not a dangling material — the scene passes its sky shell here
        // once at wiring, mirroring the water-segment pattern below.
        // Serialized so build-time wiring survives save/load (a prior
        // revision kept these transient: the rebuilt scene loaded with
        // empty segment lists and static water past 4000m).
        [SerializeField] private Renderer _skyRenderer;
        public Transform SkyAnchor => _skyRenderer != null ? _skyRenderer.transform : null;

        public float FurthestContentZ
        {
            get
            {
                float furthest = float.NegativeInfinity;
                foreach (var r in _rings)
                    if (r != null) furthest = Mathf.Max(furthest, r.transform.position.z);
                foreach (var c in _coins)
                    if (c != null) furthest = Mathf.Max(furthest, c.transform.position.z);
                foreach (var i in _islands)
                    if (i != null) furthest = Mathf.Max(furthest, i.transform.position.z + IslandHalfDepth);
                return furthest;
            }
        }

        // Water/seabed segment refs for recycling (passed once at wiring).
        // Stores refs only: shared mood instances are assigned in Start
        // (runtime), never at build — build-time assignment of unsaved
        // instances does not survive save/load.
        public void Configure(List<GameObject> waterSegs, List<GameObject> seabedSegs, Renderer skyRenderer = null)
        {
            _waterSegs = waterSegs ?? new List<GameObject>();
            _seabedSegs = seabedSegs ?? new List<GameObject>();
            _skyRenderer = skyRenderer;
            _waterHalf = Halves(_waterSegs);
            _seabedHalf = Halves(_seabedSegs);
        }

        void Start()
        {
            // Re-derive recycle math from the persisted refs and point
            // the scene renderers at the shared mood instances (Task 6
            // tints these per chunk type; order vs RunManager.Start is
            // irrelevant — both sides hold the same instances).
            _waterHalf = Halves(_waterSegs);
            _seabedHalf = Halves(_seabedSegs);
            if (_skyRenderer != null) _skyRenderer.sharedMaterial = SkyMaterial;
            foreach (var s in _waterSegs)
            {
                if (s == null) continue;
                var rend = s.GetComponent<MeshRenderer>();
                if (rend != null) rend.sharedMaterial = WaterMaterial;
            }
        }

        // M4 Task 6 mood: per-type sky/fog/water/light on chunk entry.
        // Instant switch (smoothing deferred); skips re-apply for the same
        // type. Visual only: no spawn/reclaim/recycle or judging changes.
        public void ApplyMood(ChunkSpec spec)
        {
            if (spec == null || spec.ChunkId == _appliedMoodId) return;
            _appliedMoodId = spec.ChunkId;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = spec.FogColor;
            RenderSettings.fogDensity = spec.FogDensity;
            RenderSettings.ambientLight = spec.SkyTint;
            SkyMaterial.color = spec.SkyTint;
            WaterMaterial.color = spec.WaterTint;
        }

        public void BuildChunk(ChunkSpec spec, float zStart, int seed)
        {
            if (spec == null) return;
            ApplyMood(spec);
            var rng = new System.Random(seed);
            var root = new GameObject(string.Format("Chunk_{0}_{1}_{2}", spec.ChunkId, zStart, seed));
            root.transform.SetParent(transform, false);
            _roots.Add(root);

            float spacing = Mathf.Max(spec.RingSpacing, MinRingSpacing);
            int swimRings = Mathf.RoundToInt(spec.RingCount * Mathf.Clamp01(spec.RingSwimFraction));
            for (int k = 0; k < spec.RingCount; k++)
            {
                var ring = TakeRing();
                ring.transform.SetParent(root.transform, false);
                ring.transform.position = new Vector3(
                    Mathf.Lerp(-LaneX, LaneX, (float)rng.NextDouble()),
                    k < swimRings ? SwimY : FlyY,
                    zStart + (k + 1) * spacing);
                _rings.Add(ring);
            }

            int swimTrails = Mathf.RoundToInt(spec.CoinTrails * Mathf.Clamp01(spec.RingSwimFraction));
            for (int t = 0; t < spec.CoinTrails; t++)
            {
                float baseZ = zStart + spec.Length * (t + 1f) / (spec.CoinTrails + 1f);
                float x = Mathf.Lerp(-LaneX, LaneX, (float)rng.NextDouble());
                float y = t < swimTrails ? SwimY : FlyY;
                for (int i = 0; i < spec.CoinsPerTrail; i++)
                {
                    var coin = TakeCoin();
                    coin.transform.SetParent(root.transform, false);
                    coin.transform.position = new Vector3(
                        x, y, baseZ + (i - (spec.CoinsPerTrail - 1f) / 2f) * CoinSpacing);
                    _coins.Add(coin);
                }
            }

            float cx = GateHalfWidth + IslandHalfX;
            for (int i = 0; i < spec.IslandPairs; i++)
            {
                float z = zStart + spec.Length * (i + 1f) / (spec.IslandPairs + 1f)
                    + ((float)rng.NextDouble() - 0.5f) * 20f;
                _islands.Add(PlaceIsland(root, new Vector3(-cx, IslandCenterY, z)));
                _islands.Add(PlaceIsland(root, new Vector3(cx, IslandCenterY, z)));
            }

            // Fresh transforms leave collider bounds stale until the next
            // physics step (ledger precedent): sync so the clearance pass
            // below reads true island volumes in EditMode and on frame one.
            Physics.SyncTransforms();
            ResolvePromptClearance();
        }

        // Removes content fully past the line back into the pools.
        public void ReclaimBefore(float z)
        {
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                if (_rings[i] == null || _rings[i].transform.position.z < z)
                {
                    Stage(_rings[i] != null ? _rings[i].gameObject : null, _ringPool);
                    _rings.RemoveAt(i);
                }
            }
            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                if (_coins[i] == null || _coins[i].transform.position.z < z)
                {
                    Stage(_coins[i] != null ? _coins[i].gameObject : null, _coinPool);
                    _coins.RemoveAt(i);
                }
            }
            for (int i = _islands.Count - 1; i >= 0; i--)
            {
                if (_islands[i] == null || _islands[i].transform.position.z + IslandHalfDepth < z)
                {
                    Stage(_islands[i], _islandPool);
                    _islands.RemoveAt(i);
                }
            }
            for (int i = _roots.Count - 1; i >= 0; i--)
            {
                var root = _roots[i];
                if (root == null) { _roots.RemoveAt(i); continue; }
                if (root.transform.childCount == 0)
                {
                    _roots.RemoveAt(i);
                    if (Application.isPlaying) Object.Destroy(root);
                    else Object.DestroyImmediate(root);
                }
            }
        }

        // Repositions any water/seabed segment fully behind the fish to
        // ahead; segments stay contiguous (same length steps as built).
        public void RecycleSegments(float fishZ)
        {
            RecycleList(_waterSegs, _waterHalf, fishZ);
            RecycleList(_seabedSegs, _seabedHalf, fishZ);
        }

        private static void RecycleList(List<GameObject> segs, List<float> halves, float fishZ)
        {
            if (segs == null || segs.Count == 0) return;
            float end = float.NegativeInfinity;
            for (int i = 0; i < segs.Count; i++)
            {
                if (segs[i] == null) continue;
                end = Mathf.Max(end, segs[i].transform.position.z + halves[i]);
            }
            for (int i = 0; i < segs.Count; i++)
            {
                if (segs[i] == null) continue;
                if (segs[i].transform.position.z + halves[i] < fishZ)
                {
                    var p = segs[i].transform.position;
                    p.z = end + halves[i];
                    segs[i].transform.position = p;
                    end = p.z + halves[i];
                }
            }
        }

        // Prompts never sit inside island volumes: clamp the x of any
        // prompt caught inside a rock back into the open gate.
        private void ResolvePromptClearance()
        {
            foreach (var ring in _rings) ClearPrompt(ring.transform);
            foreach (var coin in _coins) ClearPrompt(coin.transform);
        }

        private void ClearPrompt(Transform t)
        {
            if (t == null) return;
            Vector3 p = t.position;
            foreach (var island in _islands)
            {
                if (island == null) continue;
                var col = island.GetComponent<Collider>();
                if (col == null) continue;
                Bounds b = col.bounds;
                b.Expand(1f);
                if (b.Contains(p))
                {
                    p.x = (p.x >= 0f ? 1f : -1f) * (GateHalfWidth - 1.5f);
                    t.position = p;
                    return;
                }
            }
        }

        private void Stage(GameObject go, Queue<GameObject> pool)
        {
            if (go == null) return;
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            pool.Enqueue(go);
        }

        private ChargeRing TakeRing()
        {
            while (_ringPool.Count > 0)
            {
                var go = _ringPool.Dequeue();
                if (go == null) continue;
                go.SetActive(true);
                var ring = go.GetComponent<ChargeRing>();
                ring.Reset();
                return ring;
            }
            return CreateRing();
        }

        private CoinPickup TakeCoin()
        {
            while (_coinPool.Count > 0)
            {
                var go = _coinPool.Dequeue();
                if (go == null) continue;
                go.SetActive(true);
                var coin = go.GetComponent<CoinPickup>();
                coin.Reset();
                return coin;
            }
            return CreateCoin();
        }

        private GameObject PlaceIsland(GameObject root, Vector3 center)
        {
            GameObject go;
            if (_islandPool.Count > 0 && (go = _islandPool.Dequeue()) != null)
            {
                go.SetActive(true);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.GetComponent<MeshRenderer>().sharedMaterial = RockMaterial;
            }
            go.name = "ChunkIsland";
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(10f, 25f, 10f);
            return go;
        }

        private ChargeRing CreateRing()
        {
            var go = new GameObject("ChunkRing");
            var line = go.AddComponent<LineRenderer>();
            const int points = 49;
            const float radius = 3f;
            line.positionCount = points;
            line.useWorldSpace = false;
            line.startWidth = 0.25f;
            line.endWidth = 0.25f;
            line.material = RingMaterial;
            for (int i = 0; i < points; i++)
            {
                float d = Mathf.Deg2Rad * 360f * i / (points - 1);
                line.SetPosition(i, new Vector3(radius * Mathf.Sin(d), radius * Mathf.Cos(d), 0f));
            }
            return go.AddComponent<ChargeRing>();
        }

        private CoinPickup CreateCoin()
        {
            var go = new GameObject("ChunkCoin");
            var gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gem.name = "Gem";
            gem.transform.SetParent(go.transform, false);
            gem.transform.localPosition = Vector3.zero;
            gem.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
            Object.DestroyImmediate(gem.GetComponent<Collider>());
            gem.GetComponent<MeshRenderer>().sharedMaterial = CoinMaterial;
            return go.AddComponent<CoinPickup>();
        }

        private Material RingMaterial
        {
            get
            {
                if (_ringMat == null)
                {
                    _ringMat = new Material(Shader.Find("Sprites/Default"));
                    _ringMat.color = new Color(1f, 0.85f, 0.2f);
                    _ringMat.renderQueue = 3000;
                }
                return _ringMat;
            }
        }

        private Material CoinMaterial
        {
            get
            {
                if (_coinMat == null)
                {
                    _coinMat = new Material(Shader.Find("Standard"));
                    _coinMat.color = new Color(1f, 0.75f, 0.15f);
                    _coinMat.SetFloat("_Metallic", 0.85f);
                    _coinMat.SetFloat("_Glossiness", 0.55f);
                }
                return _coinMat;
            }
        }

        private Material RockMaterial
        {
            get
            {
                if (_rockMat == null)
                {
                    _rockMat = new Material(Shader.Find("Standard"));
                    _rockMat.color = new Color(0.4f, 0.42f, 0.45f);
                }
                return _rockMat;
            }
        }

        // Shared mood instances, created once (Task 6). Defaults match the
        // Lagoon starter; ApplyMood re-tints per chunk type.
        public Material WaterMaterial
        {
            get
            {
                if (_waterMat == null)
                {
                    _waterMat = new Material(Shader.Find("Standard"));
                    _waterMat.color = new Color(0.2f, 0.6f, 0.75f);
                }
                return _waterMat;
            }
        }

        public Material SkyMaterial
        {
            get
            {
                if (_skyMat == null)
                {
                    _skyMat = new Material(Shader.Find("Standard"));
                    _skyMat.color = new Color(0.53f, 0.81f, 0.92f);
                }
                return _skyMat;
            }
        }

        private static List<float> Halves(List<GameObject> segs)
        {
            var halves = new List<float>(segs.Count);
            foreach (var s in segs)
            {
                float half = 500f;
                if (s != null)
                {
                    var rend = s.GetComponentInChildren<MeshRenderer>();
                    if (rend != null) half = rend.bounds.extents.z;
                }
                halves.Add(Mathf.Max(half, 1f));
            }
            return halves;
        }
    }
}
