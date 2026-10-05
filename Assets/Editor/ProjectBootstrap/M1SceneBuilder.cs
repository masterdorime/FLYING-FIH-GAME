using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FlyingFishMomentum;
using FlyingFishMomentum.Run;
using FlyingFishMomentum.Scoring;

namespace ProjectBootstrap
{
    // Builds the M1_MovementProof scene + prefabs headlessly. Invoke:
    //   Unity.exe -batchmode -projectPath <root> -executeMethod ProjectBootstrap.M1SceneBuilder.Build -quit -logFile -
    // Synchronous work: -quit is safe. Throws (non-zero exit) on missing configs.
    // Verified by its output: the scene file exists and the Task 5 PlayMode suite goes green.
    public static class M1SceneBuilder
    {
        public static void Build()
        {
            var profiles = new List<FlightTierProfile>
            {
                Load<FlightTierProfile>("Assets/Configs/TierProfile_None.asset"),
                Load<FlightTierProfile>("Assets/Configs/TierProfile_Low.asset"),
                Load<FlightTierProfile>("Assets/Configs/TierProfile_Medium.asset"),
                Load<FlightTierProfile>("Assets/Configs/TierProfile_High.asset"),
                Load<FlightTierProfile>("Assets/Configs/TierProfile_Max.asset"),
            };
            var momSettings = Load<MomentumSettings>("Assets/Configs/MomentumSettings.asset");
            var timingSettings = Load<TimingSettings>("Assets/Configs/TimingSettings.asset");
            var camSettings = Load<CameraSettings>("Assets/Configs/CameraSettings.asset");
            var gaugeSettings = Load<FlightGaugeSettings>("Assets/Configs/FlightGaugeSettings.asset");
            var scoringSettings = Load<ScoringSettings>("Assets/Configs/ScoringSettings.asset");
            // M4 Task 7: RunManager owns content past spawn — the type deck
            // and difficulty ride the scene (missing files fail the build).
            var difficulty = Load<DifficultySettings>("Assets/Configs/DifficultySettings.asset");
            var lagoonSpec = Load<ChunkSpec>("Assets/Configs/ChunkSpec_Lagoon.asset");
            var gauntletSpec = Load<ChunkSpec>("Assets/Configs/ChunkSpec_Gauntlet.asset");
            var stormSpec = Load<ChunkSpec>("Assets/Configs/ChunkSpec_Storm.asset");
            var skySpec = Load<ChunkSpec>("Assets/Configs/ChunkSpec_Sky.asset");

            var fishMat = new Material(Shader.Find("Standard"));
            fishMat.color = new Color(1f, 0.45f, 0.1f);
            AssetDatabase.CreateAsset(fishMat, "Assets/Materials/M1Fish.mat");

            var waterMat = new Material(Shader.Find("Standard"));
            waterMat.color = new Color(0.1f, 0.4f, 0.8f, 0.6f);
            waterMat.SetFloat("_Mode", 3f);
            waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            waterMat.SetInt("_ZWrite", 0);
            waterMat.DisableKeyword("_ALPHATEST_ON");
            waterMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            waterMat.SetOverrideTag("RenderType", "Transparent");
            waterMat.renderQueue = 3000;
            AssetDatabase.CreateAsset(waterMat, "Assets/Materials/M1Water.mat");

            var sandMat = new Material(Shader.Find("Standard"));
            sandMat.color = new Color(0.85f, 0.75f, 0.5f);
            AssetDatabase.CreateAsset(sandMat, "Assets/Materials/M1Sand.mat");

            var rockMat = new Material(Shader.Find("Standard"));
            rockMat.color = new Color(0.4f, 0.42f, 0.45f);

            var coinMat = new Material(Shader.Find("Standard"));
            coinMat.color = new Color(1f, 0.75f, 0.15f);
            coinMat.SetFloat("_Metallic", 0.85f);
            coinMat.SetFloat("_Glossiness", 0.55f);
            AssetDatabase.CreateAsset(coinMat, "Assets/Materials/M1Coin.mat");
            AssetDatabase.CreateAsset(rockMat, "Assets/Materials/M1Rock.mat");

            // M4 Task 6 mood: shared sky-tint instance (water keeps the
            // shared M1Water.mat instance above). ChunkBuilder re-tints
            // these per chunk type at runtime; Task 7 wires the scene.
            // Front faces culled: the sky shell is viewed from inside.
            var skyMat = new Material(Shader.Find("Standard"));
            skyMat.color = new Color(0.53f, 0.81f, 0.92f); // Lagoon starter
            skyMat.SetInt("_Cull", 1);
            AssetDatabase.CreateAsset(skyMat, "Assets/Materials/M1Sky.mat");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Mood defaults match the Lagoon starter (fog setup rides the
            // scene; per-type switches happen in ChunkBuilder.ApplyMood).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.75f, 0.87f, 0.93f);
            RenderSettings.fogDensity = 0.002f;
            RenderSettings.ambientLight = new Color(0.53f, 0.81f, 0.92f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(-50f, -30f, 0f);

            // M4 Task 7: water/seabed are 4+4 recycled 1000m segments
            // (span z 0..4000); ChunkBuilder repositions them ahead at
            // runtime. Water stays visual-only (no MeshCollider).
            var waterSegs = new List<GameObject>();
            var seabedSegs = new List<GameObject>();
            for (int i = 0; i < 4; i++)
            {
                var wseg = GameObject.CreatePrimitive(PrimitiveType.Plane);
                wseg.name = "Water_" + i;
                wseg.transform.position = new Vector3(0f, 0f, 500f + i * 1000f);
                wseg.transform.localScale = new Vector3(40f, 1f, 100f);
                Object.DestroyImmediate(wseg.GetComponent<MeshCollider>());
                wseg.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
                waterSegs.Add(wseg);
                // Seabed: thin box (BoxColliders stay exact under
                // non-uniform scale; scaled MeshColliders misbehave).
                var sseg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sseg.name = "Seabed_" + i;
                sseg.transform.position = new Vector3(0f, -12.5f, 500f + i * 1000f);
                sseg.transform.localScale = new Vector3(400f, 1f, 1000f);
                sseg.GetComponent<MeshRenderer>().sharedMaterial = sandMat;
                seabedSegs.Add(sseg);
            }

            // M4 Task 7 (Task 6 review): sky tint rides a real renderer.
            // Inside-out shell (front faces culled) so the mood tint
            // surrounds the fish; RunManager slides it along z to follow.
            var skyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skyGo.name = "Sky";
            Object.DestroyImmediate(skyGo.GetComponent<SphereCollider>());
            skyGo.transform.position = new Vector3(0f, 0f, 2000f);
            skyGo.transform.localScale = new Vector3(3000f, 3000f, 3000f);
            var skyRend = skyGo.GetComponent<MeshRenderer>();
            skyRend.sharedMaterial = skyMat;
            skyRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skyRend.receiveShadows = false;

            // Slalom corridor z=30..90, inner gap 20 (turn radius at Low ≈ 9.4).
            // Open water beyond z=120 for high-speed runs (radius at Max ≈ 20.9).
            AddIsland("Island_West_1", new Vector3(-15f, 5f, 30f), rockMat);
            AddIsland("Island_East_1", new Vector3(15f, 5f, 50f), rockMat);
            AddIsland("Island_West_2", new Vector3(-15f, 5f, 70f), rockMat);
            AddIsland("Island_East_2", new Vector3(15f, 5f, 90f), rockMat);

            // PlayerRoot.
            var player = new GameObject("PlayerRoot");
            player.transform.position = new Vector3(0f, -3f, 0f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.skinWidth = 0.08f;
            var momentum = player.AddComponent<PlayerMomentumController>();
            var sm = player.AddComponent<FlightStateMachine>();
            var movement = player.AddComponent<PlayerMovementController>();
            momentum.Configure(momSettings, timingSettings);
            sm.Configure(profiles, momentum, momSettings);
            movement.Configure(sm, momentum, momSettings);
            AddFishPart(player.transform, "Body", PrimitiveType.Capsule,
                new Vector3(0f, 0f, 0.1f), Vector3.one, new Vector3(90f, 0f, 0f), fishMat);
            AddFishPart(player.transform, "Nose", PrimitiveType.Sphere,
                new Vector3(0f, 0f, 1.2f), new Vector3(0.9f, 0.9f, 1.1f), Vector3.zero, fishMat);
            AddFishPart(player.transform, "TailFin", PrimitiveType.Cube,
                new Vector3(0f, 0f, -1.2f), new Vector3(0.15f, 1.4f, 0.9f), Vector3.zero, fishMat);
            AddFishPart(player.transform, "DorsalFin", PrimitiveType.Cube,
                new Vector3(0f, 0.7f, -0.1f), new Vector3(0.15f, 0.7f, 0.7f), Vector3.zero, fishMat);

            // CameraRig.
            var rig = new GameObject("CameraRig");
            rig.tag = "MainCamera"; // Camera.main billboarding (dial) depends on this
            rig.transform.position = new Vector3(0f, 2f, -12f);
            var rigCam = rig.AddComponent<Camera>();
            rigCam.farClipPlane = 4000f; // 2x runway stays visible
            var reactor = rig.AddComponent<CameraSpeedReactor>();
            reactor.Configure(camSettings, momSettings, momentum, sm, movement, player.transform);
            rig.transform.LookAt(player.transform.position);

            // Debug scaffold.
            var debug = new GameObject("M1Debug");
            var dbgInput = debug.AddComponent<M1DebugInput>();
            dbgInput.Configure(sm, reactor, movement, timingSettings);
            // Timing scaffold (M2).
            var timingGo = new GameObject("Timing");
            var spawner = timingGo.AddComponent<TimingPromptSpawner>();
            var gaugeGo = new GameObject("Gauge");
            gaugeGo.transform.SetParent(timingGo.transform, false);
            var gauge = gaugeGo.AddComponent<FlightGaugeSystem>();
            gauge.Configure(sm, gaugeSettings, reactor, movement);
            spawner.Configure(movement, momentum, sm, timingSettings, momSettings, reactor, gauge);
            var ringGo = new GameObject("PromptDial");
            ringGo.transform.SetParent(timingGo.transform, false);
            var ring = ringGo.AddComponent<TimingPromptDial>();
            ring.Configure(spawner, player.transform, momentum);
            // M4 Task 7: no baked rings/coins — RunManager builds the
            // deterministic Lagoonx2 starter at Start and streams every
            // chunk after it, feeding the spawner wholesale.
            // Score system (goal layer): spawner pushes beats/distance/coins.
            var scoringGo = new GameObject("Scoring");
            var score = scoringGo.AddComponent<ScoreSystem>();
            score.Configure(sm, scoringSettings);
            spawner.SetScoreSystem(score);
            var barGo = new GameObject("ChargeBar");
            barGo.transform.SetParent(timingGo.transform, false);
            var bar = barGo.AddComponent<ChargeBar>();
            bar.Configure(spawner, player.transform);
            // M4 Task 7: endless-run director. Configure once at wiring
            // (never per chunk — Task 4 transient clone leak); Start
            // builds the starter, Update streams + creeps + feeds.
            var runGo = new GameObject("GameManager");
            var builder = runGo.AddComponent<ChunkBuilder>();
            var runManager = runGo.AddComponent<RunManager>();
            runManager.Configure(spawner, score, sm, difficulty,
                new List<ChunkSpec> { lagoonSpec, gauntletSpec, stormSpec, skySpec });
            runManager.WireScene(builder, momentum);
            builder.Configure(waterSegs, seabedSegs, skyRend);
            var overlay = debug.AddComponent<M1DebugOverlay>();
            overlay.Configure(momentum, sm, momSettings, movement, spawner, gauge, score, runManager);

            PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/PlayerRoot.prefab");
            PrefabUtility.SaveAsPrefabAsset(rig, "Assets/Prefabs/CameraRig.prefab");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/M1_MovementProof.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[M1SceneBuilder] Scene + prefabs saved.");
        }

        static void AddIsland(string name, Vector3 center, Material mat)
        {
            var island = GameObject.CreatePrimitive(PrimitiveType.Cube);
            island.name = name;
            island.transform.position = center;
            island.transform.localScale = new Vector3(10f, 25f, 10f);
            island.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void AddFishPart(Transform parent, string name, PrimitiveType type,
            Vector3 localPos, Vector3 localScale, Vector3 localEuler, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            // Visuals only: the root CharacterController is the single collider.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new System.Exception("[M1SceneBuilder] Missing config: " + path);
            return asset;
        }
    }
}
