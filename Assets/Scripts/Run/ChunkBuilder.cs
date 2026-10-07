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
        // Trail weave: coins snake across x instead of one straight lane.
        public const float WeaveAmp = 6f;
        public const float WeaveFreq = 1.2f;
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
        // Ring model prefab (watercraft gate). Wired once at scene build
        // via Configure; null keeps the legacy LineRenderer path so old
        // scenes and bare test builders still work.
        private List<float> _waterHalf = new List<float>();
        private List<float> _seabedHalf = new List<float>();
        private Material _ringMat;
        private Material _coinMat;
        private Material _rockMat;
        private Material _waterMat;
        private Material _skyMat;
        private string _appliedMoodId;
        // Skinned dressing sets, cached per BuildChunk from spec.Skin
        // (null = legacy primitive path, rng sequence untouched).
        private GameObject[] _decorPrefabs;
        private GameObject[] _islandPrefabs;
        private GameObject[] _spirePrefabs;
        private GameObject[] _archPrefabs;
        private GameObject[] _pillarPrefabs;
        private GameObject[] _lintelPrefabs;

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
        [SerializeField] private GameObject _ringPrefab;
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
        public void Configure(List<GameObject> waterSegs, List<GameObject> seabedSegs, Renderer skyRenderer = null, GameObject ringPrefab = null)
        {
            _waterSegs = waterSegs ?? new List<GameObject>();
            _seabedSegs = seabedSegs ?? new List<GameObject>();
            _skyRenderer = skyRenderer;
            _ringPrefab = ringPrefab;
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
                go.GetComponent<MeshRenderer>().sharedMaterial = SilhouetteMaterial;
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
            var skin = spec.Skin;
            if (skin != null && skin.SkyPanorama != null)
            {
                RenderSettings.skybox = SkyboxMaterial(skin);
            }
            else
            {
                RenderSettings.skybox = null;
            }
            Color skyTint = skin != null ? skin.SkyTint : spec.SkyTint;
            Color fogColor = skin != null ? skin.FogColor : spec.FogColor;
            float fogDensity = skin != null ? skin.FogDensity : spec.FogDensity;
            Color waterTint = skin != null ? skin.WaterTint : spec.WaterTint;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientLight = skyTint;
            SkyMaterial.color = skyTint;
            // Tint water RGB only: stamping the spec alpha (1 everywhere)
            // would turn the sea opaque and hide underwater gameplay.
            var water = WaterMaterial.color;
            WaterMaterial.color = new Color(waterTint.r, waterTint.g, waterTint.b, water.a);
            if (skin != null)
            {
                // Rings keep game-asset colors (§34.1): never tinted here,
                // so one shared watercraft material serves rings and shells.
                CoinMaterial.color = skin.CoinTint;
                SilhouetteMaterial.color = skin.SilhouetteColor;
                if (_cloudMat != null)
                {
                    var cloud = _cloudMat.color;
                    _cloudMat.color = new Color(skin.CloudTint.r, skin.CloudTint.g, skin.CloudTint.b, cloud.a);
                }
            }
            else
            {
                // Legacy specs never owned prompt dressing: restore the
                // shared defaults so a skin->legacy transition can't leak
                // the previous realm's tints. Rings are exempt (asset
                // colors, see above); the legacy line keeps its lazy gold.
                CoinMaterial.color = DefaultCoinTint;
                SilhouetteMaterial.color = DefaultSilhouetteColor;
                if (_cloudMat != null)
                {
                    var cloud = _cloudMat.color;
                    _cloudMat.color = new Color(DefaultCloudTint.r, DefaultCloudTint.g, DefaultCloudTint.b, cloud.a);
                }
            }
        }

        // One panoramic skybox material per skin, cached like the other
        // shared mood instances.
        private readonly Dictionary<RealmSkin, Material> _skyboxMats = new Dictionary<RealmSkin, Material>();

        private Material SkyboxMaterial(RealmSkin skin)
        {
            Material mat;
            if (!_skyboxMats.TryGetValue(skin, out mat) || mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic"));
                mat.SetTexture("_MainTex", skin.SkyPanorama);
                _skyboxMats[skin] = mat;
            }
            mat.SetColor("_Tint", skin.SkyTint);
            return mat;
        }

        public void BuildChunk(ChunkSpec spec, float zStart, int seed)
        {
            if (spec == null) return;
            ApplyMood(spec);
            var skin = spec.Skin;
            _decorPrefabs = NonNull(skin != null ? skin.DecorPrefabs : null);
            _islandPrefabs = NonNull(skin != null ? skin.IslandPrefabs : null);
            _spirePrefabs = NonNull(skin != null ? skin.SpirePrefabs : null);
            _archPrefabs = NonNull(skin != null ? skin.ArchPrefabs : null);
            // Matched arch sets are optional: empty falls back to the
            // shared arch set (legacy behavior, random per cube).
            _pillarPrefabs = NonNull(skin != null ? skin.ArchPillarPrefabs : null);
            if (_pillarPrefabs == null || _pillarPrefabs.Length == 0) _pillarPrefabs = _archPrefabs;
            _lintelPrefabs = NonNull(skin != null ? skin.ArchLintelPrefabs : null);
            if (_lintelPrefabs == null || _lintelPrefabs.Length == 0) _lintelPrefabs = _archPrefabs;
            var rng = new System.Random(seed);
            var root = new GameObject(string.Format("Chunk_{0}_{1}_{2}", spec.ChunkId, zStart, seed));
            root.transform.SetParent(transform, false);
            _roots.Add(root);

            // Showcase chunk: hand-placed layout instead of procedural
            // dice. Same place functions, same colliders/judging; invalid
            // entries are skipped with an error (the editor tool validates
            // before shipping — runtime never crashes on bad data).
            if (spec.Layout != null)
            {
                BuildLayoutContent(spec, spec.Layout, root, zStart);
                Physics.SyncTransforms();
                ResolvePromptClearance();
                return;
            }

            float spacing = Mathf.Max(spec.RingSpacing, MinRingSpacing);
            int swimRings = Mathf.RoundToInt(spec.RingCount * Mathf.Clamp01(spec.RingSwimFraction));
            var waterRings = new System.Collections.Generic.List<UnityEngine.Vector3>();
            for (int k = 0; k < spec.RingCount; k++)
            {
                var ring = TakeRing();
                ring.transform.SetParent(root.transform, false);
                ring.transform.position = new Vector3(
                    Mathf.Lerp(-LaneX, LaneX, (float)rng.NextDouble()),
                    k < swimRings ? SwimY : FlyY,
                    zStart + (k + 1) * spacing);
                _rings.Add(ring);
                waterRings.Add(ring.transform.position);
            }

            // Coin trails lead into rings: one trail per ring, coins running
            // along -z into the ring mouth on the ring's own lane, x
            // converging from a random start with the usual weave.
            for (int k = 0; k < spec.RingCount; k++)
                PlaceCoinTrail(root, waterRings[k], spec, rng);

            // Gauntlet gates: rock arches anchored over swim ring lines —
            // pillars flank the ring, lintel clears it above. All blocking;
            // steer through the middle. Falls back to lane center when the
            // chunk has no swim rings of its own. One pillar pick per arch
            // (both pillars match) plus one lintel pick.
            int waterStart = _rings.Count - spec.RingCount;
            for (int a = 0; a < spec.ArchCount; a++)
            {
                Vector3 anchor = new Vector3(0f, SwimY, zStart + spec.Length * (a + 1f) / (spec.ArchCount + 1f));
                if (spec.RingCount > 0 && waterStart >= 0 && waterStart + (a % spec.RingCount) < _rings.Count
                    && _rings[waterStart + (a % spec.RingCount)] != null)
                    anchor = _rings[waterStart + (a % spec.RingCount)].transform.position;
                GameObject pillarPrefab = Draw(_pillarPrefabs, rng);
                GameObject lintelPrefab = Draw(_lintelPrefabs, rng);
                _islands.Add(PlaceRock(root, anchor + new Vector3(-8f, 0f, 0f), new Vector3(4f, 20f, 4f), "ChunkArchPillar", pillarPrefab));
                _islands.Add(PlaceRock(root, anchor + new Vector3(8f, 0f, 0f), new Vector3(4f, 20f, 4f), "ChunkArchPillar", pillarPrefab));
                _islands.Add(PlaceRock(root, anchor + new Vector3(0f, 12f, 0f), new Vector3(20f, 4f, 4f), "ChunkArchLintel", lintelPrefab));
            }

            float cx = GateHalfWidth + IslandHalfX;
            for (int i = 0; i < spec.IslandPairs; i++)
            {
                float z = zStart + spec.Length * (i + 1f) / (spec.IslandPairs + 1f)
                    + ((float)rng.NextDouble() - 0.5f) * 20f;
                _islands.Add(PlaceIsland(root, new Vector3(-cx, IslandCenterY, z), Draw(_islandPrefabs, rng)));
                _islands.Add(PlaceIsland(root, new Vector3(cx, IslandCenterY, z), Draw(_islandPrefabs, rng)));
            }

            // Cloud-realm layer: sky rings/coins/spires ride the same pools
            // and clearance as water content (spawner feeds them wholesale).
            float skySpacing = Mathf.Max(spec.RingSpacing, MinRingSpacing);
            var skyRings = new System.Collections.Generic.List<UnityEngine.Vector3>();
            for (int k = 0; k < spec.SkyRingCount; k++)
            {
                var ring = TakeRing();
                ring.transform.SetParent(root.transform, false);
                ring.transform.position = new Vector3(
                    Mathf.Lerp(-LaneX, LaneX, (float)rng.NextDouble()),
                    SkyBaseY + k * SkyStepY,
                    zStart + (k + 1) * skySpacing);
                _rings.Add(ring);
                skyRings.Add(ring.transform.position);
            }
            for (int k = 0; k < spec.SkyRingCount; k++)
                PlaceCoinTrail(root, skyRings[k], spec, rng);
            for (int i = 0; i < spec.SkySpireCount; i++)
            {
                float z = zStart + spec.Length * (i + 1f) / (spec.SkySpireCount + 1f)
                    + ((float)rng.NextDouble() - 0.5f) * 20f;
                float x = (rng.NextDouble() < 0.5f ? -1f : 1f) * (12f + (float)rng.NextDouble() * 6f);
                _spires.Add(PlaceSpire(root, new Vector3(x, SpireCenterY, z), 50f + (float)rng.NextDouble() * 40f, Draw(_spirePrefabs, rng)));
            }

            // Dressing: one decor kind per chunk, always off the prompt
            // lanes (|x| >= 25 or sunk to the seabed) and never colliding,
            // so decor carries no safety burden. Not pooled — primitives
            // are cheap to rebuild at streaming cadence.
            PlaceDecorSet(root, spec, zStart, rng);

            // Fresh transforms leave collider bounds stale until the next
            // physics step (ledger precedent): sync so the clearance pass
            // below reads true island volumes in EditMode and on frame one.
            Physics.SyncTransforms();
            ResolvePromptClearance();
        }

        // One coin trail per ring, shared by procedural and layout chunks
        // (extracted verbatim: same draws, same math, suite-guarded).
        private void PlaceCoinTrail(GameObject root, Vector3 rp, ChunkSpec spec, System.Random rng)
        {
            float startX = Mathf.Lerp(-LaneX, LaneX, (float)rng.NextDouble());
            for (int i = 0; i < spec.CoinsPerTrail; i++)
            {
                float t = spec.CoinsPerTrail == 1 ? 1f : (float)i / (spec.CoinsPerTrail - 1);
                var coin = TakeCoin();
                coin.transform.SetParent(root.transform, false);
                coin.transform.position = new Vector3(
                    Mathf.Lerp(startX, rp.x, t) + WeaveAmp * Mathf.Sin(i * WeaveFreq),
                    rp.y,
                    rp.z - (spec.CoinsPerTrail - 1 - i) * CoinSpacing);
                _coins.Add(coin);
            }
        }

        private void PlaceDecorSet(GameObject root, ChunkSpec spec, float zStart, System.Random rng)
        {
            for (int i = 0; i < spec.DecorCount; i++)
            {
                float z = zStart + spec.Length * (i + 1f) / (spec.DecorCount + 1f);
                _decor.Add(PlaceDecor(root, spec.DecorKind, z, rng));
            }
        }

        // Showcase chunk: hand-placed layout. Placement mirrors the
        // procedural order (arches, islands, spires) so list shapes match.
        // Derived content (coins, decor) runs on the layout-fixed seed:
        // the same chunk streams identically every time.
        private void BuildLayoutContent(ChunkSpec spec, ChunkLayout layout, GameObject root, float zStart)
        {
            var lrng = new System.Random(layout.Seed);
            var waterRings = new System.Collections.Generic.List<UnityEngine.Vector3>();
            var rings = layout.Rings ?? new ChunkLayout.RingEntry[0];
            foreach (var e in rings)
            {
                if (string.IsNullOrEmpty(e.Id) || !IsFinite(e.Position))
                {
                    Debug.LogError("[ChunkBuilder] layout '" + layout.LayoutId + "' skips invalid ring.");
                    continue;
                }
                var ring = TakeRing();
                ring.transform.SetParent(root.transform, false);
                ring.transform.position = new Vector3(e.Position.x, e.Position.y, zStart + e.Position.z);
                _rings.Add(ring);
                waterRings.Add(ring.transform.position);
            }
            foreach (var rp in waterRings)
                PlaceCoinTrail(root, rp, spec, lrng);
            var arches = layout.Arches ?? new ChunkLayout.ArchEntry[0];
            foreach (var e in arches)
            {
                if (string.IsNullOrEmpty(e.Id) || !IsFinite(e.Anchor))
                {
                    Debug.LogError("[ChunkBuilder] layout '" + layout.LayoutId + "' skips invalid arch.");
                    continue;
                }
                Vector3 anchor = new Vector3(e.Anchor.x, e.Anchor.y, zStart + e.Anchor.z);
                GameObject pillarPrefab = ChunkLayout.ResolveShell(spec, ChunkLayout.ShellKind.Pillar, e.PillarShell);
                GameObject lintelPrefab = ChunkLayout.ResolveShell(spec, ChunkLayout.ShellKind.Lintel, e.LintelShell);
                if (pillarPrefab == null || lintelPrefab == null)
                {
                    Debug.LogError("[ChunkBuilder] layout '" + layout.LayoutId + "' arch '" + e.Id + "' shell does not resolve.");
                    continue;
                }
                _islands.Add(PlaceRock(root, anchor + new Vector3(-8f, 0f, 0f), new Vector3(4f, 20f, 4f), "ChunkArchPillar", pillarPrefab));
                _islands.Add(PlaceRock(root, anchor + new Vector3(8f, 0f, 0f), new Vector3(4f, 20f, 4f), "ChunkArchPillar", pillarPrefab));
                _islands.Add(PlaceRock(root, anchor + new Vector3(0f, 12f, 0f), new Vector3(20f, 4f, 4f), "ChunkArchLintel", lintelPrefab));
            }
            var islands = layout.Islands ?? new ChunkLayout.IslandEntry[0];
            foreach (var e in islands)
            {
                if (string.IsNullOrEmpty(e.Id) || !IsFinite(e.Position) || !IsFinite(e.Scale)
                    || e.Scale.x < 0.1f || e.Scale.y < 0.1f || e.Scale.z < 0.1f)
                {
                    Debug.LogError("[ChunkBuilder] layout '" + layout.LayoutId + "' skips invalid island '" + e.Id + "'.");
                    continue;
                }
                GameObject prefab = ChunkLayout.ResolveShell(spec, ChunkLayout.ShellKind.Island, e.ShellName);
                if (prefab == null)
                {
                    Debug.LogError("[ChunkBuilder] layout '" + layout.LayoutId + "' island '" + e.Id + "' shell does not resolve.");
                    continue;
                }
                _islands.Add(PlaceRock(root,
                    new Vector3(e.Position.x, e.Position.y, zStart + e.Position.z),
                    e.Scale, "ChunkIsland", prefab));
            }
            var spires = layout.Spires ?? new ChunkLayout.SpireEntry[0];
            foreach (var e in spires)
            {
                if (string.IsNullOrEmpty(e.Id) || !IsFinite(e.Position) || !(e.Height >= 1f))
                {
                    Debug.LogError("[ChunkBuilder] layout '" + layout.LayoutId + "' skips invalid spire '" + e.Id + "'.");
                    continue;
                }
                GameObject prefab = ChunkLayout.ResolveShell(spec, ChunkLayout.ShellKind.Spire, e.ShellName);
                if (prefab == null)
                {
                    Debug.LogError("[ChunkBuilder] layout '" + layout.LayoutId + "' spire '" + e.Id + "' shell does not resolve.");
                    continue;
                }
                _spires.Add(PlaceSpire(root,
                    new Vector3(e.Position.x, e.Position.y, zStart + e.Position.z),
                    e.Height, prefab));
            }
            PlaceDecorSet(root, spec, zStart, lrng);
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
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

        private GameObject PlaceIsland(GameObject root, Vector3 center, GameObject shellPrefab)
        {
            return PlaceRock(root, center, new Vector3(10f, 25f, 10f), "ChunkIsland", shellPrefab);
        }

        // One seeded pick from a set (null when the set is empty): the
        // single rng draw per obstacle keeps streams deterministic.
        private static GameObject Draw(GameObject[] set, System.Random rng)
        {
            if (set == null || set.Length == 0 || rng == null) return null;
            return set[rng.Next(set.Length)];
        }

        // Shared-material defaults (match the lazy initializers below):
        // the legacy mood path restores these so realm tints can't leak.
        private static readonly Color DefaultCoinTint = new Color(1f, 0.75f, 0.15f);
        private static readonly Color DefaultSilhouetteColor = new Color(0.16f, 0.2f, 0.3f);
        private static readonly Color DefaultCloudTint = new Color(0.95f, 0.97f, 1f);

        // Hand-edited skins may hold null entries: drop them once at
        // cache time so placement never throws mid-BuildChunk and the
        // rng sequence stays deterministic.
        private static GameObject[] NonNull(GameObject[] prefabs)
        {
            if (prefabs == null) return null;
            var kept = new List<GameObject>(prefabs.Length);
            foreach (var p in prefabs)
                if (p != null) kept.Add(p);
            return kept.ToArray();
        }

        // Uniform fit of a visual into a shell volume: scale by the
        // tightest axis so the visual never pokes out. Zero extents
        // (empty bounds) fall back to 1.
        public static float FitScale(Vector3 shell, Vector3 visual)
        {
            float s = float.PositiveInfinity;
            if (visual.x > 0f) s = Mathf.Min(s, shell.x / visual.x);
            if (visual.y > 0f) s = Mathf.Min(s, shell.y / visual.y);
            if (visual.z > 0f) s = Mathf.Min(s, shell.z / visual.z);
            if (float.IsInfinity(s) || s <= 0f) return 1f;
            return s;
        }

        // Visual shell over a collider cube: the cube (position, scale,
        // collider) is gameplay and never changes; the FBX visuals are
        // fitted children and the cube renderer goes dark. Strips a prior
        // shell first so pooled cubes never stack stale visuals.
        // Stacked shell: one prefab pick per obstacle, repeated bottom-up
        // to fill tall colliders (spires, islands, pillars) with
        // aspect-preserved segments. Tall visuals collapse to a single
        // exact-fit segment, so short lintels behave like the old
        // single-shell path. Segments never poke out: min-axis fit per
        // segment, floor-fill count (top gap under one segment, accepted),
        // bottom-aligned base.
        private static void AttachStackedShell(GameObject cube, GameObject prefab)
        {
            StripShells(cube);
            var rend = cube.GetComponent<MeshRenderer>();
            if (rend != null) rend.enabled = false;
            var first = Object.Instantiate(prefab);
            first.name = "ChunkShell_" + prefab.name;
            // Combined bounds across the full prefab hierarchy, measured
            // before parenting so the cube's scale can't pollute the fit.
            var renderers = first.GetComponentsInChildren<MeshRenderer>();
            Bounds vis = new Bounds(first.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!any) { vis = r.bounds; any = true; }
                else vis.Encapsulate(r.bounds);
            }
            if (!any)
            {
                // Renderer-less prefab: restore the cube instead of
                // leaving an invisible-but-collidable box.
                Object.DestroyImmediate(first);
                if (rend != null) rend.enabled = true;
                return;
            }
            Vector3 parentScale = cube.transform.localScale;
            Vector3 cubePos = cube.transform.position;
            float s = FitScale(parentScale, vis.size);
            float segH = vis.size.y * s;
            int count = segH > 0f ? Mathf.Max(1, Mathf.FloorToInt(parentScale.y / segH)) : 1;
            float baseY = cubePos.y - parentScale.y / 2f;
            // Preserve an authored prefab-root scale (usually identity):
            // bounds were measured with it, so the fit multiplies on top.
            Vector3 fitted = Vector3.Scale(first.transform.localScale, CounterScale(parentScale, s));
            first.transform.SetParent(cube.transform, false);
            first.transform.localScale = fitted;
            PlaceSegment(first, cube, cubePos, parentScale, baseY + segH * 0.5f);
            for (int i = 1; i < count; i++)
            {
                var seg = Object.Instantiate(first);
                seg.name = first.name;
                seg.transform.SetParent(cube.transform, false);
                seg.transform.localScale = first.transform.localScale;
                PlaceSegment(seg, cube, cubePos, parentScale, baseY + segH * (i + 0.5f));
            }
        }

        private static Vector3 CounterScale(Vector3 parentScale, float s)
        {
            // The parent cube is non-uniformly scaled, so counter-scale
            // per axis: world scale stays a uniform s.
            return new Vector3(
                parentScale.x != 0f ? s / parentScale.x : s,
                parentScale.y != 0f ? s / parentScale.y : s,
                parentScale.z != 0f ? s / parentScale.z : s);
        }

        private static void PlaceSegment(GameObject seg, GameObject cube, Vector3 cubePos, Vector3 parentScale, float worldY)
        {
            Vector3 world = new Vector3(cubePos.x, worldY, cubePos.z);
            Vector3 offset = world - cubePos;
            seg.transform.localPosition = new Vector3(
                parentScale.x != 0f ? offset.x / parentScale.x : offset.x,
                parentScale.y != 0f ? offset.y / parentScale.y : offset.y,
                parentScale.z != 0f ? offset.z / parentScale.z : offset.z);
            seg.transform.localRotation = Quaternion.identity;
        }

        private static void StripShells(GameObject cube)
        {
            if (cube == null) return;
            var doomed = new List<Transform>();
            foreach (Transform c in cube.transform)
                if (c.name.StartsWith("ChunkShell_")) doomed.Add(c);
            foreach (var c in doomed)
            {
                if (Application.isPlaying) Object.Destroy(c.gameObject);
                else Object.DestroyImmediate(c.gameObject);
            }
        }

        // Rock obstacles share one pool: placement always resets scale,
        // name, and transform, so islands, spires, and arch stones mix.
        // A null shell prefab keeps the bare cube (legacy/empty sets).
        private GameObject PlaceRock(GameObject root, Vector3 center, Vector3 scale, string name, GameObject shellPrefab)
        {
            GameObject go;
            if (_islandPool.Count > 0 && (go = _islandPool.Dequeue()) != null)
            {
                go.SetActive(true);
                // Pooled cubes may carry a prior chunk's shell: strip
                // always, so legacy/empty reuse never shows stale visuals.
                StripShells(go);
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
            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.enabled = true;
            if (shellPrefab != null)
                AttachStackedShell(go, shellPrefab);
            return go;
        }

        // Sky spires: tall colliders centered on SpireCenterY, pooled and
        // reclaimed exactly like islands. Height varies per seed.
        private GameObject PlaceSpire(GameObject root, Vector3 center, float height, GameObject shellPrefab)
        {
            GameObject go;
            if (_spirePool.Count > 0 && (go = _spirePool.Dequeue()) != null)
            {
                go.SetActive(true);
                StripShells(go);
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
            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.enabled = true;
            if (shellPrefab != null)
                AttachStackedShell(go, shellPrefab);
            return go;
        }

        // Dressing composer: kind selects the shape set, count is the
        // budget. Every piece parks off-lane with no collider.
        // Legacy kind volumes (combined AddBlob cluster bounds below):
        // skinned prefabs fit these so dressing keeps its old weight.
        private static readonly Dictionary<string, Vector3> DecorFit = new Dictionary<string, Vector3>
        {
            { "coral", new Vector3(2.5f, 3.5f, 2.5f) },
            { "cloud", new Vector3(8f, 4f, 6f) },
            { "crag", new Vector3(3f, 10f, 3f) },
            { "rubble", new Vector3(2.5f, 2.5f, 2.5f) },
        };

        private GameObject PlaceDecor(GameObject root, string kind, float z, System.Random rng)
        {
            float x = (rng.NextDouble() < 0.5f ? -1f : 1f) * (25f + (float)rng.NextDouble() * 35f);
            Vector3 pos = new Vector3(x, kind == "cloud" ? 95f : -10f, z);
            // Skinned dressing: one seeded prefab pick at the same lane
            // math. Prefabs carry no colliders (pinned by import tests).
            // Scaled to the legacy kind volumes (below) about the prefab
            // pivot, so base-planted models stay planted and clouds keep
            // their old weight. Uniform fit, never distorts.
            if (_decorPrefabs != null && _decorPrefabs.Length > 0)
            {
                var prefab = _decorPrefabs[rng.Next(_decorPrefabs.Length)];
                var dressed = Object.Instantiate(prefab);
                dressed.name = "ChunkDecor_" + prefab.name;
                dressed.transform.SetParent(root.transform, false);
                dressed.transform.position = pos;
                Vector3 target;
                if (!DecorFit.TryGetValue(kind ?? "rubble", out target)) target = DecorFit["rubble"];
                var renderers = dressed.GetComponentsInChildren<MeshRenderer>();
                Bounds vis = new Bounds(dressed.transform.position, Vector3.zero);
                bool any = false;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!any) { vis = r.bounds; any = true; }
                    else vis.Encapsulate(r.bounds);
                }
                if (any)
                    dressed.transform.localScale = dressed.transform.localScale * FitScale(target, vis.size);
                return dressed;
            }
            var go = new GameObject("ChunkDecor_" + (kind ?? "rubble"));
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
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
            // Explicit injection (tests, scene wiring) wins; otherwise
            // load the pipeline prefab. Still null (asset missing) falls
            // through to the legacy line — never a null ring.
            if (_ringPrefab == null)
                _ringPrefab = Resources.Load<GameObject>("Ring");
            if (_ringPrefab != null)
            {
                var visual = Object.Instantiate(_ringPrefab);
                visual.name = "RingVisual_" + _ringPrefab.name;
                // Combined bounds across the full prefab hierarchy,
                // measured before parenting at the prefab's own origin.
                var renderers = visual.GetComponentsInChildren<MeshRenderer>();
                Bounds bounds = new Bounds(visual.transform.position, Vector3.zero);
                bool any = false;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!any) { bounds = r.bounds; any = true; }
                    else bounds.Encapsulate(r.bounds);
                }
                if (any)
                {
                    // Fit the opening to the judging disc: uniform scale
                    // maps the outer height onto the disc diameter, bounds
                    // centered on the ring origin (BuildChunk positions it
                    // after TakeRing). Pooling never refits: Reset only
                    // re-enables renderers, position is set per build.
                    float s = bounds.size.y > 0f
                        ? (TimingPromptSpawner.RingPromptRadius * 2f) / bounds.size.y
                        : 1f;
                    Vector3 p0 = visual.transform.position;
                    visual.transform.SetParent(go.transform, false);
                    visual.transform.localScale = Vector3.one * s;
                    visual.transform.localPosition = -(bounds.center - p0) * s;
                    return go.AddComponent<ChargeRing>();
                }
                Object.DestroyImmediate(visual);
            }
            var line = go.AddComponent<LineRenderer>();
            const int points = 49;
            float radius = TimingPromptSpawner.RingPromptRadius;
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
            gem.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
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

        private Material _silhouetteMat;

        // One shared slab material (was one instance per slab): skinned
        // moods re-tint it, legacy keeps the default dusk blue.
        private Material SilhouetteMaterial
        {
            get
            {
                if (_silhouetteMat == null)
                {
                    _silhouetteMat = new Material(Shader.Find("Standard"));
                    _silhouetteMat.color = new Color(0.16f, 0.2f, 0.3f);
                }
                return _silhouetteMat;
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
