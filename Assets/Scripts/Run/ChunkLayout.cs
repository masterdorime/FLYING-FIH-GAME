using System.Collections.Generic;
using UnityEngine;

namespace FlyingFishMomentum.Run
{
    // Phase (i): showcase chunks. A ChunkLayout hand-places one chunk's
    // obstacles; ChunkSpec carries it optionally (null = procedural).
    // Positions are relative to chunk origin (zStart added at build, y
    // absolute — height is gameplay here, so Y is NOT locked). Shells
    // resolve by prefab name against the spec's skin sets (names survive
    // pipeline regenerations; fileIDs do not). Coins derive from placed
    // rings with the standard trail code on the layout-fixed Seed, so a
    // layout streams identically every time. Decor stays procedural.
    // Validate() is the ship-gate (tool runs it); the builder additionally
    // skips invalid entries with an error so bad data never crashes.
    [CreateAssetMenu(fileName = "ChunkLayout", menuName = "FlyingFish/Chunk Layout")]
    public class ChunkLayout : ScriptableObject
    {
        public const int SchemaVersion = 1;

        // Caps mirror the largest procedural specs (validation bounds,
        // same role as ChunkBuilder's PRD safety-pin consts).
        public const int MaxRings = 10;
        public const int MaxIslands = 10;
        public const int MaxSpires = 8;
        public const int MaxArches = 5;
        public const float MaxLaneX = 20f;
        public const float MinY = -15f;
        public const float MaxY = 80f;

        public enum ShellKind { Island, Spire, Pillar, Lintel }

        [System.Serializable]
        public struct RingEntry { public string Id; public Vector3 Position; }

        [System.Serializable]
        public struct IslandEntry
        {
            public string Id;
            public Vector3 Position;
            public Vector3 Scale;
            public string ShellName;
        }

        [System.Serializable]
        public struct SpireEntry
        {
            public string Id;
            public Vector3 Position;
            public float Height;
            public string ShellName;
        }

        [System.Serializable]
        public struct ArchEntry
        {
            public string Id;
            public Vector3 Anchor;
            public string PillarShell;
            public string LintelShell;
        }

        public int Version = SchemaVersion;
        public string LayoutId;
        public int Seed = 1;
        public RingEntry[] Rings;
        public IslandEntry[] Islands;
        public SpireEntry[] Spires;
        public ArchEntry[] Arches;

        // Shell lookup shared by Validate and the builder: per-kind skin
        // set matched by prefab name (null when missing/unresolved).
        public static GameObject ResolveShell(ChunkSpec spec, ShellKind kind, string name)
        {
            if (spec == null || spec.Skin == null || string.IsNullOrEmpty(name)) return null;
            var skin = spec.Skin;
            GameObject[] primary = null, fallback = null;
            switch (kind)
            {
                case ShellKind.Island: primary = skin.IslandPrefabs; break;
                case ShellKind.Spire: primary = skin.SpirePrefabs; break;
                case ShellKind.Pillar:
                    primary = skin.ArchPillarPrefabs; fallback = skin.ArchPrefabs; break;
                case ShellKind.Lintel:
                    primary = skin.ArchLintelPrefabs; fallback = skin.ArchPrefabs; break;
            }
            var found = FindByName(primary, name) ?? FindByName(fallback, name);
            return found;
        }

        private static GameObject FindByName(GameObject[] set, string name)
        {
            if (set == null || string.IsNullOrEmpty(name)) return null;
            foreach (var p in set)
                if (p != null && p.name == name) return p;
            return null;
        }

        // Ship-gate: schema, finite values, unique IDs, bounds, resolvable
        // shells, and ring clearance (no rock volume may seal a prompt).
        // Pure function of layout + spec (skin sets, Length): testable.
        public List<string> Validate(ChunkSpec spec)
        {
            var errors = new List<string>();
            if (Version != SchemaVersion)
                errors.Add("schema " + Version + " != " + SchemaVersion);
            if (string.IsNullOrEmpty(LayoutId))
                errors.Add("missing LayoutId");
            if (spec == null) { errors.Add("no spec"); return errors; }
            var rings = Rings ?? new RingEntry[0];
            var islands = Islands ?? new IslandEntry[0];
            var spires = Spires ?? new SpireEntry[0];
            var arches = Arches ?? new ArchEntry[0];
            if (rings.Length > MaxRings) errors.Add("rings " + rings.Length + " > " + MaxRings);
            if (islands.Length > MaxIslands) errors.Add("islands " + islands.Length + " > " + MaxIslands);
            if (spires.Length > MaxSpires) errors.Add("spires " + spires.Length + " > " + MaxSpires);
            if (arches.Length > MaxArches) errors.Add("arches " + arches.Length + " > " + MaxArches);
            var ids = new HashSet<string>();
            foreach (var r in rings) CheckId(ids, r.Id, "ring", errors);
            foreach (var e in islands) CheckId(ids, e.Id, "island", errors);
            foreach (var e in spires) CheckId(ids, e.Id, "spire", errors);
            foreach (var e in arches) CheckId(ids, e.Id, "arch", errors);
            foreach (var r in rings) CheckPos(r.Id, "ring", r.Position, spec, errors);
            var rocks = new List<KeyValuePair<string, Bounds>>();
            foreach (var e in islands)
            {
                CheckPos(e.Id, "island", e.Position, spec, errors);
                CheckScale(e.Id, e.Scale, errors);
                CheckShell(spec, ShellKind.Island, e.ShellName, e.Id, errors);
                rocks.Add(new KeyValuePair<string, Bounds>("island '" + e.Id + "'",
                    new Bounds(e.Position, e.Scale)));
            }
            foreach (var e in spires)
            {
                CheckPos(e.Id, "spire", e.Position, spec, errors);
                if (!(e.Height >= 1f) || e.Height > 100f)
                    errors.Add("spire '" + e.Id + "' height out of range: " + e.Height);
                CheckShell(spec, ShellKind.Spire, e.ShellName, e.Id, errors);
                rocks.Add(new KeyValuePair<string, Bounds>("spire '" + e.Id + "'",
                    new Bounds(e.Position, new Vector3(8f, e.Height, 8f))));
            }
            foreach (var e in arches)
            {
                CheckPos(e.Id, "arch", e.Anchor, spec, errors);
                // The anchor is only the gate center: pillars land ±8x
                // and the lintel +12y, so validate the built volumes —
                // an in-bounds anchor can still stage cubes out of band.
                CheckPos(e.Id + ".pillar", "arch", e.Anchor + new Vector3(-8f, 0f, 0f), spec, errors);
                CheckPos(e.Id + ".pillar", "arch", e.Anchor + new Vector3(8f, 0f, 0f), spec, errors);
                CheckPos(e.Id + ".lintel", "arch", e.Anchor + new Vector3(0f, 11.5f, 0f), spec, errors);
                CheckShell(spec, ShellKind.Pillar, e.PillarShell, e.Id, errors);
                CheckShell(spec, ShellKind.Lintel, e.LintelShell, e.Id, errors);
                rocks.Add(new KeyValuePair<string, Bounds>("arch '" + e.Id + "' pillar",
                    new Bounds(e.Anchor + new Vector3(-8f, 0f, 0f), new Vector3(6f, 18f, 6f))));
                rocks.Add(new KeyValuePair<string, Bounds>("arch '" + e.Id + "' pillar",
                    new Bounds(e.Anchor + new Vector3(8f, 0f, 0f), new Vector3(6f, 18f, 6f))));
                rocks.Add(new KeyValuePair<string, Bounds>("arch '" + e.Id + "' lintel",
                    new Bounds(e.Anchor + new Vector3(0f, 11.5f, 0f), new Vector3(20f, 5f, 5f))));
            }
            foreach (var r in rings)
                foreach (var rock in rocks)
                    if (DiscHitsBox(r.Position, TimingPromptSpawner.RingPromptRadius, rock.Value))
                        errors.Add("ring '" + r.Id + "' sealed by " + rock.Key);
            return errors;
        }

        private static void CheckId(HashSet<string> ids, string id, string kind, List<string> errors)
        {
            if (string.IsNullOrEmpty(id)) errors.Add(kind + " entry missing id");
            else if (!ids.Add(id)) errors.Add("duplicate id '" + id + "'");
        }

        private static void CheckPos(string id, string kind, Vector3 p, ChunkSpec spec, List<string> errors)
        {
            if (!IsFinite(p)) { errors.Add(kind + " '" + id + "' non-finite position"); return; }
            if (Mathf.Abs(p.x) > MaxLaneX) errors.Add(kind + " '" + id + "' x out of lane: " + p.x);
            if (p.y < MinY || p.y > MaxY) errors.Add(kind + " '" + id + "' y out of band: " + p.y);
            if (p.z < 0f || p.z > spec.Length) errors.Add(kind + " '" + id + "' z outside chunk: " + p.z);
        }

        private static void CheckScale(string id, Vector3 s, List<string> errors)
        {
            if (!IsFinite(s) || s.x < 0.1f || s.y < 0.1f || s.z < 0.1f
                || s.x > 50f || s.y > 50f || s.z > 50f)
                errors.Add("island '" + id + "' scale out of range: " + s);
        }

        private static void CheckShell(ChunkSpec spec, ShellKind kind, string name, string id, List<string> errors)
        {
            if (ResolveShell(spec, kind, name) == null)
                errors.Add("'" + id + "' shell '" + name + "' does not resolve in " + kind + " set");
        }

        // Shared finite check (builder reuses it for its crash-guard subset).
        public static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        // Ring disc (XY circle at ring z) vs rock AABB. The swept judging
        // window extends ±2m in z (see SegmentPassesDisc), so rocks near
        // the plane count too. Touching the edge passes (strict <).
        private static bool DiscHitsBox(Vector3 ring, float radius, Bounds rock)
        {
            if (ring.z < rock.min.z - 2f || ring.z > rock.max.z + 2f) return false;
            float cx = Mathf.Clamp(ring.x, rock.min.x, rock.max.x);
            float cy = Mathf.Clamp(ring.y, rock.min.y, rock.max.y);
            float dx = ring.x - cx, dy = ring.y - cy;
            return dx * dx + dy * dy < radius * radius;
        }
    }
}
