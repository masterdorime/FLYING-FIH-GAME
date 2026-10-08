using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectBootstrap
{
    public static class UrpPipelineSetup
    {
        const string SettingsFolder = "Assets/Settings";
        const string AssetPath = "Assets/Settings/UniversalRenderPipelineAsset.asset";

        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(SettingsFolder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            UniversalRenderPipelineAsset urp;
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath) != null)
            {
                urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
                Debug.Log("[UrpPipelineSetup] Asset already exists.");
            }
            else
            {
                urp = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(urp, AssetPath);

                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.AddObjectToAsset(rendererData, urp);

                var so = new SerializedObject(urp);
                var list = so.FindProperty("m_RendererDataList");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("[UrpPipelineSetup] Created URP asset + renderer data.");
            }

            GraphicsSettings.defaultRenderPipeline = urp;

            var quality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(quality, true);

            AssetDatabase.SaveAssets();
            Debug.Log("[UrpPipelineSetup] Assigned to GraphicsSettings.defaultRenderPipeline and all Quality levels.");
            EditorApplication.Exit(0);
        }
    }
}
