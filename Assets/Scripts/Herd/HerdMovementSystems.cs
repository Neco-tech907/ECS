using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Esc.Herd
{
    [BurstCompile]
    public partial struct HerdGoalSyncSystem : ISystem
    {
        private EntityQuery _sheepQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sheepQuery = SystemAPI.QueryBuilder()
                .WithAll<MoveTarget, SheepTag>()
                .WithDisabled<Dead>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<HerdGoal>(out Entity goalEntity))
                return;

            ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

            var job = new HerdGoalSyncJob
            {
                GoalEntity = goalEntity,
                TransformLookup = transformLookup
            };
            state.Dependency = job.ScheduleParallel(_sheepQuery, state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct HerdGoalSyncJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        public Entity GoalEntity;

        [BurstCompile]
        public void Execute(ref MoveTarget move)
        {
            if (TransformLookup.TryGetComponent(GoalEntity, out LocalTransform goalTransform))
                move.Position = goalTransform.Position;
        }
    }

    [BurstCompile]
    [UpdateAfter(typeof(HerdGoalSyncSystem))]
    public partial struct MoveToTargetSystem : ISystem
    {
        private EntityQuery _moveQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _moveQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalTransform, MoveTarget, Stamina, SheepTag>()
                .WithDisabled<Dead>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new MoveToTargetJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            };
            state.Dependency = job.ScheduleParallel(_moveQuery, state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct MoveToTargetJob : IJobEntity
    {
        public float DeltaTime;

        [BurstCompile]
        public void Execute(ref LocalTransform transform, in MoveTarget move, ref Stamina stamina)
        {
            if (stamina.Exhausted || stamina.Current <= 0f)
            {
                stamina.Exhausted = true;
                stamina.Current = math.max(0f, stamina.Current);
                return;
            }

            float3 offset = move.Position - transform.Position;
            offset.y = 0f;

            float distance = math.length(offset);
            if (distance <= move.StopDistance)
                return;

            float3 direction = math.normalizesafe(offset);
            float step = math.min(move.Speed * DeltaTime, distance - move.StopDistance);
            transform.Position += direction * step;
            transform.Rotation = quaternion.LookRotationSafe(direction, math.up());

            float left = stamina.Current - stamina.DrainPerSecond * DeltaTime;
            if (left <= 0f)
            {
                stamina.Current = 0f;
                stamina.Exhausted = true;
            }
            else
            {
                stamina.Current = left;
            }
        }
    }
}
