using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Esc.Herd
{
    [BurstCompile]
    public partial struct HerdGoalSyncSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<HerdGoal>(out Entity goalEntity))
                return;

            ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            if (!transformLookup.TryGetComponent(goalEntity, out LocalTransform goalTransform))
                return;

            float3 goalPosition = goalTransform.Position;

            foreach (var move in SystemAPI.Query<RefRW<MoveTarget>>().WithAll<SheepTag>().WithDisabled<Dead>())
                move.ValueRW.Position = goalPosition;
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(HerdGoalSyncSystem))]
    public partial struct MoveToTargetSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, move, stamina) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveTarget>, RefRW<Stamina>>()
                         .WithAll<SheepTag>()
                         .WithDisabled<Dead>())
            {
                if (stamina.ValueRO.Exhausted || stamina.ValueRO.Current <= 0f)
                {
                    stamina.ValueRW.Exhausted = true;
                    stamina.ValueRW.Current = math.max(0f, stamina.ValueRO.Current);
                    continue;
                }

                float3 offset = move.ValueRO.Position - transform.ValueRO.Position;
                offset.y = 0f;

                float distance = math.length(offset);
                if (distance <= move.ValueRO.StopDistance)
                    continue;

                float3 direction = math.normalizesafe(offset);
                float step = math.min(move.ValueRO.Speed * deltaTime, distance - move.ValueRO.StopDistance);
                transform.ValueRW.Position += direction * step;
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, math.up());

                float left = stamina.ValueRO.Current - stamina.ValueRO.DrainPerSecond * deltaTime;
                if (left <= 0f)
                {
                    stamina.ValueRW.Current = 0f;
                    stamina.ValueRW.Exhausted = true;
                }
                else
                {
                    stamina.ValueRW.Current = left;
                }
            }
        }
    }
}
