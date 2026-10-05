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
        // Cloud-realm layer: sky content lives above 55m; the cloud sea
        // fades in 40→60m (RealmBlend). Spires are tall colliders.
        public const float SkyBaseY = 60f;
        public const float SkyStepY = 12f;
        public const float SpireCenterY = 55f;
        public const float SpireHalfDepth = 2f;
        public const float RealmLowY = 40f;
        public const float RealmHighY = 60f;
        public const float CloudY = 35f;
        public const float CloudSize = 1200f;
        public const float CloudMaxAlpha = 0.95f;

        private readonly List<ChargeRing> _rings = new List<ChargeRing>();
        private readonly List<CoinPickup> _coins = new List<CoinPickup>();
        private readonly List<GameObject> _islands = new List<GameObject>();
        private readonly List<GameObject> _spires = new List<GameObject>();
        private readonly List<GameObject> _decor = new List<GameObject>();
        private readonly Queue<GameObject> _spirePool = new Queue<GameObject>();
        private readonly Queue<GameObject> _decorPool = new Queue<GameObject>();
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
        public IReadOnlyList<GameObject> Spires => _spires;
        public IReadOnlyList<GameObject> Decor => _decor;

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
                foreach (var s in _spires)
                    if (s != null) furthest = Mathf.Max(furthest, s.transform.position.z + SpireHalfDepth);
                foreach (var d in _decor)
                    if (d != null) furthest = Mathf.Max(furthest, d.transform.position.z);
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
            EnsureCloudSea();
        }

        // Cloud sea: one big soft deck the fish climbs into above RealmLowY
        // and leaves below it. Plane primitive faces +Y, so it reads as a
        // deck from the sky and stays invisible from underwater. Follows
        // the fish (x/z) and fades with RealmBlend — purely visual.
        private GameObject _cloudSea;
        private Material _cloudMat;
        private Transform _fish;

        private void EnsureCloudSea()
        {
            if (_cloudSea != null) return;
            _cloudMat = new Material(Shader.Find("Standard"));
            _cloudMat.color = new Color(0.95f, 0.97f, 1f, 0f);
            _cloudMat.SetFloat("_Mode", 3f);
            _cloudMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _cloudMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _cloudMat.SetInt("_ZWrite", 0);
            _cloudMat.DisableKeyword("_ALPHATEST_ON");
            _cloudMat.EnableKeyword("_ALPHABLEND_ON");
            _cloudMat.SetOverrideTag("RenderType", "Transparent");
            _cloudMat.renderQueue = 3000;
            _cloudSea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _cloudSea.name = "CloudSea";
            var col = _cloudSea.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            _cloudSea.transform.localScale = new Vector3(CloudSize / 10f, 1f, CloudSize / 10f);
            var rend = _cloudSea.GetComponent<MeshRenderer>();
            rend.sharedMaterial = _cloudMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        void Update()
        {
            if (_fish == null)
            {
                var mover = Object.FindFirstObjectByType<PlayerMovementController>();
                if (mover == null) return;
                _fish = mover.transform;
            }
            if (_cloudSea != null && _cloudMat != null)
            {
                var p = _cloudSea.transform.position;
                p.x = _fish.position.x;
                p.z = _fish.position.z;
                p.y = CloudY;
                _cloudSea.transform.position = p;
                var c = _cloudMat.color;
                c.a = RealmBlend(_fish.position.y) * CloudMaxAlpha;
                _cloudMat.color = c;
            }
            EnsureSilhouettes();
            for (int i = 0; i < _silhouettes.Count; i++)
            {
                if (_silhouettes[i] == null) continue;
                var p = _silhouettes[i].transform.position;
                p.z = SilhouetteZ(_fish.position.z, i);
                _silhouettes[i].transform.position = p;
            }
        }

        // Horizon mesas: three dark slabs that always sit ahead, so the
        // runway never reads as void. Decor (no colliders), follow the
        // fish, never reclaimed. Pure spacing math for tests.
        public static float SilhouetteZ(float fishZ, int i)
        {
            return fishZ + 500f + i * 250f;
        }

        private readonly List<GameObject> _silhouettes = new List<GameObject>();

        private void EnsureSilhouettes()
        {
            if (_silhouettes.Count > 0) return;
            for (int i = 0; i < 3; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "HorizonSilhouette";
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(i % 2 == 0 ? -150f : 150f, 5f, SilhouetteZ(0f, i));
                go.transform.localScale = new Vector3(80f, 40f, 30f);
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.16f, 0.2f, 0.3f);
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                _silhouettes.Add(go);
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
            // Tint water RGB only: stamping the spec alpha (1 everywhere)
            // would turn the sea opaque and hide underwater gameplay.
            var water = WaterMaterial.color;
            WaterMaterial.color = new Color(spec.WaterTint.r, spec.WaterTint.g, spec.WaterTint.b, water.a);
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

            // Gauntlet gates: rock arches anchored over swim ring lines —
            // pillars flank the ring, lintel clears it above. All blocking;
            // steer through the middle. Falls back to lane center when the
            // chunk has no swim rings of its own.
            int waterStart = _rings.Count - spec.RingCount;
            for (int a = 0; a < spec.ArchCount; a++)
            {
                Vector3 anchor = new Vector3(0f, SwimY, zStart + spec.Length * (a + 1f) / (spec.ArchCount + 1f));
                if (spec.RingCount > 0 && waterStart >= 0 && waterStart + (a % spec.RingCount) < _rings.Count
                    && _rings[waterStart + (a % spec.RingCount)] != null)
                    anchor = _rings[waterStart + (a % spec.RingCount)].transform.position;
                _islands.Add(PlaceRock(root, anchor + new Vector3(-8f, 0f, 0f), new Vector3(4f, 20f, 4f), "ChunkArchPillar"));
                _islands.Add(PlaceRock(root, anchor + new Vector3(8f, 0f, 0f), new Vector3(4f, 20f, 4f), "ChunkArchPillar"));
                _islands.Add(PlaceRock(root, anchor + new Vector3(0f, 12f, 0f), new Vector3(20f, 4f, 4f), "ChunkArchLintel"));
            }

            float cx = GateHalfWidth + IslandHalfX;
            for (int i = 0; i < spec.IslandPairs; i++)
            {
                float z = zStart + spec.Length * (i + 1f) / (spec.IslandPairs + 1f)
                    + ((float)rng.NextDouble() - 0.5f) * 20f;
                _islands.Add(PlaceIsland(root, new Vector3(-cx, IslandCenterY, z)));
                _islands.Add(PlaceIsland(root, new Vector3(cx, IslandCenterY, z)));
            }

            // Cloud-realm layer: sky rings/coins/spires ride the same pools
            // and clearance as water content (spawner feeds them wholesale).
            float skySpacing = Mathf.Max(spec.RingSpacing, MinRingSpacing);
            for (int k = 0; k < spec.SkyRingCount; k++)
            {
                var ring = TakeRing();
                ring.transform.SetParent(root.transform, false);
                ring.transform.position = new Vector3(
                    Mathf.Lerp(-LaneX, LaneX, (float)rng.NextDouble()),
                    SkyBaseY + k * SkyStepY,
                    zStart + (k + 1) * skySpacing);
                _rings.Add(ring);
            }
            for (int t = 0; t < spec.SkyCoinTrails; t++)
            {
                float baseZ = zStart + spec.Length * (t + 1f) / (spec.SkyCoinTrails + 1f);
                float x = Mathf.Lerp(-LaneX, LaneX, (float)rng.NextDouble());
                float y = SkyBaseY + 10f + t * 8f;
                for (int i = 0; i < spec.CoinsPerTrail; i++)
                {
                    var coin = TakeCoin();
                    coin.transform.SetParent(root.transform, false);
                    coin.transform.position = new Vector3(
                        x, y, baseZ + (i - (spec.CoinsPerTrail - 1f) / 2f) * CoinSpacing);
                    _coins.Add(coin);
                }
            }
            for (int i = 0; i < spec.SkySpireCount; i++)
            {
                float z = zStart + spec.Length * (i + 1f) / (spec.SkySpireCount + 1f)
                    + ((float)rng.NextDouble() - 0.5f) * 20f;
                float x = (rng.NextDouble() < 0.5f ? -1f : 1f) * (12f + (float)rng.NextDouble() * 6f);
                _spires.Add(PlaceSpire(root, new Vector3(x, SpireCenterY, z), 50f + (float)rng.NextDouble() * 40f));
            }

            // Dressing: one decor kind per chunk, always off the prompt
            // lanes (|x| >= 25 or sunk to the seabed) and never colliding,
            // so decor carries no safety burden. Not pooled — primitives
            // are cheap to rebuild at streaming cadence.
            for (int i = 0; i < spec.DecorCount; i++)
            {
                float z = zStart + spec.Length * (i + 1f) / (spec.DecorCount + 1f);
                _decor.Add(PlaceDecor(root, spec.DecorKind, z, rng));
            }

            // Fresh transforms leave collider bounds stale until the next
            // physics step (ledger precedent): sync so the clearance pass
            // below reads true island volumes in EditMode and on frame one.
            Physics.SyncTransforms();
            ResolvePromptClearance();
        }

        // Realm blend 0..1: ocean below RealmLowY, cloud-sea above RealmHighY.
        public static float RealmBlend(float fishY)
        {
            return Mathf.Clamp01((fishY - RealmLowY) / (RealmHighY - RealmLowY));
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
            for (int i = _spires.Count - 1; i >= 0; i--)
            {
                if (_spires[i] == null || _spires[i].transform.position.z + SpireHalfDepth < z)
                {
                    Stage(_spires[i], _spirePool);
                    _spires.RemoveAt(i);
                }
            }
            // Decor is rebuilt, not pooled (cheap primitives at streaming
            // cadence); silhouettes follow the fish and are never reclaimed.
            for (int i = _decor.Count - 1; i >= 0; i--)
            {
                var go = _decor[i];
                _decor.RemoveAt(i);
                if (go == null) continue;
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
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
                if (TryClear(island, ref p, t)) return;
            foreach (var spire in _spires)
                if (TryClear(spire, ref p, t)) return;
        }

        // Bounds.Contains is 3D, so swim rocks and sky spires share it.
        private static bool TryClear(GameObject rock, ref Vector3 p, Transform t)
        {
            if (rock == null) return false;
            var col = rock.GetComponent<Collider>();
            if (col == null) return false;
            Bounds b = col.bounds;
            b.Expand(1f);
            if (!b.Contains(p)) return false;
            p.x = (p.x >= 0f ? 1f : -1f) * (GateHalfWidth - 1.5f);
            t.position = p;
            return true;
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
            return PlaceRock(root, center, new Vector3(10f, 25f, 10f), "ChunkIsland");
        }

        // Rock obstacles share one pool: placement always resets scale,
        // name, and transform, so islands, spires, and arch stones mix.
        private GameObject PlaceRock(GameObject root, Vector3 center, Vector3 scale, string name)
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
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.transform.localScale = scale;
            return go;
        }

        // Sky spires: tall colliders centered on SpireCenterY, pooled and
        // reclaimed exactly like islands. Height varies per seed.
        private GameObject PlaceSpire(GameObject root, Vector3 center, float height)
        {
            GameObject go;
            if (_spirePool.Count > 0 && (go = _spirePool.Dequeue()) != null)
            {
                go.SetActive(true);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.GetComponent<MeshRenderer>().sharedMaterial = RockMaterial;
            }
            go.name = "ChunkSpire";
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(4f, height, 4f);
            return go;
        }

        // Dressing composer: kind selects the shape set, count is the
        // budget. Every piece parks off-lane with no collider.
        private GameObject PlaceDecor(GameObject root, string kind, float z, System.Random rng)
        {
            var go = new GameObject("ChunkDecor_" + (kind ?? "rubble"));
            go.transform.SetParent(root.transform, false);
            float x = (rng.NextDouble() < 0.5f ? -1f : 1f) * (25f + (float)rng.NextDouble() * 35f);
            go.transform.position = new Vector3(x, kind == "cloud" ? 95f : -10f, z);
            switch (kind)
            {
                case "coral":
                    AddBlob(go, new Vector3(x, -10.5f, z), new Vector3(2.5f, 1f, 2.5f), DecorMaterial("rubble"));
                    AddBlob(go, new Vector3(x, -9f, z), new Vector3(1.5f, 3f, 0.8f), DecorMaterial("coral"));
                    break;
                case "cloud":
                    AddBlob(go, new Vector3(x, 100f + (float)rng.NextDouble() * 25f, z), new Vector3(8f, 4f, 6f), DecorMaterial("cloud"));
                    AddBlob(go, new Vector3(x + 5f, 102f + (float)rng.NextDouble() * 25f, z), new Vector3(6f, 3f, 5f), DecorMaterial("cloud"));
                    break;
                case "crag":
                    AddBlob(go, new Vector3(x, -3f, z), new Vector3(3f, 10f, 3f), RockMaterial, 15f * ((float)rng.NextDouble() - 0.5f));
                    break;
                default: // "rubble" and anything unknown: seabed scatter
                    AddBlob(go, new Vector3(x, -11.3f, z), Vector3.one * (1f + (float)rng.NextDouble() * 1.5f), DecorMaterial("rubble"));
                    break;
            }
            return go;
        }

        private static void AddBlob(GameObject root, Vector3 center, Vector3 scale, Material mat, float tiltZ = 0f)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = "Blob";
            part.transform.SetParent(root.transform, false);
            part.transform.position = center;
            part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = mat;
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

        private Material _coralMat;
        private Material _puffMat;
        private Material _sandMat;

        // Decor palette (shared instances like the rest): coral pink,
        // cloud white, sand. RockMaterial covers crag/rubble.
        private Material DecorMaterial(string kind)
        {
            switch (kind)
            {
                case "cloud":
                    if (_puffMat == null)
                    {
                        _puffMat = new Material(Shader.Find("Standard"));
                        _puffMat.color = new Color(0.96f, 0.97f, 1f);
                    }
                    return _puffMat;
                case "coral":
                    if (_coralMat == null)
                    {
                        _coralMat = new Material(Shader.Find("Standard"));
                        _coralMat.color = new Color(0.95f, 0.45f, 0.55f);
                    }
                    return _coralMat;
                default:
                    if (_sandMat == null)
                    {
                        _sandMat = new Material(Shader.Find("Standard"));
                        _sandMat.color = new Color(0.8f, 0.7f, 0.5f);
                    }
                    return _sandMat;
            }
        }

        // Shared mood instances, created once (Task 6). Defaults match the
        // Lagoon starter; ApplyMood re-tints per chunk type. The sea keeps
        // the M1 transparent blend (alpha 0.6) under every mood: opaque
        // water hides the fish and the swim rings (live bug).
        public Material WaterMaterial
        {
            get
            {
                if (_waterMat == null)
                {
                    _waterMat = new Material(Shader.Find("Standard"));
                    _waterMat.color = new Color(0.2f, 0.6f, 0.75f, 0.6f);
                    _waterMat.SetFloat("_Mode", 3f);
                    _waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    _waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _waterMat.SetInt("_ZWrite", 0);
                    _waterMat.DisableKeyword("_ALPHATEST_ON");
                    _waterMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    _waterMat.SetOverrideTag("RenderType", "Transparent");
                    _waterMat.renderQueue = 3000;
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
