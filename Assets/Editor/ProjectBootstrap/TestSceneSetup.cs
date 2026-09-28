using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    // One-time setup: registers the M1 scene in the build scene list so
    // PlayMode tests can SceneManager.LoadScene it headlessly. Invoke:
    //   Unity.exe -batchmode -projectPath <root> -executeMethod ProjectBootstrap.TestSceneSetup.AddM1Scene -quit -logFile -
    public static class TestSceneSetup
    {
        const string M1Scene = "Assets/Scenes/M1_MovementProof.unity";

        public static void AddM1Scene()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == M1Scene))
            {
                scenes.Add(new EditorBuildSettingsScene(M1Scene, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("[TestSceneSetup] Registered " + M1Scene);
            }
            else
            {
                Debug.Log("[TestSceneSetup] Already registered.");
            }
        }
    }
}
