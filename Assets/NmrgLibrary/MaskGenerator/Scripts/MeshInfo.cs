using System.Linq;
using UnityEngine;

namespace NmrgLibrary.NmrgMaskGenerator
{
    public class MeshInfo
    {
        public readonly Vector3[] positions;
        public readonly Vector3[] normals;
        public readonly Vector2[] uvs;
        public readonly int[] triangles;
        public readonly int[] islandIds;
        public readonly int vertexCount;
        
        public MeshInfo(Mesh mesh)
        {
            if (mesh == null)
                throw new System.ArgumentNullException(nameof(mesh));
                
            vertexCount = mesh.vertexCount;
            positions = mesh.vertices;
            normals = mesh.normals.Length > 0 ? 
                mesh.normals : 
                Enumerable.Repeat(Vector3.up, vertexCount).ToArray();
            uvs = mesh.uv.Length > 0 ? 
                mesh.uv : 
                Enumerable.Repeat(Vector2.zero, vertexCount).ToArray();
            triangles = mesh.triangles;
            islandIds = MeshIslands.GetIslandIds(triangles);
        }
        
        public Mesh ToUnityMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            return mesh;
        }
        
    }
}