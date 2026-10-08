using System.IO;
using UnityEditor;
using UnityEngine;
using FlyingFishMomentum.Run;

// Water package task 1: bakes WaterArt painters to committed PNG
// assets with mirrored wrap (provably seamless tiling for any
// content) and refreshes the database. Invoke:
//   unity run . -- -executeMethod WaterArtImport.WriteWaterArt
public static class WaterArtImport
{
    public const string FoamPath = "Assets/Materials/WaterFoam.png";
    public const string PuffPath = "Assets/Materials/WaterPuff.png";

    [MenuItem("FlyingFish/Bake Water Art")]
    public static void WriteWaterArt()
    {
        Write(WaterArt.PaintFoam(256, 7), FoamPath);
        Write(WaterArt.PaintPuff(64), PuffPath);
        AssetDatabase.Refresh();
        Debug.Log("[WaterArtImport] baked foam + puff.");
    }

    private static void Write(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Mirror;
            importer.SaveAndReimport();
        }
    }
}
