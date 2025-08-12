using UnityEngine;

namespace Scripts
{
    public class VoxelData
    {
        // The 8 vertices of a cube
        private static readonly Vector3[] Vertices = {
            new Vector3(0, 0, 0), // 0
            new Vector3(1, 0, 0), // 1
            new Vector3(1, 1, 0), // 2
            new Vector3(0, 1, 0), // 3
            new Vector3(0, 0, 1), // 4
            new Vector3(0, 1, 1), // 5
            new Vector3(1, 1, 1), // 6
            new Vector3(1, 0, 1)  // 7
        };

        // Defines the 2 triangles for each of the 6 faces
        // The numbers correspond to indices in the vertices array
        private static readonly int[,] Triangles = {
            {3, 2, 6, 5}, // Up face
            {1, 0, 4, 7}, // Down face
            {4, 5, 6, 7}, // Forward face
            {0, 1, 2, 3}, // Back face
            {1, 7, 6, 2}, // Right face
            {0, 3, 5, 4}  // Left face
        };

        public static Vector3[] FaceVertices(Direction dir, Vector3 pos)
        {
            var fv = new Vector3[4];
            for (var i = 0; i < 4; i++)
            {
                fv[i] = Vertices[Triangles[(int)dir, i]] + pos;
            }
            return fv;
        }

    }
}