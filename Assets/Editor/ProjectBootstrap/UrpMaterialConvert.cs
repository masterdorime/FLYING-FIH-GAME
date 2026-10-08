using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class UrpMaterialConvert
    {
        public static void Convert()
        {
            UnityEditor.Rendering.Universal.Converters.RunInBatchMode(
                "BuiltInToURP",
                new List<string> { "Material", "ReadonlyMaterial" },
                isInclusive: true);
            AssetDatabase.SaveAssets();
            Debug.Log("[UrpMaterialConvert] Material + ReadonlyMaterial converters done.");
            EditorApplication.Exit(0);
        }
    }
}
