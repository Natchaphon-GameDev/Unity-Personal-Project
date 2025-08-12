using UnityEngine;

namespace Scripts
{
    public class WorldGeneration : MonoBehaviour
    {
        public int chunkSize = 16; // Size of each chunk in blocks (16x16x16)
        public int worldHeight = 128; // Max height of the world in blocks

        public GameObject chunkPrefab;

        private void Start()
        {
            GenerateChunk(0, 0); // Generate a single chunk at position (0, 0)
        }

        private void GenerateChunk(int chunkX, int chunkZ)
        {
            // Calculate the chunk's position in the world
            var chunkPosition = new Vector3(chunkX * chunkSize, 0, chunkZ * chunkSize);

            // Instantiate a new chunk and pass world data to it
            var newChunk = Instantiate(chunkPrefab, chunkPosition, Quaternion.identity);
            var chunkScript = newChunk.AddComponent<ChunkGenerator>();
            chunkScript.Initialize(chunkSize, worldHeight, chunkPosition);
        }
    }
}