using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Esc.Herd
{
    public class WavesAuthoring : MonoBehaviour
    {
        public List<SheepWaveData> Waves = new List<SheepWaveData>
        {
            new SheepWaveData { WaitTimeBeforeWave = 0f, Amount = 12 },
            new SheepWaveData { WaitTimeBeforeWave = 4f, Amount = 8 }
        };

        public float HalfExtentX = 7f;
        public float HalfExtentZ = 4f;
        public uint Seed = 1;

        [Serializable]
        public class SheepWaveData
        {
            public float WaitTimeBeforeWave;
            public int Amount;
        }

        class WavesBaker : Baker<WavesAuthoring>
        {
            public override void Bake(WavesAuthoring authoring)
            {
                int waveCount = authoring.Waves == null ? 0 : authoring.Waves.Count;
                var builder = new BlobBuilder(Allocator.Temp);
                ref WavesBlob root = ref builder.ConstructRoot<WavesBlob>();
                BlobBuilderArray<WaveBlob> waves = builder.Allocate(ref root.Waves, waveCount);
                for (int i = 0; i < waveCount; i++)
                {
                    SheepWaveData source = authoring.Waves[i];
                    waves[i] = new WaveBlob
                    {
                        WaitTimeBeforeWave = math.max(0f, source.WaitTimeBeforeWave),
                        Amount = math.max(0, source.Amount)
                    };
                }

                BlobAssetReference<WavesBlob> wavesBlob = builder.CreateBlobAssetReference<WavesBlob>(Allocator.Persistent);
                builder.Dispose();
                AddBlobAsset(ref wavesBlob, out _);

                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WavesHolder { WavesReference = wavesBlob });
                AddComponent(entity, new WaveCursor());
                AddComponent(entity, new SpawnArea
                {
                    Center = authoring.transform.position,
                    HalfExtentX = math.max(0f, authoring.HalfExtentX),
                    HalfExtentZ = math.max(0f, authoring.HalfExtentZ)
                });
                AddComponent(entity, new RandomHolder
                {
                    Rng = new Unity.Mathematics.Random(authoring.Seed == 0u ? 1u : authoring.Seed)
                });
                AddBuffer<SheepSpawnPoint>(entity);
            }
        }
    }
}
