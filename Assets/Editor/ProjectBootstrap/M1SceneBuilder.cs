using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FlyingFishMomentum;

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
            AssetDatabase.CreateAsset(rockMat, "Assets/Materials/M1Rock.mat");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(-50f, -30f, 0f);

            // Water: visual only — MeshCollider removed so breaches pass through.
            // Long runway (span z -100..1500) for M2 timing play at speed.
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            water.transform.position = new Vector3(0f, 0f, 700f);
            water.transform.localScale = new Vector3(40f, 1f, 160f);
            Object.DestroyImmediate(water.GetComponent<MeshCollider>());
            water.GetComponent<MeshRenderer>().sharedMaterial = waterMat;

            // Seabed: thin box (BoxColliders stay exact under non-uniform scale;
            // scaled MeshColliders misbehave).
            var seabed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seabed.name = "Seabed";
            seabed.transform.position = new Vector3(0f, -12.5f, 700f);
            seabed.transform.localScale = new Vector3(400f, 1f, 1600f);
            seabed.GetComponent<MeshRenderer>().sharedMaterial = sandMat;

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
            rigCam.farClipPlane = 2000f; // long M2 runway stays visible
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
            spawner.Configure(movement, momentum, sm, timingSettings, momSettings, reactor);
            var ringGo = new GameObject("PromptDial");
            ringGo.transform.SetParent(timingGo.transform, false);
            var ring = ringGo.AddComponent<TimingPromptDial>();
            ring.Configure(spawner, player.transform, momentum);
            // Charge rings (M2): fixed swim/fly alternation, deterministic builds.
            var rings = new List<ChargeRing>
            {
                AddChargeRing("ChargeRing_1", new Vector3(-15f, -3f, 150f)),
                AddChargeRing("ChargeRing_2", new Vector3(15f, 10f, 400f)),
                AddChargeRing("ChargeRing_3", new Vector3(0f, -3f, 650f)),
                AddChargeRing("ChargeRing_4", new Vector3(-15f, 10f, 900f)),
                AddChargeRing("ChargeRing_5", new Vector3(15f, -3f, 1200f)),
            };
            spawner.SetRings(rings);
            var barGo = new GameObject("ChargeBar");
            barGo.transform.SetParent(timingGo.transform, false);
            var bar = barGo.AddComponent<ChargeBar>();
            bar.Configure(spawner, player.transform);
            var overlay = debug.AddComponent<M1DebugOverlay>();
            overlay.Configure(momentum, sm, momSettings, movement, spawner);

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

        static ChargeRing AddChargeRing(string name, Vector3 center)
        {
            var go = new GameObject(name);
            go.transform.position = center;
            var line = go.AddComponent<LineRenderer>();
            const int points = 49;
            const float radius = 3f;
            line.positionCount = points;
            line.useWorldSpace = false;
            line.startWidth = 0.25f;
            line.endWidth = 0.25f;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 0.85f, 0.2f);
            mat.renderQueue = 3000;
            line.material = mat;
            for (int i = 0; i < points; i++)
            {
                float d = Mathf.Deg2Rad * 360f * i / (points - 1);
                line.SetPosition(i, new Vector3(radius * Mathf.Sin(d), radius * Mathf.Cos(d), 0f));
            }
            return go.AddComponent<ChargeRing>();
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
