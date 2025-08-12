using System.Collections.Generic;
using UnityEngine;

namespace Scripts
{
    // Enum to represent directions
    public enum Direction { Up, Down, Forward, Back, Right, Left }
    
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class ChunkGenerator : MonoBehaviour
    {
        private List<Vector3> _vertices = new List<Vector3>();
        private List<int> _triangles = new List<int>();
        private int _vertexIndex = 0;

        private int _chunkSize;
        private int _worldHeight;
        private Vector3 _chunkPosition;

        // Call this from the WorldGenerator script
        public void Initialize(int size, int height, Vector3 position)
        {
            _chunkSize = size;
            _worldHeight = height;
            _chunkPosition = position;
            GenerateVoxelData();
            CreateMesh();
        }

        private void GenerateVoxelData()
        {
            // Loop through every potential block position in this chunk
            for (var x = 0; x < _chunkSize; x++)
            {
                for (var z = 0; z < _chunkSize; z++)
                {
                    // Calculate the world position of this column
                    var worldX = _chunkPosition.x + x;
                    var worldZ = _chunkPosition.z + z;

                    // Use Perlin noise to get the surface height
                    // We use a "scale" to control the frequency of hills/valleys
                    const float noiseScale = 0.05f;
                    var surfaceHeight =
                        Mathf.FloorToInt(Mathf.PerlinNoise(worldX * noiseScale, worldZ * noiseScale) * 30f + 20f);

                    for (var y = 0; y < _worldHeight; y++)
                    {
                        // If the current block is at or below the surface, create it
                        if (y > surfaceHeight) continue;
                        // Check neighbors to decide if a face should be drawn
                        // This is a key optimization: only draw visible faces!
                        if (y == surfaceHeight || IsBlockTransparent(x, y + 1, z))
                            CreateFace(Direction.Up, x, y, z);
                        if (y == 0 || IsBlockTransparent(x, y - 1, z)) CreateFace(Direction.Down, x, y, z);
                        if (IsBlockTransparent(x, y, z + 1)) CreateFace(Direction.Forward, x, y, z);
                        if (IsBlockTransparent(x, y, z - 1)) CreateFace(Direction.Back, x, y, z);
                        if (IsBlockTransparent(x + 1, y, z)) CreateFace(Direction.Right, x, y, z);
                        if (IsBlockTransparent(x - 1, y, z)) CreateFace(Direction.Left, x, y, z);
                    }
                }
            }
        }

        // This function will be expanded later for things like air, water, etc.
        // For now, it just checks if a block is above the surface height.
        private bool IsBlockTransparent(int x, int y, int z)
        {
            // Check chunk boundaries
            if (y < 0 || y >= _worldHeight) return true;

            // For simplicity, we recalculate noise. A better way is to generate
            // and store all block data for this chunk and its neighbors first.
            var worldX = _chunkPosition.x + x;
            var worldZ = _chunkPosition.z + z;
            const float noiseScale = 0.05f;
            var surfaceHeight = Mathf.FloorToInt(Mathf.PerlinNoise(worldX * noiseScale, worldZ * noiseScale) * 30f + 20f);

            return y > surfaceHeight;
        }

        private void CreateMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = _vertices.ToArray();
            mesh.triangles = _triangles.ToArray();

            mesh.RecalculateNormals(); // Important for lighting

            GetComponent<MeshFilter>().mesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        private void CreateFace(Direction dir, int x, int y, int z)
        {
            _vertices.AddRange(VoxelData.FaceVertices(dir, new Vector3(x, y, z)));
            _triangles.Add(_vertexIndex + 0);
            _triangles.Add(_vertexIndex + 1);
            _triangles.Add(_vertexIndex + 2);
            _triangles.Add(_vertexIndex + 2);
            _triangles.Add(_vertexIndex + 1);
            _triangles.Add(_vertexIndex + 3);
            _vertexIndex += 4;
        }
    }
}