using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Esc.Herd
{
    [BurstCompile]
    [UpdateBefore(typeof(HealthSystem))]
    public partial struct StoneImpactSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonBuffer<StoneImpact>(out DynamicBuffer<StoneImpact> impacts) || impacts.Length == 0)
                return;

            NativeArray<StoneImpact> hits = impacts.ToNativeArray(Allocator.Temp);
            foreach (var (transform, health) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<Health>>()
                         .WithAll<WolfTag>()
                         .WithDisabled<Dead>())
            {
                float2 wolf = transform.ValueRO.Position.xz;
                for (int i = 0; i < hits.Length; i++)
                {
                    float radius = hits[i].Radius;
                    if (math.distancesq(wolf, hits[i].Position.xz) > radius * radius)
                        continue;

                    health.ValueRW.CurrentHealth -= hits[i].Damage;
                    break;
                }
            }

            impacts.Clear();
            hits.Dispose();
        }
    }
}
