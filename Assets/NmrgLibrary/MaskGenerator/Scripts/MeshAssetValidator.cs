using UnityEngine;
using UnityEditor;

namespace NmrgLibrary.NmrgMaskGenerator
{
    public static class MeshAssetValidator
    {
        public static bool IsMeshReadable(Mesh mesh)
        {
            if (mesh == null) return false;
            return mesh.isReadable;
        }
        
        public static bool CanFixMeshReadability(Mesh mesh)
        {
            if (mesh == null) return false;
            
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(assetPath)) return false;
            
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            return importer != null;
        }
        
        public static bool FixMeshReadability(Mesh mesh)
        {
            if (mesh == null) return false;
            
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(assetPath)) return false;
            
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return false;
            
            if (importer.isReadable) return true;
            
            importer.isReadable = true;
            
            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to fix mesh readability: {ex.Message}");
                return false;
            }
        }
        
        public static string GetMeshAssetPath(Mesh mesh)
        {
            if (mesh == null) return null;
            return AssetDatabase.GetAssetPath(mesh);
        }
    }
}