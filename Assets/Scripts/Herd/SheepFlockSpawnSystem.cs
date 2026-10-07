using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Esc.Herd
{
    [BurstCompile]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct SheepFlockSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PrefabsHolder>(out PrefabsHolder prefabs) || prefabs.SheepPrefab == Entity.Null)
                return;

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var spawner in SystemAPI.Query<RefRW<SheepFlockSpawner>>())
            {
                if (spawner.ValueRO.Spawned || spawner.ValueRO.Count <= 0)
                    continue;

                for (int i = 0; i < spawner.ValueRO.Count; i++)
                {
                    float x = spawner.ValueRW.Random.NextFloat(spawner.ValueRO.AreaMin.x, spawner.ValueRO.AreaMax.x);
                    float z = spawner.ValueRW.Random.NextFloat(spawner.ValueRO.AreaMin.y, spawner.ValueRO.AreaMax.y);
                    bool insideRock = math.abs(x - 6f) <= 3f && math.abs(z - 4f) <= 4f;
                    if (insideRock)
                    {
                        x = math.clamp(x - 8f, spawner.ValueRO.AreaMin.x, spawner.ValueRO.AreaMax.x);
                    }

                    Entity sheep = ecb.Instantiate(prefabs.SheepPrefab);
                    ecb.SetComponent(sheep, LocalTransform.FromPositionRotationScale(
                        new float3(x, 0f, z),
                        quaternion.identity,
                        1f));
                }

                spawner.ValueRW.Spawned = true;
            }
        }
    }
}
