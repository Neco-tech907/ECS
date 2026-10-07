using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Esc.Herd
{
    [Serializable]
    public class WaveEnemy
    {
        public EnemyType EnemyType;
        public int MinAmount = 4;
        public int MaxAmount = 6;
    }

    [Serializable]
    public class EnemiesWave
    {
        public float WaitTimeBeforeWave = 8f;
        public List<WaveEnemy> EnemiesInWave = new List<WaveEnemy>();
    }

    public class WaveAuthoring : MonoBehaviour
    {
        public uint Seed = 12345;
        public List<EnemiesWave> Waves = new List<EnemiesWave>();

        class WaveBaker : Baker<WaveAuthoring>
        {
            public override void Bake(WaveAuthoring authoring)
            {
                List<EnemiesWave> waves = authoring.Waves;
                if (waves == null || waves.Count == 0)
                    waves = DefaultWaves();

                var blobBuilder = new BlobBuilder(Allocator.Temp);
                ref WavesBlob root = ref blobBuilder.ConstructRoot<WavesBlob>();
                BlobBuilderArray<Wave> waveArray = blobBuilder.Allocate(ref root.Waves, waves.Count);
                for (int i = 0; i < waves.Count; i++)
                {
                    waveArray[i].WaitTimeBeforeWave = waves[i].WaitTimeBeforeWave;
                    List<WaveEnemy> enemies = waves[i].EnemiesInWave;
                    int enemyCount = enemies == null ? 0 : enemies.Count;
                    BlobBuilderArray<EnemyInWave> enemyArray = blobBuilder.Allocate(ref waveArray[i].Enemies, enemyCount);
                    for (int j = 0; j < enemyCount; j++)
                    {
                        int min = math.max(0, enemies[j].MinAmount);
                        int max = math.max(min, enemies[j].MaxAmount);
                        enemyArray[j] = new EnemyInWave
                        {
                            EnemyType = enemies[j].EnemyType,
                            MinAmount = min,
                            MaxAmount = max
                        };
                    }
                }

                float firstWait = waves[0].WaitTimeBeforeWave;
                BlobAssetReference<WavesBlob> wavesReference = blobBuilder.CreateBlobAssetReference<WavesBlob>(Allocator.Persistent);
                blobBuilder.Dispose();
                AddBlobAsset(ref wavesReference, out _);

                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WavesHolder
                {
                    WavesReference = wavesReference
                });
                AddComponent(entity, new WaveSpawner
                {
                    TimeUntilWave = firstWait,
                    NextWave = 0,
                    Random = Unity.Mathematics.Random.CreateFromIndex(authoring.Seed == 0 ? 1u : authoring.Seed)
                });
            }

            static List<EnemiesWave> DefaultWaves()
            {
                var wolf = new WaveEnemy { EnemyType = EnemyType.Wolf, MinAmount = 4, MaxAmount = 6 };
                return new List<EnemiesWave>
                {
                    new EnemiesWave
                    {
                        WaitTimeBeforeWave = 0.4f,
                        EnemiesInWave = new List<WaveEnemy> { wolf }
                    },
                    new EnemiesWave
                    {
                        WaitTimeBeforeWave = 8f,
                        EnemiesInWave = new List<WaveEnemy> { wolf }
                    },
                    new EnemiesWave
                    {
                        WaitTimeBeforeWave = 8f,
                        EnemiesInWave = new List<WaveEnemy> { wolf }
                    }
                };
            }
        }
    }
}
