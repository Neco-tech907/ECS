using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Esc.Herd
{
    [BurstCompile]
    [UpdateBefore(typeof(MoveToTargetSystem))]
    [UpdateBefore(typeof(WolfBrainSystem))]
    public partial struct PenSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<HerdGoal>(out Entity penEntity))
                return;
            if (!SystemAPI.HasComponent<LocalTransform>(penEntity) || !SystemAPI.HasComponent<PenZone>(penEntity))
                return;

            float3 center = SystemAPI.GetComponent<LocalTransform>(penEntity).Position;
            float radius = SystemAPI.GetComponent<PenZone>(penEntity).Radius;
            float radiusSq = radius * radius;

            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<SheepTag>()
                         .WithDisabled<Dead, InPen>()
                         .WithEntityAccess())
            {
                if (math.distancesq(transform.ValueRO.Position.xz, center.xz) <= radiusSq)
                    state.EntityManager.SetComponentEnabled<InPen>(entity, true);
            }
        }
    }
}
