using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Esc.Herd
{
    [UpdateBefore(typeof(HerdGoalSyncSystem))]
    public partial struct SheepWaveSpawnSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PrefabsHolder>(out PrefabsHolder prefabs) || prefabs.SheepPrefab == Entity.Null)
                return;

            state.Dependency.Complete();

            ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            if (!transformLookup.TryGetComponent(prefabs.SheepPrefab, out LocalTransform prefabTransform))
                return;

            float deltaTime = SystemAPI.Time.DeltaTime;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (holder, cursor, area, random, points) in SystemAPI.Query<RefRO<WavesHolder>, RefRW<WaveCursor>, RefRO<SpawnArea>, RefRW<RandomHolder>, DynamicBuffer<SheepSpawnPoint>>())
            {
                if (cursor.ValueRO.Finished || !holder.ValueRO.WavesReference.IsCreated)
                    continue;

                ref BlobArray<WaveBlob> waves = ref holder.ValueRO.WavesReference.Value.Waves;
                if (cursor.ValueRO.NextWave >= waves.Length)
                {
                    cursor.ValueRW.Finished = true;
                    continue;
                }

                float waited = cursor.ValueRO.Waited + deltaTime;
                WaveBlob wave = waves[cursor.ValueRO.NextWave];
                if (waited < wave.WaitTimeBeforeWave)
                {
                    cursor.ValueRW.Waited = waited;
                    continue;
                }

                int nextWave = cursor.ValueRO.NextWave + 1;
                cursor.ValueRW.Waited = 0f;
                cursor.ValueRW.NextWave = nextWave;
                cursor.ValueRW.Finished = nextWave >= waves.Length;

                points.Clear();
                ref Random rng = ref random.ValueRW.Rng;
                for (int i = 0; i < wave.Amount; i++)
                {
                    float x = area.ValueRO.Center.x + rng.NextFloat(-area.ValueRO.HalfExtentX, area.ValueRO.HalfExtentX);
                    float z = area.ValueRO.Center.z + rng.NextFloat(-area.ValueRO.HalfExtentZ, area.ValueRO.HalfExtentZ);
                    points.Add(new SheepSpawnPoint { Position = new float3(x, area.ValueRO.Center.y, z) });
                }

                for (int i = 0; i < points.Length; i++)
                {
                    Entity sheep = ecb.Instantiate(prefabs.SheepPrefab);
                    LocalTransform spawnedTransform = prefabTransform;
                    spawnedTransform.Position = points[i].Position;
                    ecb.SetComponent(sheep, spawnedTransform);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(HealthSystem))]
    public partial struct DeadSheepDestroySystem : ISystem
    {
        private EntityQuery _deadSheepQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _deadSheepQuery = SystemAPI.QueryBuilder()
                .WithAll<SheepTag, Dead>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            var job = new DeadSheepDestroyJob
            {
                Ecb = ecb.AsParallelWriter()
            };
            state.Dependency = job.ScheduleParallel(_deadSheepQuery, state.Dependency);

            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    public partial struct DeadSheepDestroyJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter Ecb;

        [BurstCompile]
        public void Execute([ChunkIndexInQuery] int chunkIndexInQuery, Entity entity)
        {
            Ecb.DestroyEntity(chunkIndexInQuery, entity);
        }
    }
}
