using System.Collections.Generic;
using UnityEngine;

namespace NmrgLibrary.NmrgMaskGenerator
{
    public static class MeshIslands
    {
        public static int[] GetIslandIds(int[] triangles)
        {
            if (triangles == null || triangles.Length == 0)
                return new int[0];

            int triangleCount = triangles.Length / 3;
            var parent = new int[triangleCount];
            var size = new int[triangleCount];
            
            for (int i = 0; i < triangleCount; i++)
            {
                parent[i] = i;
                size[i] = 1;
            }

            var vertexToTriangles = new Dictionary<int, List<int>>();
            
            for (int triIndex = 0; triIndex < triangleCount; triIndex++)
            {
                for (int vertIdx = 0; vertIdx < 3; vertIdx++)
                {
                    int vertex = triangles[triIndex * 3 + vertIdx];
                    
                    if (!vertexToTriangles.ContainsKey(vertex))
                        vertexToTriangles[vertex] = new List<int>();
                    
                    vertexToTriangles[vertex].Add(triIndex);
                }
            }
            
            foreach (var triangleList in vertexToTriangles.Values)
            {
                for (int i = 0; i < triangleList.Count - 1; i++)
                {
                    Union(parent, size, triangleList[i], triangleList[i + 1]);
                }
            }
            
            var islandIds = new int[triangleCount];
            var islandMapping = new Dictionary<int, int>();
            int currentIslandId = 0;
            
            for (int i = 0; i < triangleCount; i++)
            {
                int root = Find(parent, i);
                if (!islandMapping.ContainsKey(root))
                {
                    islandMapping[root] = currentIslandId++;
                }
                islandIds[i] = islandMapping[root];
            }
            
            return islandIds;
        }
        
        private static int Find(int[] parent, int x)
        {
            if (parent[x] != x)
            {
                parent[x] = Find(parent, parent[x]);
            }
            return parent[x];
        }
        
        private static void Union(int[] parent, int[] size, int x, int y)
        {
            int rootX = Find(parent, x);
            int rootY = Find(parent, y);
            
            if (rootX == rootY) return;
            
            if (size[rootX] < size[rootY])
            {
                parent[rootX] = rootY;
                size[rootY] += size[rootX];
            }
            else
            {
                parent[rootY] = rootX;
                size[rootX] += size[rootY];
            }
        }
    }
}