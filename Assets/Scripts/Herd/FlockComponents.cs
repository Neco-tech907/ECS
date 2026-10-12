using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Esc.Herd
{
    public struct WavesBlob
    {
        public BlobArray<WaveBlob> Waves;
    }

    public struct WaveBlob
    {
        public float WaitTimeBeforeWave;
        public int Amount;
    }

    public struct WavesHolder : IComponentData
    {
        public BlobAssetReference<WavesBlob> WavesReference;
    }

    public struct WaveCursor : IComponentData
    {
        public int NextWave;
        public float Waited;
        public bool Finished;
    }

    public struct SpawnArea : IComponentData
    {
        public float3 Center;
        public float HalfExtentX;
        public float HalfExtentZ;
    }

    public struct RandomHolder : IComponentData
    {
        public Random Rng;
    }

    public struct PrefabsHolder : IComponentData
    {
        public Entity SheepPrefab;
    }

    [InternalBufferCapacity(32)]
    public struct SheepSpawnPoint : IBufferElementData
    {
        public float3 Position;
    }
}
